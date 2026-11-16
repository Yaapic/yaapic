using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Yaapic.Core.Auth.Jwt;

/// <summary>The decoded parts of a JWT. Nothing here has been verified.</summary>
/// <param name="HeaderJson">The pretty-printed JOSE header.</param>
/// <param name="PayloadJson">The pretty-printed claims set.</param>
/// <param name="Signature">The third part as written in the token (empty for an unsigned token).</param>
/// <param name="Algorithm">The header <c>alg</c> value, when it is a string.</param>
/// <param name="IssuedAt">The <c>iat</c> claim, when present and numeric.</param>
/// <param name="NotBefore">The <c>nbf</c> claim, when present and numeric.</param>
/// <param name="ExpiresAt">The <c>exp</c> claim, when present and numeric.</param>
public sealed record DecodedJwt(
    string HeaderJson,
    string PayloadJson,
    string Signature,
    string? Algorithm,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? NotBefore,
    DateTimeOffset? ExpiresAt);

/// <summary>The time-based state of a decoded JWT.</summary>
public enum JwtStatus
{
    /// <summary>The token has an expiry that has not been reached and is already usable.</summary>
    Valid,

    /// <summary>The <c>exp</c> claim is at or before the reference time.</summary>
    Expired,

    /// <summary>The <c>nbf</c> claim is after the reference time.</summary>
    NotYetValid,

    /// <summary>The token has no <c>exp</c> claim.</summary>
    NoExpiry,
}

/// <summary>Decodes JWTs for display. It never verifies a signature.</summary>
public static class JwtDecoder
{
    const string BearerPrefix = "Bearer ";

    /// <summary>Decodes the header and payload of a JWT. The signature is never verified.</summary>
    /// <param name="token">The token, optionally prefixed with <c>Bearer </c> and surrounded by whitespace.</param>
    /// <param name="jwt">The decoded token on success.</param>
    /// <param name="error">An English diagnostic on failure (the view model localizes its own message).</param>
    /// <returns><see langword="true"/> when the token could be decoded.</returns>
    public static bool TryDecode(string token, out DecodedJwt? jwt, out string? error)
    {
        jwt = null;
        error = null;
        var text = (token ?? "").Trim();
        if (text.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            text = text[BearerPrefix.Length..].Trim();
        }

        var parts = text.Split('.');
        if (parts.Length != 3)
        {
            error = "A JWT needs exactly three dot-separated parts.";
            return false;
        }

        using var header = ParsePart(parts[0], "header", out error);
        if (header is null)
        {
            return false;
        }

        using var payload = ParsePart(parts[1], "payload", out error);
        if (payload is null)
        {
            return false;
        }

        var alg = header.RootElement.TryGetProperty("alg", out var a) && a.ValueKind == JsonValueKind.String
            ? a.GetString()
            : null;
        jwt = new DecodedJwt(
            Pretty(header.RootElement),
            Pretty(payload.RootElement),
            parts[2],
            alg,
            Time(payload.RootElement, "iat"),
            Time(payload.RootElement, "nbf"),
            Time(payload.RootElement, "exp"));
        return true;
    }

    /// <summary>Reports whether a decoded token is expired or not yet valid at a given time.</summary>
    /// <param name="jwt">The decoded token.</param>
    /// <param name="now">The reference time.</param>
    /// <returns>The status.</returns>
    public static JwtStatus GetStatus(DecodedJwt jwt, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(jwt);
        if (jwt.ExpiresAt is { } exp && exp <= now)
        {
            return JwtStatus.Expired;
        }

        if (jwt.NotBefore is { } nbf && nbf > now)
        {
            return JwtStatus.NotYetValid;
        }

        return jwt.ExpiresAt is null ? JwtStatus.NoExpiry : JwtStatus.Valid;
    }

    static JsonDocument? ParsePart(string part, string name, out string? error)
    {
        error = null;
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(Base64Url.Decode(part));
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            error = $"{name}: not valid Base64URL-encoded JSON";
            return null;
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            doc.Dispose();
            error = $"{name}: not a JSON object";
            return null;
        }

        return doc;
    }

    static DateTimeOffset? Time(JsonElement root, string claim)
    {
        if (root.TryGetProperty(claim, out var v)
            && v.ValueKind == JsonValueKind.Number
            && v.TryGetInt64(out var seconds)
            && seconds is >= -62135596800 and <= 253402300799)
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return null;
    }

    static string Pretty(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            element.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
