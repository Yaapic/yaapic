using System.Globalization;
using System.Text.Json;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Auth.OAuth2;

/// <summary>The outcome of parsing a token endpoint response.</summary>
/// <param name="Token">The token, or null on failure.</param>
/// <param name="Error">The error message, or null on success.</param>
public sealed record OAuth2TokenParseResult(OAuth2Token? Token, string? Error);

/// <summary>Parses token endpoint responses.</summary>
public static class OAuth2TokenResponses
{
    /// <summary>Parses a token endpoint response.</summary>
    /// <param name="response">The response.</param>
    /// <param name="now">The current instant, used for <see cref="OAuth2Token.ObtainedAt"/> and the expiry.</param>
    /// <returns>The token, or an error message that never echoes the body beyond <c>error</c> and <c>error_description</c>.</returns>
    public static OAuth2TokenParseResult Parse(HttpResponseData response, DateTimeOffset now)
    {
        var invalid = new OAuth2TokenParseResult(null, Strings.Oauth2InvalidResponse(response.StatusCode));
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(response.Body);
        }
        catch (JsonException)
        {
            return invalid;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return invalid;
            }

            if (Str(root, "error") is { Length: > 0 } error)
            {
                return new(null, Str(root, "error_description") is { Length: > 0 } d ? $"{error}: {d}" : error);
            }

            if (!response.IsSuccessStatusCode)
            {
                return invalid;
            }

            if (Str(root, "access_token") is not { Length: > 0 } access)
            {
                return invalid;
            }

            DateTimeOffset? expires = null;
            if (root.TryGetProperty("expires_in", out var e)
                && (e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out var sec)
                    || e.ValueKind == JsonValueKind.String && double.TryParse(e.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out sec)))
            {
                expires = now.AddSeconds(sec);
            }

            var type = Str(root, "token_type") is { Length: > 0 } t ? t : "Bearer";
            return new(new OAuth2Token(access, type, Str(root, "refresh_token"), Str(root, "scope"), expires, now), null);
        }
    }

    private static string? Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
