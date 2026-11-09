using Yaapic.Domain.Http;

namespace Yaapic.Core.Scripting;

/// <summary>The <see cref="IScriptHost"/> of one script run; hosts of one stage share the overlay and the request.</summary>
internal sealed class ScriptHost(
    ScriptVariableOverlay overlay,
    ScriptRequest request,
    ScriptResponse? response,
    ScriptInfo info,
    Func<ScriptRequest, CancellationToken, ScriptSendResult>? send = null) : IScriptHost
{
    private readonly Func<ScriptRequest, CancellationToken, ScriptSendResult> _send =
        send ?? ((_, _) => new ScriptSendResult(null, "pm.sendRequest is not available yet"));

    /// <summary>
    /// Gets or sets the token observed by <see cref="Send"/>. <see cref="ScriptedRequestSender"/> sets it to the send's token;
    /// an engine replaces it with a token linked to its timeout so that a blocked <c>pm.sendRequest</c> is interrupted.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    public ScriptInfo Info => info;

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
