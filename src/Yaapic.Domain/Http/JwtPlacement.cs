namespace Yaapic.Domain.Http;

/// <summary>Where the signed JWT is sent.</summary>
public enum JwtPlacement
{
    /// <summary>In the Authorization header.</summary>
    Header,

    /// <summary>In a query string parameter.</summary>
    Query,
}
