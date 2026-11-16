using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Auth.Jwt;

/// <summary>Sends a freshly signed JWT in the Authorization header or the query string.</summary>
/// <param name="time">The clock used for the <c>iat</c> and <c>exp</c> claims.</param>
public sealed class JwtAuthMethod(TimeProvider time) : IAuthMethod
{
    /// <inheritdoc />
    public AuthKind Kind => AuthKind.Jwt;

    /// <inheritdoc />
    public Task<AuthenticationResult> ApplyAsync(ResolvedRequest request, ResolvedAuth auth, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(auth);
        return Task.FromResult(Apply(request, auth.Jwt ?? JwtSettings.Default));
    }

    private AuthenticationResult Apply(ResolvedRequest request, JwtSettings s)
    {
        var payload = BuildPayload(s);
        if (payload is null)
        {
            return AuthenticationResult.Fail(Strings.JwtPayloadInvalid);
        }

        var header = BuildHeader(s);
        if (header is null)
        {
            return AuthenticationResult.Fail(Strings.JwtHeadersInvalid);
        }

        string jwt;
        try
        {
            if (s.Algorithm is JwtAlgorithm.Hs256 or JwtAlgorithm.Hs384 or JwtAlgorithm.Hs512)
            {
                if (s.Secret.Length == 0)
                {
                    return AuthenticationResult.Fail(Strings.JwtKeyMissing);
                }

                byte[] key;
                try
                {
                    key = s.SecretIsBase64 ? Convert.FromBase64String(s.Secret) : Encoding.UTF8.GetBytes(s.Secret);
                }
                catch (FormatException)
                {
                    return AuthenticationResult.Fail(Strings.JwtSecretInvalid);
                }

                jwt = JwtSigner.Sign(header, payload, s.Algorithm, key);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(s.PrivateKey))
                {
                    return AuthenticationResult.Fail(Strings.JwtKeyMissing);
                }

                jwt = JwtSigner.Sign(header, payload, s.Algorithm, s.PrivateKey);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or CryptographicException)
        {
            return AuthenticationResult.Fail(ex.Message);
        }

        if (s.Placement == JwtPlacement.Query)
        {
            var name = string.IsNullOrWhiteSpace(s.QueryParameterName) ? "token" : s.QueryParameterName;
            return AuthenticationResult.Success(request.WithQueryParameter(name, jwt));
        }

        var prefix = s.HeaderPrefix?.Trim() ?? "";
        return AuthenticationResult.Success(request.WithHeaderUnlessPresent("Authorization", prefix.Length == 0 ? jwt : $"{prefix} {jwt}"));
    }

    private string? BuildPayload(JwtSettings s)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(s.Payload) ? "{}" : s.Payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var now = time.GetUtcNow().ToUnixTimeSeconds();
            var hasIat = false;
            var hasExp = false;
            var buffer = new ArrayBufferWriter<byte>();
            using (var w = new Utf8JsonWriter(buffer))
            {
                w.WriteStartObject();
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    hasIat |= p.NameEquals("iat");
                    hasExp |= p.NameEquals("exp");
                    p.WriteTo(w);
                }

                if (s.AddIssuedAt && !hasIat)
                {
                    w.WriteNumber("iat", now);
                }

                if (s.ExpiresInSeconds > 0 && !hasExp)
                {
                    w.WriteNumber("exp", now + s.ExpiresInSeconds);
                }

                w.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? BuildHeader(JwtSettings s)
    {
        try
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var w = new Utf8JsonWriter(buffer))
            {
                w.WriteStartObject();
                w.WriteString("alg", s.Algorithm.ToString().ToUpperInvariant());
                w.WriteString("typ", "JWT");
                if (!string.IsNullOrWhiteSpace(s.ExtraHeaders))
                {
                    using var doc = JsonDocument.Parse(s.ExtraHeaders);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        return null;
                    }

                    foreach (var p in doc.RootElement.EnumerateObject().Where(p => !p.NameEquals("alg") && !p.NameEquals("typ")))
                    {
                        p.WriteTo(w);
                    }
                }

                w.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
