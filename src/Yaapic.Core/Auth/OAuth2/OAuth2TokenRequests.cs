using System.Text;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Auth.OAuth2;

/// <summary>Builds the token endpoint requests of the OAuth 2.0 grants.</summary>
public static class OAuth2TokenRequests
{
    /// <summary>Builds a <c>client_credentials</c> token request.</summary>
    /// <param name="s">The OAuth 2.0 settings.</param>
    /// <returns>The request to send to the token endpoint.</returns>
    public static ResolvedRequest ClientCredentials(OAuth2Settings s) =>
        Build(s, [("grant_type", "client_credentials"), ("scope", s.Scope), ("audience", s.Audience)], false);

    /// <summary>Builds a <c>password</c> token request.</summary>
    /// <param name="s">The OAuth 2.0 settings.</param>
    /// <returns>The request to send to the token endpoint.</returns>
    public static ResolvedRequest Password(OAuth2Settings s) =>
        Build(s, [("grant_type", "password"), ("username", s.Username), ("password", s.Password), ("scope", s.Scope), ("audience", s.Audience)], false);

    /// <summary>Builds the <c>authorization_code</c> exchange request.</summary>
    /// <param name="s">The OAuth 2.0 settings.</param>
    /// <param name="code">The authorization code.</param>
    /// <param name="codeVerifier">The PKCE code verifier.</param>
    /// <param name="redirectUri">The redirect URI used in the authorization request.</param>
    /// <returns>The request to send to the token endpoint.</returns>
    public static ResolvedRequest AuthorizationCode(OAuth2Settings s, string code, string codeVerifier, string redirectUri) =>
        Build(s, [("grant_type", "authorization_code"), ("code", code), ("redirect_uri", redirectUri), ("code_verifier", codeVerifier)], true);

    /// <summary>Builds a <c>refresh_token</c> request.</summary>
    /// <param name="s">The OAuth 2.0 settings.</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <returns>The request to send to the token endpoint.</returns>
    public static ResolvedRequest Refresh(OAuth2Settings s, string refreshToken) =>
        Build(s, [("grant_type", "refresh_token"), ("refresh_token", refreshToken), ("scope", s.Scope)], false);

    private static ResolvedRequest Build(OAuth2Settings s, (string Key, string Value)[] fields, bool alwaysClientId)
    {
        var headers = new List<KeyValueEntry> { new("Accept", "application/json") };
        var body = fields.Where(f => f.Value.Length > 0 || f.Key is not ("scope" or "audience")).ToList();
        if (s.ClientAuthentication == OAuth2ClientAuthentication.BasicHeader)
        {
            if (s.ClientId.Length > 0 || s.ClientSecret.Length > 0)
            {
                var pair = $"{Uri.EscapeDataString(s.ClientId)}:{Uri.EscapeDataString(s.ClientSecret)}";
                headers.Add(new("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(pair))));
            }

            if (alwaysClientId)
            {
                body.Add(("client_id", s.ClientId));
            }
        }
        else
        {
            body.Add(("client_id", s.ClientId));
            if (s.ClientSecret.Length > 0)
            {
                body.Add(("client_secret", s.ClientSecret));
            }
        }

        var text = string.Join('&', body.Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}"));
        return new ResolvedRequest(
            HttpMethodKind.Post, s.TokenUrl, headers, text, "application/x-www-form-urlencoded", [],
            FollowRedirects: false, TimeoutSeconds: 30);
    }
}
