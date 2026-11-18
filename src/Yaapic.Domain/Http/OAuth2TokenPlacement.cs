namespace Yaapic.Domain.Http;

/// <summary>Where the access token is sent on the API request.</summary>
public enum OAuth2TokenPlacement
{
    /// <summary>In the Authorization header.</summary>
    Header,

    /// <summary>In a query string parameter.</summary>
    Query,
}
