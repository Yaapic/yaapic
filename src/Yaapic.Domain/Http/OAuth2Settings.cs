namespace Yaapic.Domain.Http;

/// <summary>The OAuth 2.0 configuration of a request.</summary>
/// <param name="Grant">The grant type.</param>
/// <param name="TokenUrl">The token endpoint URL.</param>
/// <param name="AuthorizationUrl">The authorization endpoint URL (authorization code grant).</param>
/// <param name="ClientId">The client identifier.</param>
/// <param name="ClientSecret">The client secret.</param>
/// <param name="Scope">The space-separated scopes.</param>
/// <param name="Audience">The optional audience.</param>
/// <param name="Username">The resource owner username (password grant).</param>
/// <param name="Password">The resource owner password (password grant).</param>
/// <param name="RedirectUri">The loopback redirect URI (authorization code grant).</param>
/// <param name="ClientAuthentication">How the client credentials reach the token endpoint.</param>
/// <param name="Placement">Where the access token is sent.</param>
/// <param name="HeaderPrefix">The Authorization header prefix.</param>
/// <param name="QueryParameterName">The query parameter name when the token goes in the query string.</param>
public sealed record OAuth2Settings(
    OAuth2Grant Grant,
    string TokenUrl,
    string AuthorizationUrl,
    string ClientId,
    string ClientSecret,
    string Scope,
    string Audience,
    string Username,
    string Password,
    string RedirectUri,
    OAuth2ClientAuthentication ClientAuthentication,
    OAuth2TokenPlacement Placement,
    string HeaderPrefix,
    string QueryParameterName)
{
    /// <summary>The defaults of a new OAuth 2.0 configuration.</summary>
    public static OAuth2Settings Default { get; } = new(
        OAuth2Grant.ClientCredentials, "", "", "", "", "", "", "", "",
        "http://127.0.0.1:8742/callback",
        OAuth2ClientAuthentication.BasicHeader, OAuth2TokenPlacement.Header, "Bearer", "access_token");
}
