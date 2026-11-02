namespace Yaapic.Domain.Http;

/// <summary>Kind of payload carried by a request.</summary>
public enum BodyKind
{
    /// <summary>The request has no body.</summary>
    None,

    /// <summary>The body is raw text.</summary>
    Raw,

    /// <summary>The body is an application/x-www-form-urlencoded form.</summary>
    UrlEncoded,

    /// <summary>The body is a multipart/form-data form.</summary>
    FormData,

    /// <summary>The body is the content of a file.</summary>
    Binary,
}
