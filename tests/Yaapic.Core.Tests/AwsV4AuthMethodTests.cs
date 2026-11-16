using Yaapic.Core.Auth;
using System.Text;
using Yaapic.Core.Auth.Aws;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class AwsV4AuthMethodTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);
    private readonly AwsV4AuthMethod _method = new(new FakeTimeProvider(Now));

    private static AwsV4Settings Settings(Func<AwsV4Settings, AwsV4Settings>? edit = null) =>
        (edit ?? (s => s))(new("AKID", "secret", "", "us-east-1", "s3"));

    private static ResolvedRequest Request(HttpMethodKind method = HttpMethodKind.Get, string url = "https://example.test/a?x=1",
        string? body = null, string? file = null, IReadOnlyList<ResolvedPart>? parts = null, params KeyValueEntry[] headers) =>
        new(method, url, headers, body, null, [], parts, file);

    private async Task<AuthenticationResult> Run(ResolvedRequest request, AwsV4Settings? settings = null) =>
        await _method.ApplyAsync(request, new ResolvedAuth(AuthKind.AwsV4, "", "", null, null, settings ?? Settings()), CancellationToken.None);

    private static string Header(AuthenticationResult r, string name) => r.Request!.Headers.Single(h => h.Key == name).Value;

    private static string Direct(ResolvedRequest r, string hash, AwsV4Settings s) =>
        AwsSigV4.Sign(new AwsSigningInput(r.Method.ToText(), new Uri(r.Url), [new KeyValueEntry("x-amz-content-sha256", hash)], hash,
            s.AccessKeyId, s.SecretAccessKey, s.SessionToken, s.Region, s.Service, Now)).Authorization;

    [Fact]
    public async Task Get_without_body_uses_the_empty_hash_and_matches_the_signer()
    {
        var request = Request();
        var result = await Run(request);

        Assert.Equal(AwsSigV4.EmptyPayloadHash, Header(result, "x-amz-content-sha256"));
        Assert.Equal("20261112T080000Z", Header(result, "x-amz-date"));
        Assert.Equal(Direct(request, AwsSigV4.EmptyPayloadHash, Settings()), Header(result, "Authorization"));
    }

    [Fact]
    public async Task Text_body_is_hashed_as_utf8()
    {
        var request = Request(HttpMethodKind.Post, body: "héllo");
        var hash = AwsSigV4.HashPayload(Encoding.UTF8.GetBytes("héllo"));
        var result = await Run(request);

        Assert.Equal(hash, Header(result, "x-amz-content-sha256"));
        Assert.Equal(Direct(request, hash, Settings()), Header(result, "Authorization"));
    }

    [Fact]
    public async Task File_body_is_hashed_from_the_file()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, [1, 2, 3], TestContext.Current.CancellationToken);
            var result = await Run(Request(HttpMethodKind.Put, file: path));

            Assert.Equal(AwsSigV4.HashPayload([1, 2, 3]), Header(result, "x-amz-content-sha256"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Unreadable_file_fails_with_the_path()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.bin");
        var result = await Run(Request(HttpMethodKind.Put, file: path));

        Assert.Null(result.Request);
        Assert.Contains(path, result.ErrorMessage);
    }

    [Fact]
    public async Task Multipart_is_unsigned_payload()
    {
        var result = await Run(Request(HttpMethodKind.Post, parts: [new ResolvedPart("a", "b", null)]));

        Assert.Equal("UNSIGNED-PAYLOAD", Header(result, "x-amz-content-sha256"));
    }

    [Fact]
    public async Task Session_token_is_added()
    {
        var result = await Run(Request(), Settings(s => s with { SessionToken = "tok" }));

        Assert.Equal("tok", Header(result, "x-amz-security-token"));
        Assert.Contains("x-amz-security-token", Header(result, "Authorization"));
    }

    [Fact]
    public async Task User_content_hash_header_is_replaced()
    {
        var result = await Run(Request(headers: new KeyValueEntry("X-Amz-Content-Sha256", "wrong")));

        Assert.Equal(AwsSigV4.EmptyPayloadHash, Header(result, "x-amz-content-sha256"));
        Assert.DoesNotContain(result.Request!.Headers, h => h.Value == "wrong");
    }

    [Theory]
    [InlineData("access key ID")]
    [InlineData("secret access key")]
    [InlineData("region")]
    [InlineData("service")]
    public async Task Each_missing_field_is_named(string field)
    {
        var result = await Run(Request(), Settings(s => field switch
        {
            "access key ID" => s with { AccessKeyId = "" },
            "secret access key" => s with { SecretAccessKey = "" },
            "region" => s with { Region = "" },
            _ => s with { Service = "" },
        }));

        Assert.Null(result.Request);
        Assert.Contains(field, result.ErrorMessage);
    }

    [Fact]
    public async Task Empty_settings_fail_with_the_general_message()
    {
        var result = await Run(Request(), AwsV4Settings.Default);

        Assert.Null(result.Request);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Relative_or_non_http_url_fails()
    {
        Assert.Null((await Run(Request(url: "/relative"))).Request);
        Assert.Null((await Run(Request(url: "ftp://example.test/a"))).Request);
    }

    [Fact]
    public async Task User_authorization_header_wins()
    {
        var request = Request(headers: new KeyValueEntry("authorization", "mine"));
        var result = await Run(request);

        Assert.Same(request, result.Request);
    }
}
