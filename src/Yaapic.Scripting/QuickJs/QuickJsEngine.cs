using System.Globalization;
using System.Text.Json;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;

namespace Yaapic.Scripting.QuickJs;

/// <summary>
/// Runs JavaScript blocks with QuickJS-NG: a fresh runtime per run on a dedicated thread, memory, stack and time limits,
/// cancellation, console capture and the <c>__host</c> bridge (ADR 0009).
/// </summary>
public sealed class QuickJsEngine : IScriptEngine
{
    private const int ThreadStackBytes = 2 * 1024 * 1024;
    private const int ScriptStackBytes = 1024 * 1024; // below the thread stack: native frames of host calls need room
    private const string PreludeName = "yaapic-prelude";

    private readonly TimeProvider time;
    private readonly Lazy<string?> unavailableReason;

    /// <summary>Initializes an engine using the system clock.</summary>
    public QuickJsEngine()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Initializes an engine.</summary>
    /// <param name="time">The clock measuring run durations and stamping console entries.</param>
    public QuickJsEngine(TimeProvider time)
        : this(time, ProbeNativeLibrary)
    {
    }

    internal QuickJsEngine(TimeProvider time, Func<string?> probe)
    {
        this.time = time ?? throw new ArgumentNullException(nameof(time));
        unavailableReason = new(probe, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public ScriptLanguage Language => ScriptLanguage.JavaScript;

    /// <inheritdoc />
    public bool IsAvailable(out string? reason)
    {
        reason = unavailableReason.Value;
        return reason is null;
    }

    /// <inheritdoc />
    public Task<ScriptRunResult> RunAsync(ScriptInvocation invocation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        if (!IsAvailable(out var reason))
        {
            return Task.FromResult(ScriptRunResult.Unavailable(invocation.Phase, invocation.Origin, Language, reason!));
        }

        TaskCompletionSource<ScriptRunResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // A dedicated thread, never the pool: host calls (pm.sendRequest) block it.
        Thread thread = new(() => completion.SetResult(RunGuarded(invocation, ct)), ThreadStackBytes)
        {
            Name = "yaapic-js",
            IsBackground = true,
        };
        thread.Start();
        return completion.Task;
    }

    private static string? ProbeNativeLibrary()
    {
        try
        {
            using QuickJsRuntime runtime = new();
            using var context = runtime.CreateContext();
            return context.Eval("1").IsSuccess ? null : "quickjs-ng failed its self-test";
        }
        catch (DllNotFoundException)
        {
            return $"quickjs-ng library not found in {AppContext.BaseDirectory}";
        }
        catch (Exception ex) when (ex is BadImageFormatException or EntryPointNotFoundException or PlatformNotSupportedException or InvalidOperationException)
        {
            return $"quickjs-ng could not be loaded: {ex.Message}";
        }
    }

    private static bool TryFindUserFrame(string stack, string origin, out int line, out int? column)
    {
        line = 0;
        column = null;
        var marker = origin + ":";
        foreach (var frame in stack.Split('\n'))
        {
            var at = frame.LastIndexOf(marker, StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            var position = frame[(at + marker.Length)..].TrimEnd().TrimEnd(')').Split(':');
            if (position.Length is 1 or 2 && int.TryParse(position[0], CultureInfo.InvariantCulture, out line))
            {
                column = position.Length == 2 && int.TryParse(position[1], CultureInfo.InvariantCulture, out var c) ? c : null;
                return true;
            }
        }

        return false;
    }

    private static string TimeoutText(TimeSpan timeout) =>
        string.Create(CultureInfo.InvariantCulture, $"Script timed out after {timeout.TotalSeconds:0.##} s");

    private static ScriptError ToScriptError(JsError error, string origin)
    {
        // The first frame can sit in the prelude (a host error thrown by a wrapper): prefer the first frame in the user source.
        int? line = error.Line;
        int? column = null;
        if (error.Stack is { } stack && TryFindUserFrame(stack, origin, out var userLine, out var userColumn))
        {
            line = userLine;
            column = userColumn;
        }

        return new(error.Message, line, column, error.Stack);
    }

    private ScriptRunResult RunGuarded(ScriptInvocation invocation, CancellationToken ct)
    {
        var start = time.GetTimestamp();
        RunCollector collector = new(invocation.Limits, time, invocation.Origin);
        try
        {
            var (status, error) = Execute(invocation, ct, collector);
            return new(invocation.Phase, invocation.Origin, Language, status, error, collector.Console, collector.Tests, time.GetElapsedTime(start));
        }
        catch (Exception ex)
        {
            // The engine contract: nothing escapes the run, an unexpected failure is a failed script.
            return new(invocation.Phase, invocation.Origin, Language, ScriptRunStatus.Failed, new ScriptError(ex.Message), collector.Console, collector.Tests, time.GetElapsedTime(start));
        }
    }

    private (ScriptRunStatus Status, ScriptError? Error) Execute(ScriptInvocation invocation, CancellationToken ct, RunCollector collector)
    {
        var limits = invocation.Limits;
        if (ct.IsCancellationRequested)
        {
            return (ScriptRunStatus.Canceled, new ScriptError("Script canceled"));
        }

        HostBridge bridge = new(invocation.Host, collector);
        using QuickJsRuntime runtime = new((nuint)Math.Max(0, limits.MemoryLimitBytes), ScriptStackBytes);
        using var context = runtime.CreateContext();
        context.DefineHostFunction("__hostRaw", bridge.Invoke);
        runtime.Interrupt.SetTimeout(limits.Timeout);
        using var registration = ct.Register(runtime.Interrupt.Cancel);

        var phase = invocation.Phase == ScriptPhase.PreRequest ? "preRequest" : "postResponse";
        var origin = JsonSerializer.Serialize(invocation.Origin, HostJsonContext.Default.String);
        JsResult setup = new(null, null);
        foreach (var part in JsPrelude.Build())
        {
            setup = context.Eval(part, PreludeName);
            if (!setup.IsSuccess)
            {
                break;
            }
        }

        if (setup.IsSuccess)
        {
            setup = context.Eval($"Object.defineProperty(globalThis, '__yaapic', {{ value: Object.freeze({{ phase: '{phase}', origin: {origin}, errors: [] }}) }});", PreludeName);
        }

        var pending = false;
        var error = setup.IsSuccess ? context.EvalAsync(invocation.Block.Source, invocation.Origin, out pending) : setup.Error;

        if (runtime.Interrupt.Fired)
        {
            return ct.IsCancellationRequested
                ? (ScriptRunStatus.Canceled, new ScriptError("Script canceled"))
                : (ScriptRunStatus.TimedOut, new ScriptError(TimeoutText(limits.Timeout)));
        }

        if (error is not null)
        {
            return error.Kind == JsErrorKind.OutOfMemory
                ? (ScriptRunStatus.MemoryLimit, new ScriptError($"Script exceeded the memory limit of {limits.MemoryLimitBytes / (1024 * 1024)} MiB"))
                : (ScriptRunStatus.Failed, ToScriptError(error, invocation.Origin));
        }

        // A pm.sendRequest callback that threw: the run fails with the first such error.
        var thrown = context.Eval("__yaapic.errors.length === 0 ? '' : String(__yaapic.errors[0]?.message ?? __yaapic.errors[0])", PreludeName);
        if (thrown is { IsSuccess: true, Value: { Length: > 0 } message })
        {
            return (ScriptRunStatus.Failed, new ScriptError(message));
        }

        // Nothing can settle the promise any more (no timers), so waiting for the deadline would only delay the verdict.
        if (pending)
        {
            return (ScriptRunStatus.TimedOut, new ScriptError($"{TimeoutText(limits.Timeout)}: a promise never settled"));
        }

        // The same for a pm.test whose done callback or promise is still open once the jobs are drained.
        var open = context.Eval("__pendingTests()", PreludeName);
        return open.IsSuccess && open.Value != "0"
            ? (ScriptRunStatus.TimedOut, new ScriptError($"{TimeoutText(limits.Timeout)}: a test never completed"))
            : (ScriptRunStatus.Succeeded, null);
    }
}
