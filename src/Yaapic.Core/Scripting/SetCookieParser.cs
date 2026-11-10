using System.Globalization;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Scripting;

/// <summary>Parses <c>Set-Cookie</c> response headers; nothing is stored.</summary>
public static class SetCookieParser
{
    /// <summary>Parses every <c>Set-Cookie</c> header, one cookie each; malformed headers are skipped.</summary>
    /// <param name="responseHeaders">The response headers.</param>
    /// <returns>The cookies in header order.</returns>
    public static IReadOnlyList<ScriptCookie> Parse(IEnumerable<KeyValueEntry> responseHeaders)
    {
        ArgumentNullException.ThrowIfNull(responseHeaders);
        List<ScriptCookie> cookies = [];
        foreach (var header in responseHeaders)
        {
            if (string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase)
                && ParseOne(header.Value) is { } cookie)
            {
                cookies.Add(cookie);
            }
        }

        return cookies;
    }

    private static ScriptCookie? ParseOne(string header)
    {
        var parts = header.Split(';');
        var eq = parts[0].IndexOf('=');
        if (eq <= 0)
        {
            return null;
        }

        var name = parts[0][..eq].Trim();
        if (name.Length == 0)
        {
            return null;
        }

        string? domain = null, path = null, sameSite = null;
        DateTimeOffset? expires = null;
        bool httpOnly = false, secure = false;
        foreach (var part in parts.Skip(1))
        {
            var kv = part.IndexOf('=');
            var key = (kv < 0 ? part : part[..kv]).Trim();
            var value = kv < 0 ? "" : part[(kv + 1)..].Trim();
            switch (key.ToLowerInvariant())
            {
                case "domain": domain = value; break;
                case "path": path = value; break;
                case "samesite": sameSite = value; break;
                case "httponly": httpOnly = true; break;
                case "secure": secure = true; break;
                case "expires":
                    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                    {
                        expires = date;
                    }

                    break;
                default:
                    // Max-Age is ignored in v1: Expires stays null rather than being computed from the current time.
                    break;
            }
        }

        return new ScriptCookie(name, Unquote(parts[0][(eq + 1)..].Trim()), domain, path, expires, httpOnly, secure, sameSite);
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
}
