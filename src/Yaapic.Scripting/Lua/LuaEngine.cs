using System.Text.Json;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.QuickJs;

namespace Yaapic.Scripting.Lua;

/// <summary>
/// Runs Lua blocks with Lua 5.4: a fresh state per run on a dedicated thread, memory and time limits, cancellation,
/// console capture and the <c>__host</c> bridge shared with JavaScript (ADR 0009).
/// </summary>
public sealed class LuaEngine : IScriptEngine
{
    private const int ThreadStackBytes = 2 * 1024 * 1024; // Lua also recurses on the C stack
    private const string PreludeName = "yaapic-prelude";

    private readonly TimeProvider time;
    private readonly Lazy<string?> unavailableReason;

    /// <summary>Initializes an engine using the system clock.</summary>
    public LuaEngine()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Initializes an engine.</summary>
    /// <param name="time">The clock measuring run durations and stamping console entries.</param>
    public LuaEngine(TimeProvider time)
        : this(time, ProbeNativeLibrary)
    {
    }

    internal LuaEngine(TimeProvider time, Func<string?> probe)
    {
        this.time = time ?? throw new ArgumentNullException(nameof(time));
        unavailableReason = new(probe, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public ScriptLanguage Language => ScriptLanguage.Lua;

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
            Name = "yaapic-lua",
            IsBackground = true,
        };
        thread.Start();
        return completion.Task;
    }

    private static string? ProbeNativeLibrary()
    {
        try
        {
            using LuaState state = new();
            return state.Run("local x = 1", "probe").IsSuccess ? null : "lua54 failed its self-test";
        }
        catch (DllNotFoundException)
        {
            return $"lua54 library not found in {AppContext.BaseDirectory}";
        }
        catch (Exception ex) when (ex is BadImageFormatException or EntryPointNotFoundException or PlatformNotSupportedException or InvalidOperationException)
        {
            return $"lua54 could not be loaded: {ex.Message}";
        }
    }

    // A host failure becomes a Lua error (the shim raises it) so scripts can pcall it; the prelude adds the script line.
    private static string CallHost(HostBridge bridge, string op, string args)
    {
        var reply = bridge.Invoke(op, args);
        if (!reply.StartsWith("{\"err\":", StringComparison.Ordinal))
        {
            return reply;
        }

        using var doc = JsonDocument.Parse(reply);
        throw new InvalidOperationException(doc.RootElement.GetProperty("err").GetString());
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
        using LuaState state = new(Math.Max(0, limits.MemoryLimitBytes));
        state.DefineHost((op, args) => CallHost(bridge, op, args));
        state.SetTimeout(limits.Timeout);
        using var registration = ct.Register(state.Cancel);

        var phase = invocation.Phase == ScriptPhase.PreRequest ? "preRequest" : "postResponse";
        var result = state.Run(LuaPrelude.Core, PreludeName);
        if (result.IsSuccess)
        {
            result = state.Run(LuaPmJsonPrelude.Source, PreludeName);
        }

        if (result.IsSuccess)
        {
            result = state.Run(LuaPmVariablesPrelude.Source, PreludeName);
        }

        if (result.IsSuccess)
        {
            result = state.Run(LuaPrelude.Context(phase, invocation.Origin), PreludeName);
        }

        if (result.IsSuccess)
        {
            result = state.Run(invocation.Block.Source, invocation.Origin);
        }

        return result.Error switch
        {
            null => (ScriptRunStatus.Succeeded, null),
            { Kind: LuaErrorKind.Canceled } => (ScriptRunStatus.Canceled, new ScriptError("Script canceled")),
            { Kind: LuaErrorKind.Timeout } => (ScriptRunStatus.TimedOut, new ScriptError(QuickJsEngine.TimeoutText(limits.Timeout))),
            { Kind: LuaErrorKind.OutOfMemory } => (ScriptRunStatus.MemoryLimit, new ScriptError($"Script exceeded the memory limit of {limits.MemoryLimitBytes / (1024 * 1024)} MiB")),
            var error => (ScriptRunStatus.Failed, new ScriptError(error.Message, error.Line, null, error.Traceback)),
        };
    }
}
