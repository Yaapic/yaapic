using Yaapic.Core.Auth;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Yaapic.Core.Auth.Jwt;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class JwtAuthMethodTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);
    private static readonly long NowSeconds = Now.ToUnixTimeSeconds();
    private readonly JwtAuthMethod _method = new(new FakeTimeProvider(Now));

    private static ResolvedRequest Request(params KeyValueEntry[] headers) =>
        new(HttpMethodKind.Get, "https://example.test/a?x=1", headers, null, null, []);

    private static JwtSettings Settings(Func<JwtSettings, JwtSettings>? edit = null) =>
        (edit ?? (s => s))(JwtSettings.Default with { Secret = "s3cret", Payload = "{\"sub\":\"me\",\"a\":1}" });

    private async Task<AuthenticationResult> Run(JwtSettings settings, ResolvedRequest? request = null) =>
        await _method.ApplyAsync(request ?? Request(), new ResolvedAuth(AuthKind.Jwt, "", "", null, settings, null), CancellationToken.None);

    private static string Segment(string token, int index) => Encoding.UTF8.GetString(Base64Url.Decode(token.Split('.')[index]));

    private static string TokenFromHeader(AuthenticationResult r) => r.Request!.Headers.Single(h => h.Key == "Authorization").Value.Split(' ')[1];

    [Fact]
    public async Task Header_placement_signs_and_verifies_hs256()
    {
        var token = TokenFromHeader(await Run(Settings()));
        var parts = token.Split('.');

        Assert.Equal("{\"alg\":\"HS256\",\"typ\":\"JWT\"}", Segment(token, 0));
        Assert.Equal($"{{\"sub\":\"me\",\"a\":1,\"iat\":{NowSeconds}}}", Segment(token, 1));
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes("s3cret"), Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]));
        Assert.Equal(expected, Base64Url.Decode(parts[2]));
    }

    [Fact]
    public async Task Query_placement_uses_the_parameter_name_or_token()
    {
        var named = await Run(Settings(s => s with { Placement = JwtPlacement.Query, QueryParameterName = "jwt" }));
        var fallback = await Run(Settings(s => s with { Placement = JwtPlacement.Query, QueryParameterName = " " }));

        Assert.StartsWith("https://example.test/a?x=1&jwt=", named.Request!.Url);
        Assert.StartsWith("https://example.test/a?x=1&token=", fallback.Request!.Url);
        Assert.DoesNotContain(named.Request.Headers, h => h.Key == "Authorization");
    }

    [Fact]
    public async Task Prefix_is_trimmed_and_may_be_empty()
    {
        var custom = await Run(Settings(s => s with { HeaderPrefix = " JWT " }));
        var bare = await Run(Settings(s => s with { HeaderPrefix = "" }));

        Assert.StartsWith("JWT ", custom.Request!.Headers.Single().Value);
        Assert.Equal(3, bare.Request!.Headers.Single().Value.Split('.').Length);
    }

    [Fact]
    public async Task Iat_and_exp_are_added_only_when_enabled_and_absent()
    {
        var both = await Run(Settings(s => s with { ExpiresInSeconds = 60 }));
        var none = await Run(Settings(s => s with { AddIssuedAt = false }));
        var present = await Run(Settings(s => s with { Payload = "{\"exp\":5,\"iat\":4}", ExpiresInSeconds = 60 }));

        Assert.Equal($"{{\"sub\":\"me\",\"a\":1,\"iat\":{NowSeconds},\"exp\":{NowSeconds + 60}}}", Segment(TokenFromHeader(both), 1));
        Assert.Equal("{\"sub\":\"me\",\"a\":1}", Segment(TokenFromHeader(none), 1));
        Assert.Equal("{\"exp\":5,\"iat\":4}", Segment(TokenFromHeader(present), 1));
    }

    [Fact]
    public async Task Empty_payload_counts_as_an_empty_object()
    {
        var result = await Run(Settings(s => s with { Payload = "  ", AddIssuedAt = false }));

        Assert.Equal("{}", Segment(TokenFromHeader(result), 1));
    }

    [Fact]
    public async Task Extra_headers_are_appended_and_alg_typ_ignored()
    {
        var result = await Run(Settings(s => s with { ExtraHeaders = "{\"kid\":\"k1\",\"alg\":\"none\",\"typ\":\"x\"}" }));

        Assert.Equal("{\"alg\":\"HS256\",\"typ\":\"JWT\",\"kid\":\"k1\"}", Segment(TokenFromHeader(result), 0));
    }

    [Theory]
    [InlineData("[1]")]
    [InlineData("nope")]
    [InlineData("\"text\"")]
    public async Task Invalid_payload_fails(string payload)
    {
        var result = await Run(Settings(s => s with { Payload = payload }));

        Assert.Null(result.Request);
        Assert.Equal(Yaapic.Localization.Strings.JwtPayloadInvalid, result.ErrorMessage);
    }

    [Theory]
    [InlineData("[1]")]
    [InlineData("{bad")]
    public async Task Invalid_extra_headers_fail(string headers)
    {
        var result = await Run(Settings(s => s with { ExtraHeaders = headers }));

        Assert.Equal(Yaapic.Localization.Strings.JwtHeadersInvalid, result.ErrorMessage);
    }

    [Fact]
    public async Task Missing_key_fails_without_echoing_anything()
    {
        var hs = await Run(Settings(s => s with { Secret = "" }));
        var rs = await Run(Settings(s => s with { Algorithm = JwtAlgorithm.Rs256, PrivateKey = " " }));

        Assert.Equal(Yaapic.Localization.Strings.JwtKeyMissing, hs.ErrorMessage);
        Assert.Equal(Yaapic.Localization.Strings.JwtKeyMissing, rs.ErrorMessage);
    }

    [Fact]
    public async Task Bad_base64_secret_fails_and_good_one_signs()
    {
        var bad = await Run(Settings(s => s with { Secret = "not base64!", SecretIsBase64 = true }));
        var good = await Run(Settings(s => s with { Secret = Convert.ToBase64String([1, 2, 3]), SecretIsBase64 = true }));

        Assert.Equal(Yaapic.Localization.Strings.JwtSecretInvalid, bad.ErrorMessage);
        Assert.NotNull(good.Request);
    }

    [Fact]
    public async Task Invalid_pem_fails_without_leaking_the_key()
    {
        var result = await Run(Settings(s => s with { Algorithm = JwtAlgorithm.Es256, PrivateKey = "garbage-key-material" }));

        Assert.Null(result.Request);
        Assert.DoesNotContain("garbage-key-material", result.ErrorMessage);
    }

    [Fact]
    public async Task User_authorization_header_wins()
    {
        var request = Request(new KeyValueEntry("authorization", "Basic x"));

        var result = await Run(Settings(), request);

        Assert.Equal("Basic x", result.Request!.Headers.Single().Value);
    }

    [Fact]
    public async Task Es256_token_verifies_with_the_public_key()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var settings = Settings(s => s with { Algorithm = JwtAlgorithm.Es256, PrivateKey = key.ExportPkcs8PrivateKeyPem() });

        var token = TokenFromHeader(await Run(settings));

        var last = token.LastIndexOf('.');
        Assert.True(key.VerifyData(Encoding.ASCII.GetBytes(token[..last]), Base64Url.Decode(token[(last + 1)..]),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Fact]
    public async Task Null_jwt_settings_use_the_defaults_and_fail_on_the_missing_secret()
    {
        var result = await _method.ApplyAsync(Request(), new ResolvedAuth(AuthKind.Jwt, "", "", null, null, null), CancellationToken.None);

        Assert.Equal(Yaapic.Localization.Strings.JwtKeyMissing, result.ErrorMessage);
        Assert.Equal(AuthKind.Jwt, _method.Kind);
    }
}
