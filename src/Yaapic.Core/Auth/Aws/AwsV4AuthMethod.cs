using System.Security.Cryptography;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Auth.Aws;

/// <summary>Signs a request with AWS Signature Version 4 (headers form).</summary>
/// <param name="time">The clock used as the signing time.</param>
public sealed class AwsV4AuthMethod(TimeProvider time) : IAuthMethod
{
    private const string ContentHashHeader = "x-amz-content-sha256";

    /// <inheritdoc />
    public AuthKind Kind => AuthKind.AwsV4;

    /// <inheritdoc />
    public async Task<AuthenticationResult> ApplyAsync(ResolvedRequest request, ResolvedAuth auth, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(auth);
        var s = auth.AwsV4 ?? AwsV4Settings.Default;

        var missing = MissingField(s);
        if (missing is not null)
        {
            return AuthenticationResult.Fail(
                s is { AccessKeyId.Length: 0, SecretAccessKey.Length: 0, Region.Length: 0, Service.Length: 0 }
                    ? Strings.AwsSettingsMissing
                    : Strings.AwsFieldMissing(missing));
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return AuthenticationResult.Fail(Strings.AwsUrlInvalid);
        }

        if (request.Headers.Any(h => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)))
        {
            return AuthenticationResult.Success(request);
        }

        string hash;
        try
        {
            hash = await PayloadHashAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AuthenticationResult.Fail(Strings.AwsPayloadUnreadable(request.BodyFilePath!));
        }

        var headers = request.Headers
            .Where(h => !h.Key.Equals(ContentHashHeader, StringComparison.OrdinalIgnoreCase))
            .Append(new KeyValueEntry(ContentHashHeader, hash))
            .ToList();
        var signature = AwsSigV4.Sign(new AwsSigningInput(
            request.Method.ToText(),
            uri,
            headers,
            hash,
            s.AccessKeyId,
            s.SecretAccessKey,
            s.SessionToken,
            s.Region,
            s.Service,
            time.GetUtcNow()));
        headers.Add(new KeyValueEntry("Authorization", signature.Authorization));
        headers.AddRange(signature.ExtraHeaders);
        return AuthenticationResult.Success(request with { Headers = headers });
    }

    private static string? MissingField(AwsV4Settings s) =>
        s.AccessKeyId.Length == 0 ? "access key ID"
        : s.SecretAccessKey.Length == 0 ? "secret access key"
        : s.Region.Length == 0 ? "region"
        : s.Service.Length == 0 ? "service"
        : null;

    private static async Task<string> PayloadHashAsync(ResolvedRequest request, CancellationToken ct)
    {
        if (request.BodyText is not null)
        {
            return AwsSigV4.HashPayload(System.Text.Encoding.UTF8.GetBytes(request.BodyText));
        }

        if (request.BodyFilePath is not null)
        {
            await using var stream = File.OpenRead(request.BodyFilePath);
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false));
        }

        return request.Parts is not null ? AwsSigV4.UnsignedPayload : AwsSigV4.EmptyPayloadHash;
    }
}
