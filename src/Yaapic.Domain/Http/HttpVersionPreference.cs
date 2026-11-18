namespace Yaapic.Domain.Http;

/// <summary>The HTTP version a request prefers.</summary>
public enum HttpVersionPreference
{
    /// <summary>HTTP/1.1 with an upgrade to HTTP/2 through ALPN.</summary>
    Default,

    /// <summary>HTTP/1.1 only.</summary>
    Http1,

    /// <summary>HTTP/2.</summary>
    Http2,

    /// <summary>HTTP/3.</summary>
    Http3,
}
