namespace Yaapic.Domain.Scripting;

/// <summary>The moment at which a script runs.</summary>
public enum ScriptPhase
{
    /// <summary>Before the request is sent.</summary>
    PreRequest,

    /// <summary>After the response is received.</summary>
    PostResponse,
}
