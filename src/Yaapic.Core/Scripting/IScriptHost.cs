using Yaapic.Domain.Http;

namespace Yaapic.Core.Scripting;

/// <summary>The application side of the <c>yaapic</c> API, called by engines on their own thread.</summary>
public interface IScriptHost
{
    /// <summary>Gets the data behind <c>yaapic.info</c>.</summary>
    ScriptInfo Info { get; }

    /// <summary>Gets the request being sent.</summary>
    ScriptRequest Request { get; }

    /// <summary>Gets the response, or <see langword="null"/> in a pre-request script.</summary>
    ScriptResponse? Response { get; }

    /// <summary>Gets the cookies of the current response; empty in a pre-request script.</summary>
    IReadOnlyList<ScriptCookie> ResponseCookies { get; }

    /// <summary>Reads a variable.</summary>
    /// <param name="scope">The layer to read.</param>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value when found.</param>
    /// <returns><see langword="true"/> when the variable exists.</returns>
    bool TryGetVariable(ScriptScope scope, string name, out string value);

    /// <summary>Sets a variable.</summary>
    /// <param name="scope">The layer to write.</param>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The new value.</param>
    void SetVariable(ScriptScope scope, string name, string value);

    /// <summary>Removes a variable.</summary>
    /// <param name="scope">The layer to write.</param>
    /// <param name="name">The variable name.</param>
    /// <returns><see langword="true"/> when the variable existed.</returns>
    bool UnsetVariable(ScriptScope scope, string name);

    /// <summary>Removes every variable of a layer.</summary>
    /// <param name="scope">The layer to clear.</param>
    void ClearVariables(ScriptScope scope);

    /// <summary>Lists the enabled variables of a layer; <see cref="ScriptScope.Layered"/> merges, highest layer wins.</summary>
    /// <param name="scope">The layer to list.</param>
    /// <returns>The enabled rows.</returns>
    IReadOnlyList<KeyValueEntry> ListVariables(ScriptScope scope);

    /// <summary>Resolves the <c>{{variables}}</c> of a string (<c>yaapic.variables.replaceIn</c>).</summary>
    /// <param name="template">The text to resolve.</param>
    /// <returns>The resolved text.</returns>
    string ReplaceIn(string template);

    /// <summary>Sends a request and blocks until it completes (<c>yaapic.sendRequest</c>).</summary>
    /// <param name="request">The request to send.</param>
    /// <returns>The response, or the failure text.</returns>
    ScriptSendResult Send(ScriptRequest request);
}
