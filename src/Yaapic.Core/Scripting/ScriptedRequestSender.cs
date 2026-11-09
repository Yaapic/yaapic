using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Scripting;

/// <summary>Sends a request after running the pre-request scripts of its chain (ADR 0009).</summary>
/// <param name="sender">The plain sender used once the scripts have run.</param>
/// <param name="engines">The script engines.</param>
/// <param name="time">The clock; used by the post-response stage.</param>
public sealed class ScriptedRequestSender(RequestSender sender, ScriptEngineRegistry engines, TimeProvider time)
{
    private readonly TimeProvider _time = time;

    /// <summary>Runs the pre-request scripts, then sends the request unless a script failed.</summary>
    /// <param name="input">The request, scopes and script chain.</param>
    /// <param name="ct">The cancellation token; cancelling it aborts the send with a canceled run, never an exception.</param>
    /// <returns>The report; <see cref="ScriptedSendReport.AbortedBy"/> is set when nothing was sent.</returns>
    public async Task<ScriptedSendReport> SendAsync(ScriptedSendInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var steps = input.Chain.Steps.Where(s => !s.Scripts.PreRequest.IsEmpty).ToList();
        if (steps.Count == 0)
        {
            return new(await sender.SendAsync(input.Request, input.Scopes, ct).ConfigureAwait(false), [], [], [], null, input.Request);
        }

        var limits = input.Limits ?? ScriptLimits.Default;
        var overlay = new ScriptVariableOverlay(input.Scopes, input.HasEnvironment, input.HasCollection);
        var request = ScriptRequest.FromDefinition(input.Request, readOnly: false);
        List<ScriptRunResult> runs = [];

        foreach (var step in steps)
        {
            var block = step.Scripts.PreRequest;
            var origin = $"pre-request · {step.Origin}";
            var run = ct.IsCancellationRequested
                ? Failure(origin, block.Language, ScriptRunStatus.Canceled, "Canceled")
                : await engines.Get(block.Language).RunAsync(
                    new ScriptInvocation(
                        block,
                        ScriptPhase.PreRequest,
                        origin,
                        new ScriptHost(overlay, request, null, new ScriptInfo("prerequest", input.Request.Name, input.Request.Id, origin)),
                        limits),
                    ct).ConfigureAwait(false);
            runs.Add(run);
            if (!run.Succeeded)
            {
                return Aborted(runs, overlay, run);
            }
        }

        HttpRequestDefinition definition;
        try
        {
            definition = request.ApplyTo(input.Request);
        }
        catch (InvalidOperationException ex)
        {
            var last = steps[^1];
            var failed = Failure($"pre-request · {last.Origin}", last.Scripts.PreRequest.Language, ScriptRunStatus.Failed, ex.Message);
            runs.Add(failed);
            return Aborted(runs, overlay, failed);
        }

        var report = await sender.SendAsync(definition, overlay.ToScopes(), ct).ConfigureAwait(false);
        return new(report, runs, overlay.Changes, overlay.Warnings, null, definition);
    }

    private static ScriptedSendReport Aborted(List<ScriptRunResult> runs, ScriptVariableOverlay overlay, ScriptRunResult by) =>
        new(null, runs, [], overlay.Warnings, by, null);

    private static ScriptRunResult Failure(string origin, ScriptLanguage language, ScriptRunStatus status, string message) =>
        new(ScriptPhase.PreRequest, origin, language, status, new ScriptError(message), [], [], TimeSpan.Zero);
}
