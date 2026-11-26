using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Runner;

/// <summary>The outcome of one executed step of a run.</summary>
/// <param name="Result">The request result.</param>
/// <param name="Report">The scripted send report, for the console panel.</param>
public sealed record RunStepResult(RunRequestResult Result, ScriptedSendReport Report)
{
    /// <summary>Gets a value indicating whether the step ended because of a cancellation.</summary>
    public bool IsCanceled => Report.AbortedBy is { Status: ScriptRunStatus.Canceled }
        || Report.Report?.Result.Failure?.Kind == SendFailureKind.Canceled;
}

/// <summary>Executes one request of a run: scripts, send, variable changes and result mapping (ADR 0018). Reused by load tests.</summary>
/// <param name="sender">The scripted sender.</param>
/// <param name="chains">The script chain provider.</param>
/// <param name="scopes">The variable scope source and persistence port.</param>
public sealed class RunStepExecutor(ScriptedRequestSender sender, IScriptChainProvider chains, IRunScopeSource scopes)
{
    /// <summary>Sends one request of a run.</summary>
    /// <param name="item">The request to send.</param>
    /// <param name="state">The in-memory variable state of the run; receives the changes of the scripts.</param>
    /// <param name="context">The run context of the iteration.</param>
    /// <param name="persist">Whether variable changes are saved through the scope source.</param>
    /// <param name="limits">The script limits, or <see langword="null"/> for the defaults.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The step result.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public async Task<RunStepResult> ExecuteAsync(RunItem item, RunVariableState state, ScriptRunContext context, bool persist, ScriptLimits? limits, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        HttpRequestDefinition request = item.Request;
        Guid id = request.Id;
        VariableScopes overlaid = state.Overlay(scopes.GetScopes(id), new IterationDataRow(context.Data));
        IReadOnlyList<VariableScopeKind> writable = scopes.GetWritableScopes(id);
        var input = new ScriptedSendInput(
            request,
            overlaid,
            chains.GetAncestors(id).WithRequest(request.Name, request.Scripts),
            writable.Contains(VariableScopeKind.Environment),
            writable.Contains(VariableScopeKind.Collection),
            limits,
            context);
        ScriptedSendReport report = await sender.SendAsync(input, ct).ConfigureAwait(false);
        state.Apply(report.Changes);
        List<string> warnings = [.. report.Warnings];
        if (persist && report.Changes.Count > 0)
        {
            foreach (string error in await scopes.PersistAsync(id, report.Changes, ct).ConfigureAwait(false))
            {
                warnings.Add(Strings.RequestVariableChangesNotSaved(error));
            }
        }

        return new RunStepResult(ToResult(item, report, warnings), report);
    }

    private static RunRequestResult ToResult(RunItem item, ScriptedSendReport report, List<string> warnings)
    {
        HttpResponseData? response = report.Report?.Result.Response;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<ConsoleEntry> console = [.. report.Console, .. report.NetworkLog, .. warnings.Select(w => new ConsoleEntry(now, ConsoleLevel.Warn, ConsoleSource.System, w))];
        return new RunRequestResult(
            item.Request.Id,
            item.Request.Name,
            item.Path,
            item.Request.Method,
            item.Request.Url,
            RunOutcomes.From(report),
            response?.StatusCode,
            response?.Duration ?? TimeSpan.Zero,
            response?.SizeBytes ?? 0,
            report.Tests,
            console,
            ErrorOf(report));
    }

    private static string? ErrorOf(ScriptedSendReport report)
    {
        if (report.AbortedBy is { } aborted)
        {
            return aborted.Error?.Message ?? StatusText(aborted.Status);
        }

        return report.Report?.Result.Failure?.Message;
    }

    private static string StatusText(ScriptRunStatus status) => status switch
    {
        ScriptRunStatus.Canceled => Strings.ScriptStatusCanceled,
        ScriptRunStatus.TimedOut => Strings.ScriptStatusTimedOut,
        ScriptRunStatus.MemoryLimit => Strings.ScriptStatusMemoryLimit,
        ScriptRunStatus.Unavailable => Strings.ScriptStatusUnavailable,
        _ => Strings.ScriptStatusError,
    };
}
