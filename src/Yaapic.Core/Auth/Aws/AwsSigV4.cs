using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Auth.Aws;

/// <summary>Computes AWS Signature Version 4 request signatures (headers form only).</summary>
public static class AwsSigV4
{
    /// <summary>The payload hash of an empty body.</summary>
    public const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>The payload hash marker for bodies that are not hashed.</summary>
    public const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    private const string Algorithm = "AWS4-HMAC-SHA256";

    /// <summary>Hashes a payload.</summary>
    /// <param name="payload">The body bytes.</param>
    /// <returns>The lowercase hex SHA-256.</returns>
    public static string HashPayload(ReadOnlySpan<byte> payload) =>
        Convert.ToHexStringLower(SHA256.HashData(payload));

    /// <summary>Signs a request.</summary>
    /// <param name="input">The request description and credentials.</param>
    /// <returns>The <c>Authorization</c> header value and the headers to add.</returns>
    public static AwsSignature Sign(AwsSigningInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var utc = input.Time.UtcDateTime;
        var amzDate = utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var date = amzDate[..8];

        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = input.Url.Authority,
            ["x-amz-date"] = amzDate,
        };
        if (!string.IsNullOrEmpty(input.SessionToken))
        {
            headers["x-amz-security-token"] = input.SessionToken;
        }

        foreach (var h in input.Headers)
        {
            var name = h.Key.Trim().ToLowerInvariant();
            if (h.Enabled && name is "content-type" or "x-amz-content-sha256")
            {
                headers[name] = CollapseSpaces(h.Value);
            }
        }

        var signedHeaders = string.Join(';', headers.Keys);
        var canonicalHeaders = new StringBuilder();
        foreach (var (k, v) in headers)
        {
            canonicalHeaders.Append(k).Append(':').Append(v).Append('\n');
        }

        var canonicalRequest = string.Join(
            '\n',
            input.Method.ToUpperInvariant(),
            CanonicalPath(input.Url, input.Service),
            CanonicalQuery(input.Url),
            canonicalHeaders.ToString(),
            signedHeaders,
            input.PayloadHash);

        var scope = $"{date}/{input.Region}/{input.Service}/aws4_request";
        var stringToSign = $"{Algorithm}\n{amzDate}\n{scope}\n" +
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));

        var key = Hmac(Encoding.UTF8.GetBytes("AWS4" + input.SecretAccessKey), date);
        key = Hmac(key, input.Region);
        key = Hmac(key, input.Service);
        key = Hmac(key, "aws4_request");
        var signature = Convert.ToHexStringLower(Hmac(key, stringToSign));

        var authorization =
            $"{Algorithm} Credential={input.AccessKeyId}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}";

        var extra = new List<KeyValueEntry> { new("x-amz-date", amzDate) };
        if (headers.TryGetValue("x-amz-security-token", out var token))
        {
            extra.Add(new KeyValueEntry("x-amz-security-token", token));
        }

        return new AwsSignature(authorization, amzDate, extra);
    }

    private static byte[] Hmac(byte[] key, string data) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static string CollapseSpaces(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    // Wire form: each segment percent-encoded once. Every service but s3 encodes that again.
    private static string CanonicalPath(Uri url, string service)
    {
        var path = url.AbsolutePath;
        if (path.Length == 0)
        {
            return "/";
        }

        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var once = Encode(Uri.UnescapeDataString(segments[i]));
            segments[i] = service == "s3" ? once : Encode(once);
        }

        return string.Join('/', segments);
    }

    private static string CanonicalQuery(Uri url)
    {
        var query = url.Query;
        if (query.Length <= 1)
        {
            return string.Empty;
        }

        var pairs = new List<(string Key, string Value)>();
        foreach (var part in query[1..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            var k = eq < 0 ? part : part[..eq];
            var v = eq < 0 ? string.Empty : part[(eq + 1)..];
            pairs.Add((Encode(Uri.UnescapeDataString(k)), Encode(Uri.UnescapeDataString(v))));
        }

        pairs.Sort(static (a, b) =>
        {
            var c = string.CompareOrdinal(a.Key, b.Key);
            return c != 0 ? c : string.CompareOrdinal(a.Value, b.Value);
        });
        return string.Join('&', pairs.Select(static p => $"{p.Key}={p.Value}"));
    }

    private static string Encode(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (b < 128 && (char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~'))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return sb.ToString();
    }
}
