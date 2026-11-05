namespace Yaapic.Domain.Http;

/// <summary>Where an API key is sent.</summary>
public enum ApiKeyLocation
{
    /// <summary>In a request header.</summary>
    Header,

    /// <summary>In the query string.</summary>
    Query,
}
