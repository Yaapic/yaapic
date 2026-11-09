using Yaapic.Core.Http;
using Yaapic.Core.Responses;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Scripting;

/// <summary>Sends a request after running the pre-request scripts of its chain (ADR 0009).</summary>
/// <param name="sender">The plain sender used once the scripts have run.</param>
/// <param name="engines">The script engines.</param>
/// <param name="time">The clock; stamps the network console lines.</param>
public sealed class ScriptedRequestSender(RequestSender sender, ScriptEngineRegistry engines, TimeProvider time)
{
    private readonly TimeProvider _time = time;

    /// <summary>Runs the pre-request scripts, sends the request unless one failed, then runs the post-response scripts after a response.</summary>
    /// <param name="input">The request, scopes and script chain.</param>
    /// <param name="ct">The cancellation token; cancelling it aborts the send with a canceled run, never an exception.</param>
    /// <returns>The report; <see cref="ScriptedSendReport.AbortedBy"/> is set when nothing was sent.</returns>
    public async Task<ScriptedSendReport> SendAsync(ScriptedSendInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var limits = input.Limits ?? ScriptLimits.Default;
        var overlay = new ScriptVariableOverlay(input.Scopes, input.HasEnvironment, input.HasCollection);
        var request = ScriptRequest.FromDefinition(input.Request, readOnly: false);
        List<ScriptRunResult> runs = [];
        ScriptSendService sends = new(sender, overlay, limits.MaxSendRequests, _time);

        var preSteps = input.Chain.Steps.Where(s => !s.Scripts.PreRequest.IsEmpty).ToList();
        foreach (var step in preSteps)
        {
            var block = step.Scripts.PreRequest;
            var origin = $"pre-request · {step.Origin}";
            var run = ct.IsCancellationRequested
                ? Failure(ScriptPhase.PreRequest, origin, block.Language, ScriptRunStatus.Canceled, "Canceled")
                : await engines.Get(block.Language).RunAsync(
                    new ScriptInvocation(
                        block,
                        ScriptPhase.PreRequest,
                        origin,
                        new ScriptHost(overlay, request, null, new ScriptInfo("prerequest", input.Request.Name, input.Request.Id, origin), sends.Send) { CancellationToken = ct },
                        limits),
                    ct).ConfigureAwait(false);
            runs.Add(run);
            if (!run.Succeeded)
            {
                return Aborted(runs, overlay, run, sends);
            }
        }

        var definition = input.Request;
        if (preSteps.Count > 0)
        {
            try
            {
                definition = request.ApplyTo(input.Request);
            }
            catch (InvalidOperationException ex)
            {
                var last = preSteps[^1];
                var failed = Failure(ScriptPhase.PreRequest, $"pre-request · {last.Origin}", last.Scripts.PreRequest.Language, ScriptRunStatus.Failed, ex.Message);
                runs.Add(failed);
                return Aborted(runs, overlay, failed, sends);
            }
        }

        var report = await sender.SendAsync(definition, preSteps.Count > 0 ? overlay.ToScopes() : input.Scopes, ct).ConfigureAwait(false);
        List<ConsoleEntry> networkLog = [.. sends.NetworkLog, .. NetworkLog(_time.GetUtcNow(), definition, report)];

        if (report.Result.IsSuccess)
        {
            await RunPostResponseAsync(input, definition, report.Result.Response!, overlay, limits, sends, runs, networkLog, ct).ConfigureAwait(false);
        }

        return new(report, runs, overlay.Changes, overlay.Warnings, null, definition, networkLog);
    }

    /// <summary>
    /// Runs the post-response blocks in chain order. Unlike the pre-request stage, a failed block does not stop the chain,
    /// and the variable changes of failed blocks are kept; only a cancelled token stops it, with a canceled run.
    /// </summary>
    private async Task RunPostResponseAsync(
        ScriptedSendInput input,
        HttpRequestDefinition definition,
        HttpResponseData response,
        ScriptVariableOverlay overlay,
        ScriptLimits limits,
        ScriptSendService sends,
        List<ScriptRunResult> runs,
        List<ConsoleEntry> networkLog,
        CancellationToken ct)
    {
        var scriptResponse = ScriptResponse.FromData(response);
        foreach (var step in input.Chain.Steps.Where(s => !s.Scripts.PostResponse.IsEmpty))
        {
            var block = step.Scripts.PostResponse;
            var origin = $"post-response · {step.Origin}";
            if (ct.IsCancellationRequested)
            {
                runs.Add(Failure(ScriptPhase.PostResponse, origin, block.Language, ScriptRunStatus.Canceled, "Canceled"));
                return;
            }

            var host = new ScriptHost(
                overlay,
                ScriptRequest.FromDefinition(definition, readOnly: true),
                scriptResponse,
                new ScriptInfo("test", input.Request.Name, input.Request.Id, origin),
                sends.Send) { CancellationToken = ct };
            var logged = sends.NetworkLog.Count;
            runs.Add(await engines.Get(block.Language)
                .RunAsync(new ScriptInvocation(block, ScriptPhase.PostResponse, origin, host, limits), ct).ConfigureAwait(false));
            networkLog.AddRange(sends.NetworkLog.Skip(logged));
        }
    }

    internal static List<ConsoleEntry> NetworkLog(DateTimeOffset now, HttpRequestDefinition definition, SendReport report, string? origin = null)
    {
        var method = definition.Method.ToText();
        List<ConsoleEntry> log = [];
        if (report.Result.Response is { } r)
        {
            var level = r.IsSuccessStatusCode ? ConsoleLevel.Info : ConsoleLevel.Error;
            log.Add(new(now, level, ConsoleSource.Network, $"{method} {r.FinalUrl}   {r.StatusCode} · {ResponseText.FormatDuration(r.Duration)} · {ResponseText.FormatSize(r.SizeBytes)}", origin));
        }
        else
        {
            log.Add(new(now, ConsoleLevel.Error, ConsoleSource.Network, $"{method} {definition.Url}   {report.Result.Failure?.Message}", origin));
        }

        if (report.UnresolvedVariables.Count > 0)
        {
            log.Add(new(now, ConsoleLevel.Warn, ConsoleSource.System, $"Unresolved variables: {string.Join(", ", report.UnresolvedVariables)}", origin));
        }

        return log;
    }

    private static ScriptedSendReport Aborted(List<ScriptRunResult> runs, ScriptVariableOverlay overlay, ScriptRunResult by, ScriptSendService sends) =>
        new(null, runs, [], overlay.Warnings, by, null, [.. sends.NetworkLog]);

    private static ScriptRunResult Failure(ScriptPhase phase, string origin, ScriptLanguage language, ScriptRunStatus status, string message) =>
        new(phase, origin, language, status, new ScriptError(message), [], [], TimeSpan.Zero);
}
