namespace Yaapic.Domain.Http;

/// <summary>Kind of payload carried by a request.</summary>
public enum BodyKind
{
    /// <summary>The request has no body.</summary>
    None,

    /// <summary>The body is raw text.</summary>
    Raw,
}
