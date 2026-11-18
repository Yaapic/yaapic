using System.Security.Cryptography;
using System.Text;

namespace Yaapic.Core.Auth.Digest;

/// <summary>Computes the <c>Authorization: Digest</c> header value (RFC 7616).</summary>
public static class DigestResponse
{
    /// <summary>Builds the full <c>Authorization</c> header value answering a challenge.</summary>
    /// <param name="challenge">The server challenge.</param>
    /// <param name="method">The HTTP method, e.g. <c>GET</c>.</param>
    /// <param name="requestUri">The path and query of the URL, e.g. <c>/dir/index.html?x=1</c>.</param>
    /// <param name="username">The user name.</param>
    /// <param name="password">The password.</param>
    /// <param name="clientNonce">The client nonce.</param>
    /// <returns>The header value, starting with <c>Digest </c>.</returns>
    /// <exception cref="NotSupportedException">The algorithm is unknown, or only <c>auth-int</c> is offered.</exception>
    public static string BuildHeader(
        DigestChallenge challenge,
        string method,
        string requestUri,
        string username,
        string password,
        string clientNonce)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        var (sha256, session) = challenge.Algorithm.ToUpperInvariant() switch
        {
            "MD5" => (false, false),
            "MD5-SESS" => (false, true),
            "SHA-256" => (true, false),
            "SHA-256-SESS" => (true, true),
            _ => throw new NotSupportedException($"Digest algorithm '{challenge.Algorithm}' is not supported."),
        };

        var useAuth = challenge.Qop.Any(q => q.Equals("auth", StringComparison.OrdinalIgnoreCase));
        if (challenge.Qop.Count > 0 && !useAuth)
        {
            throw new NotSupportedException("Digest qop 'auth-int' is not supported.");
        }

        string H(string s) => Convert.ToHexStringLower(
            sha256 ? SHA256.HashData(Encoding.UTF8.GetBytes(s)) : MD5.HashData(Encoding.UTF8.GetBytes(s)));

        var ha1 = H($"{username}:{challenge.Realm}:{password}");
        if (session)
        {
            ha1 = H($"{ha1}:{challenge.Nonce}:{clientNonce}");
        }

        var ha2 = H($"{method}:{requestUri}");
        const string nc = "00000001";
        var response = useAuth
            ? H($"{ha1}:{challenge.Nonce}:{nc}:{clientNonce}:auth:{ha2}")
            : H($"{ha1}:{challenge.Nonce}:{ha2}");

        var sb = new StringBuilder("Digest ")
            .Append("username=\"").Append(Escape(username)).Append("\", ")
            .Append("realm=\"").Append(Escape(challenge.Realm)).Append("\", ")
            .Append("nonce=\"").Append(Escape(challenge.Nonce)).Append("\", ")
            .Append("uri=\"").Append(Escape(requestUri)).Append("\", ")
            .Append("algorithm=").Append(challenge.Algorithm).Append(", ")
            .Append("response=\"").Append(response).Append('"');
        if (useAuth)
        {
            sb.Append(", qop=auth, nc=").Append(nc).Append(", cnonce=\"").Append(Escape(clientNonce)).Append('"');
        }

        if (challenge.Opaque is not null)
        {
            sb.Append(", opaque=\"").Append(Escape(challenge.Opaque)).Append('"');
        }

        return sb.ToString();
    }

    static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
