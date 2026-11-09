using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Scripting;

/// <summary>
/// Performs the <c>pm.sendRequest</c> calls of one scripted send. Sub-requests run no scripts and are not recorded in the
/// history; each one appends network console entries to <see cref="NetworkLog"/>.
/// </summary>
/// <param name="sender">The plain sender used for sub-requests.</param>
/// <param name="overlay">The live variable overlay; its current scopes resolve each sub-request.</param>
/// <param name="maxSends">The number of sub-requests allowed.</param>
/// <param name="time">The clock stamping the console entries.</param>
internal sealed class ScriptSendService(RequestSender sender, ScriptVariableOverlay overlay, int maxSends, TimeProvider time)
{
    internal const string Origin = "pm.sendRequest";

    private readonly List<ConsoleEntry> _networkLog = [];
    private int _count;

    /// <summary>Gets the network console entries of the sub-requests sent so far.</summary>
    public IReadOnlyList<ConsoleEntry> NetworkLog => _networkLog;

    /// <summary>
    /// Sends a request and blocks until it completes. It runs on the engine thread, never the UI thread, and
    /// <see cref="RequestSender"/> uses <c>ConfigureAwait(false)</c>, so waiting on the task cannot deadlock.
    /// </summary>
    /// <param name="request">The request built by the script.</param>
    /// <param name="ct">Cancels the in-flight send; engines cancel it when the block times out.</param>
    /// <returns>The response, or the failure text.</returns>
    public ScriptSendResult Send(ScriptRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (++_count > maxSends)
        {
            return new(null, $"pm.sendRequest limit of {maxSends} per run reached");
        }

        HttpRequestDefinition definition;
        try
        {
            definition = request.ApplyTo(HttpRequestDefinition.Create(Origin));
        }
        catch (InvalidOperationException ex)
        {
            return new(null, ex.Message);
        }

        var report = sender.SendAsync(definition, overlay.ToScopes(), ct).GetAwaiter().GetResult();
        _networkLog.AddRange(ScriptedRequestSender.NetworkLog(time.GetUtcNow(), definition, report, Origin));
        return report.Result.Response is { } response
            ? new(ScriptResponse.FromData(response), null)
            : new(null, report.Result.Failure?.Message ?? "Request failed");
    }
}
