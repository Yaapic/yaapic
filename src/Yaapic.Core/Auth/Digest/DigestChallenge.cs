using System.Text;

namespace Yaapic.Core.Auth.Digest;

/// <summary>A parsed <c>WWW-Authenticate: Digest</c> challenge (RFC 7616).</summary>
/// <param name="Realm">The protection space.</param>
/// <param name="Nonce">The server nonce.</param>
/// <param name="Opaque">The opaque value to echo back, if any.</param>
/// <param name="Algorithm">The algorithm name as sent by the server (<c>MD5</c> when absent).</param>
/// <param name="Qop">The offered quality-of-protection values (empty for the RFC 2069 form).</param>
/// <param name="Stale">Whether the server flagged the previous nonce as stale.</param>
public sealed record DigestChallenge(
    string Realm,
    string Nonce,
    string? Opaque,
    string Algorithm,
    IReadOnlyList<string> Qop,
    bool Stale)
{
    /// <summary>Parses the first <c>Digest</c> challenge of a <c>WWW-Authenticate</c> value.</summary>
    /// <param name="headerValue">One or several challenges, possibly of several schemes.</param>
    /// <param name="challenge">The parsed challenge, or <see langword="null"/> when none is usable.</param>
    /// <returns><see langword="true"/> when a Digest challenge with a realm and a nonce was found.</returns>
    public static bool TryParse(string headerValue, out DigestChallenge? challenge)
    {
        challenge = null;
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        Dictionary<string, string>? parameters = null;
        var i = 0;
        while (i < headerValue.Length)
        {
            while (i < headerValue.Length && (char.IsWhiteSpace(headerValue[i]) || headerValue[i] == ','))
            {
                i++;
            }

            var start = i;
            while (i < headerValue.Length && !char.IsWhiteSpace(headerValue[i]) && headerValue[i] != ',' && headerValue[i] != '=')
            {
                i++;
            }

            var token = headerValue[start..i];
            while (i < headerValue.Length && char.IsWhiteSpace(headerValue[i]))
            {
                i++;
            }

            if (i < headerValue.Length && headerValue[i] == '=')
            {
                i++;
                while (i < headerValue.Length && char.IsWhiteSpace(headerValue[i]))
                {
                    i++;
                }

                var value = ReadValue(headerValue, ref i);
                if (parameters is not null)
                {
                    parameters[token] = value;
                }
            }
            else if (token.Length > 0)
            {
                if (parameters is not null)
                {
                    break; // the Digest challenge ended, another scheme starts
                }

                if (token.Equals("Digest", StringComparison.OrdinalIgnoreCase))
                {
                    parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        if (parameters is null
            || !parameters.TryGetValue("realm", out var realm)
            || !parameters.TryGetValue("nonce", out var nonce))
        {
            return false;
        }

        parameters.TryGetValue("opaque", out var opaque);
        var qop = parameters.TryGetValue("qop", out var q)
            ? q.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        challenge = new DigestChallenge(
            realm,
            nonce,
            opaque,
            parameters.GetValueOrDefault("algorithm", "MD5"),
            qop,
            parameters.TryGetValue("stale", out var stale) && stale.Equals("true", StringComparison.OrdinalIgnoreCase));
        return true;
    }

    static string ReadValue(string s, ref int i)
    {
        if (i < s.Length && s[i] == '"')
        {
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                }

                sb.Append(s[i++]);
            }

            i++; // closing quote
            return sb.ToString();
        }

        var start = i;
        while (i < s.Length && s[i] != ',')
        {
            i++;
        }

        return s[start..i].Trim();
    }
}
