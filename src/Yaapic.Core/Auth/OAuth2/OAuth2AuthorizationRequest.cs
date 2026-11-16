using System.Security.Cryptography;
using System.Text;
using Yaapic.Core.Auth.Jwt;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Auth.OAuth2;

/// <summary>The query of the redirect received after the authorization request.</summary>
/// <param name="Code">The authorization code.</param>
/// <param name="Error">The error code.</param>
/// <param name="ErrorDescription">The human-readable error description.</param>
/// <param name="State">The echoed state.</param>
public sealed record AuthorizationCallback(string? Code, string? Error, string? ErrorDescription, string? State);

/// <summary>Builds the authorization request URL and parses the redirect (RFC 6749 section 4.1).</summary>
public static class OAuth2AuthorizationRequest
{
    /// <summary>Creates a random <c>state</c> value.</summary>
    /// <returns>16 random bytes as base64url.</returns>
    public static string NewState() => Base64Url.Encode(RandomNumberGenerator.GetBytes(16));

    /// <summary>Builds the authorization URL with PKCE.</summary>
    /// <param name="s">The OAuth 2.0 settings.</param>
    /// <param name="state">The state value.</param>
    /// <param name="codeChallenge">The S256 code challenge.</param>
    /// <returns>The URL to open in the browser.</returns>
    /// <exception cref="ArgumentException">The authorization URL is invalid or not http(s).</exception>
    public static Uri BuildUrl(OAuth2Settings s, string state, string codeChallenge)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!Uri.TryCreate(s.AuthorizationUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("The authorization URL must be an absolute http or https URL.", nameof(s));
        }

        var sb = new StringBuilder();
        void Add(string key, string value)
        {
            if (sb.Length > 0)
            {
                sb.Append('&');
            }

            sb.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
        }

        Add("response_type", "code");
        Add("client_id", s.ClientId);
        Add("redirect_uri", s.RedirectUri);
        Add("state", state);
        Add("code_challenge", codeChallenge);
        Add("code_challenge_method", "S256");
        if (s.Scope.Length > 0)
        {
            Add("scope", s.Scope);
        }

        if (s.Audience.Length > 0)
        {
            Add("audience", s.Audience);
        }

        var existing = baseUri.Query.TrimStart('?');
        return new UriBuilder(baseUri) { Query = existing.Length > 0 ? existing + "&" + sb : sb.ToString() }.Uri;
    }

    /// <summary>Parses the request target of the redirect, for example <c>/callback?code=abc&amp;state=xyz</c>.</summary>
    /// <param name="requestTarget">The path and query.</param>
    /// <returns>The callback values; the first value of a duplicated name wins.</returns>
    public static AuthorizationCallback ParseCallback(string requestTarget)
    {
        ArgumentNullException.ThrowIfNull(requestTarget);
        var q = requestTarget.IndexOf('?', StringComparison.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (q >= 0)
        {
            foreach (var part in requestTarget[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=', StringComparison.Ordinal);
                var key = Decode(eq < 0 ? part : part[..eq]);
                values.TryAdd(key, eq < 0 ? "" : Decode(part[(eq + 1)..]));
            }
        }

        return new(values.GetValueOrDefault("code"), values.GetValueOrDefault("error"),
            values.GetValueOrDefault("error_description"), values.GetValueOrDefault("state"));
    }

    /// <summary>Extracts the loopback listen address of a redirect URI.</summary>
    /// <param name="redirectUri">The redirect URI.</param>
    /// <param name="port">The port to listen on.</param>
    /// <param name="path">The path to expect.</param>
    /// <returns><see langword="true"/> for an <c>http</c> loopback URI with an explicit port.</returns>
    public static bool TryGetListenAddress(string redirectUri, out int port, out string path)
    {
        port = 0;
        path = "";
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
            || uri.Scheme != "http" || uri.Host is not ("127.0.0.1" or "localhost")
            || uri.Port is < 1 or > 65535
            || !redirectUri.Contains($":{uri.Port}", StringComparison.Ordinal))
        {
            return false;
        }

        port = uri.Port;
        path = uri.AbsolutePath;
        return true;
    }

    private static string Decode(string s) => Uri.UnescapeDataString(s.Replace('+', ' '));
}
