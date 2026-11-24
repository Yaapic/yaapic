using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Scripting;

/// <summary>The <see cref="IScriptHost"/> of one script run; hosts of one stage share the overlay and the request.</summary>
internal sealed class ScriptHost(
    ScriptVariableOverlay overlay,
    ScriptRequest request,
    ScriptResponse? response,
    ScriptInfo info,
    ScriptRunContext run,
    ScriptExecutionControl control,
    Func<ScriptRequest, CancellationToken, ScriptSendResult>? send = null) : IScriptHost
{
    private readonly Func<ScriptRequest, CancellationToken, ScriptSendResult> _send =
        send ?? ((_, _) => new ScriptSendResult(null, "yaapic.sendRequest is not available yet"));

    /// <summary>
    /// Gets or sets the token observed by <see cref="Send"/>. <see cref="ScriptedRequestSender"/> sets it to the send's token;
    /// an engine replaces it with a token linked to its timeout so that a blocked <c>yaapic.sendRequest</c> is interrupted.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    public ScriptInfo Info => info with { Iteration = run.Iteration, IterationCount = run.IterationCount };

    public IReadOnlyList<KeyValueEntry> IterationData => run.Data;

    public void SetNextRequest(string? nameOrId) => control.SetNextRequest(nameOrId, run.InRunner);

    public void SkipRequest() => control.SkipRequest(response is null ? ScriptPhase.PreRequest : ScriptPhase.PostResponse);

    public ScriptRequest Request => request;

    public ScriptResponse? Response => response;

    public IReadOnlyList<ScriptCookie> ResponseCookies => response?.Cookies ?? [];

    public bool TryGetVariable(ScriptScope scope, string name, out string value) => overlay.TryGet(scope, name, out value);

    public void SetVariable(ScriptScope scope, string name, string value) => overlay.Set(scope, name, value);

    public bool UnsetVariable(ScriptScope scope, string name)
    {
        var existed = overlay.TryGet(scope, name, out _);
        overlay.Unset(scope, name);
        return existed;
    }

    public void ClearVariables(ScriptScope scope) => overlay.Clear(scope);

    public IReadOnlyList<KeyValueEntry> ListVariables(ScriptScope scope) =>
        [.. overlay.List(scope).Select(p => new KeyValueEntry(p.Key, p.Value))];

    public string ReplaceIn(string template) => overlay.ReplaceIn(template);

    public ScriptSendResult Send(ScriptRequest scriptRequest) => _send(scriptRequest, CancellationToken);
}
