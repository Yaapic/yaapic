using System.Text;
using Yaapic.Core.Auth.Aws;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class AwsSigV4Tests
{
    const string Key = "AKIDEXAMPLE";
    const string Secret = "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY";
    static readonly DateTimeOffset Time = new(2015, 8, 30, 12, 36, 0, TimeSpan.Zero);

    static AwsSignature Sign(string method, string url, string payloadHash = AwsSigV4.EmptyPayloadHash,
        IReadOnlyList<KeyValueEntry>? headers = null, string? token = null, string service = "service") =>
        AwsSigV4.Sign(new AwsSigningInput(method, new Uri(url), headers ?? [], payloadHash, Key, Secret, token,
            "us-east-1", service, Time));

    static string Expected(string signedHeaders, string signature) =>
        $"AWS4-HMAC-SHA256 Credential={Key}/20150830/us-east-1/service/aws4_request, SignedHeaders={signedHeaders}, Signature={signature}";

    [Fact]
    public void EmptyPayloadHashMatchesHashOfNothing() =>
        Assert.Equal(AwsSigV4.EmptyPayloadHash, AwsSigV4.HashPayload([]));

    [Fact]
    public void GetVanilla() =>
        Assert.Equal(
            Expected("host;x-amz-date", "5fa00fa31553b73ebf1942676e86291e8372ff2a2260956d9b8aae1d763fbf31"),
            Sign("GET", "https://example.amazonaws.com/").Authorization);

    [Fact]
    public void GetVanillaQueryOrderKeyCase() =>
        Assert.Equal(
            Expected("host;x-amz-date", "b97d918cfa904a5beff61c982a1b6f458b799221646efd99d3219ec94cdf2500"),
            Sign("GET", "https://example.amazonaws.com/?Param2=value2&Param1=value1").Authorization);

    [Fact]
    public void PostVanillaQuery() =>
        Assert.Equal(
            Expected("host;x-amz-date", "28038455d6de14eafc1f9222cf5aa6f1a96197d7deb8263271d420d138af7f11"),
            Sign("POST", "https://example.amazonaws.com/?Param1=value1").Authorization);

    [Fact]
    public void PostFormUrlEncoded()
    {
        var hash = AwsSigV4.HashPayload(Encoding.UTF8.GetBytes("Param1=value1"));
        var sig = Sign("POST", "https://example.amazonaws.com/", hash,
            [new KeyValueEntry("Content-Type", "application/x-www-form-urlencoded")]);
        Assert.Equal(
            Expected("content-type;host;x-amz-date", "ff11897932ad3f4e8b18135d722051e5ac45fc38421b1da7b9d196a0fe09473a"),
            sig.Authorization);
    }

    [Fact]
    public void HeaderValuesAreTrimmedAndCollapsed()
    {
        var a = Sign("GET", "https://example.amazonaws.com/", headers: [new KeyValueEntry("content-type", "  text/plain   ")]);
        var b = Sign("GET", "https://example.amazonaws.com/", headers: [new KeyValueEntry("Content-Type", "text/plain")]);
        Assert.Equal(a.Authorization, b.Authorization);
    }

    [Fact]
    public void UnrelatedAndDisabledHeadersAreNotSigned()
    {
        var sig = Sign("GET", "https://example.amazonaws.com/", headers:
        [
            new KeyValueEntry("X-Other", "1"),
            new KeyValueEntry("Content-Type", "a/b", Enabled: false),
        ]);
        Assert.Contains("SignedHeaders=host;x-amz-date,", sig.Authorization);
    }

    [Fact]
    public void ContentSha256HeaderIsSigned()
    {
        var sig = Sign("GET", "https://example.amazonaws.com/",
            headers: [new KeyValueEntry("x-amz-content-sha256", AwsSigV4.EmptyPayloadHash)]);
        Assert.Contains("SignedHeaders=host;x-amz-content-sha256;x-amz-date,", sig.Authorization);
    }

    [Fact]
    public void SessionTokenIsSignedAndReturned()
    {
        var sig = Sign("GET", "https://example.amazonaws.com/", token: "tok");
        Assert.Contains("SignedHeaders=host;x-amz-date;x-amz-security-token,", sig.Authorization);
        Assert.Equal("20150830T123600Z", sig.AmzDate);
        Assert.Collection(sig.ExtraHeaders,
            h => Assert.Equal(("x-amz-date", "20150830T123600Z"), (h.Key, h.Value)),
            h => Assert.Equal(("x-amz-security-token", "tok"), (h.Key, h.Value)));
    }

    [Fact]
    public void NoTokenMeansOnlyDateHeader() =>
        Assert.Single(Sign("GET", "https://example.amazonaws.com/").ExtraHeaders);

    [Fact]
    public void NonDefaultPortChangesSignatureDefaultPortDoesNot()
    {
        var plain = Sign("GET", "https://example.amazonaws.com/").Authorization;
        Assert.Equal(plain, Sign("GET", "https://example.amazonaws.com:443/").Authorization);
        Assert.NotEqual(plain, Sign("GET", "https://example.amazonaws.com:8443/").Authorization);
    }

    [Fact]
    public void TimeIsConvertedToUtc()
    {
        var local = new DateTimeOffset(2015, 8, 30, 14, 36, 0, TimeSpan.FromHours(2));
        var sig = AwsSigV4.Sign(new AwsSigningInput("GET", new Uri("https://example.amazonaws.com/"), [],
            AwsSigV4.EmptyPayloadHash, Key, Secret, null, "us-east-1", "service", local));
        Assert.Equal(Sign("GET", "https://example.amazonaws.com/").Authorization, sig.Authorization);
    }

    [Fact]
    public void NonAsciiPathIsEncodedSameWayWhetherOrNotAlreadyEscaped()
    {
        var raw = Sign("GET", "https://example.amazonaws.com/café").Authorization;
        var escaped = Sign("GET", "https://example.amazonaws.com/caf%C3%A9").Authorization;
        Assert.Equal(raw, escaped);
    }

    [Fact]
    public void S3DoesNotDoubleEncodeThePath()
    {
        var s3 = Sign("GET", "https://example.amazonaws.com/a%20b", service: "s3").Authorization;
        var other = Sign("GET", "https://example.amazonaws.com/a%20b", service: "service").Authorization;
        Assert.NotEqual(s3, other);
        // Same path written differently for s3 must still match: encoded once either way.
        Assert.Equal(s3, Sign("GET", "https://example.amazonaws.com/a b", service: "s3").Authorization);
    }
}
