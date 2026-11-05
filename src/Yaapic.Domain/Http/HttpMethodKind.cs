namespace Yaapic.Domain.Http;

/// <summary>HTTP methods supported by the request editor.</summary>
public enum HttpMethodKind
{
    /// <summary>The GET method.</summary>
    Get,

    /// <summary>The POST method.</summary>
    Post,

    /// <summary>The PUT method.</summary>
    Put,

    /// <summary>The PATCH method.</summary>
    Patch,

    /// <summary>The DELETE method.</summary>
    Delete,

    /// <summary>The HEAD method.</summary>
    Head,

    /// <summary>The OPTIONS method.</summary>
    Options,
}
