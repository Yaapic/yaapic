using System.Text;
using Yaapic.Core.Auth.Jwt;

namespace Yaapic.Core.Tests;

public class JwtDecoderTests
{
    const string Rfc7515 =
        "eyJ0eXAiOiJKV1QiLA0KICJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJqb2UiLA0KICJleHAiOjEzMDA4MTkzODAsDQogImh0dHA6Ly9leGFtcGxlLmNvbS9pc19yb290Ijp0cnVlfQ.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

    static string Part(string json) => Base64Url.Encode(Encoding.UTF8.GetBytes(json));

    static string Token(string header, string payload, string sig = "sig") => $"{Part(header)}.{Part(payload)}.{sig}";

    static DecodedJwt Decode(string token)
    {
        Assert.True(JwtDecoder.TryDecode(token, out var jwt, out var error), error);
        return jwt!;
    }

    [Fact]
    public void Rfc7515_a1_token_decodes()
    {
        var jwt = Decode(Rfc7515);
        Assert.Equal("{\n  \"typ\": \"JWT\",\n  \"alg\": \"HS256\"\n}", jwt.HeaderJson);
        Assert.Contains("\"iss\": \"joe\"", jwt.PayloadJson);
        Assert.Equal("HS256", jwt.Algorithm);
        Assert.Equal("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk", jwt.Signature);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1300819380), jwt.ExpiresAt);
    }

    [Theory]
    [InlineData("  {0}\n")]
    [InlineData("Bearer {0}")]
    [InlineData("  bEaReR   {0} ")]
    public void Prefix_and_whitespace_are_trimmed(string format) =>
        Assert.Equal("HS256", Decode(string.Format(format, Rfc7515)).Algorithm);

    [Theory]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    [InlineData("!!!.e30.sig")]
    [InlineData("")]
    public void Malformed_tokens_fail(string token)
    {
        Assert.False(JwtDecoder.TryDecode(token, out var jwt, out var error));
        Assert.Null(jwt);
        Assert.NotNull(error);
    }

    [Fact]
    public void Non_object_and_non_json_parts_fail()
    {
        Assert.False(JwtDecoder.TryDecode(Token("{}", "[1]"), out _, out _));
        Assert.False(JwtDecoder.TryDecode(Token("{}", "\"x\""), out _, out _));
        Assert.False(JwtDecoder.TryDecode(Token("not json", "{}"), out _, out _));
        Assert.False(JwtDecoder.TryDecode(Token("[]", "{}"), out _, out _));
    }

    [Fact]
    public void Claims_are_parsed_and_non_integers_ignored()
    {
        var jwt = Decode(Token("{\"alg\":\"none\"}", "{\"iat\":10,\"nbf\":20,\"exp\":30}"));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(10), jwt.IssuedAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(20), jwt.NotBefore);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(30), jwt.ExpiresAt);

        var ignored = Decode(Token("{\"alg\":1}", "{\"iat\":\"10\",\"nbf\":2.5,\"exp\":null}"));
        Assert.Null(ignored.Algorithm);
        Assert.Null(ignored.IssuedAt);
        Assert.Null(ignored.NotBefore);
        Assert.Null(ignored.ExpiresAt);
    }

    [Fact]
    public void Unsigned_token_is_allowed()
    {
        var jwt = Decode(Token("{\"alg\":\"none\"}", "{}", ""));
        Assert.Equal("", jwt.Signature);
    }

    [Fact]
    public void Property_order_is_kept()
    {
        var jwt = Decode(Token("{\"z\":1,\"a\":2}", "{\"b\":1,\"a\":{\"y\":1,\"x\":2}}"));
        Assert.True(jwt.HeaderJson.IndexOf("\"z\"", StringComparison.Ordinal) < jwt.HeaderJson.IndexOf("\"a\"", StringComparison.Ordinal));
        Assert.True(jwt.PayloadJson.IndexOf("\"y\"", StringComparison.Ordinal) < jwt.PayloadJson.IndexOf("\"x\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Status_covers_every_case()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1000);
        JwtStatus Status(string payload) => JwtDecoder.GetStatus(Decode(Token("{}", payload)), now);

        Assert.Equal(JwtStatus.Valid, Status("{\"exp\":2000}"));
        Assert.Equal(JwtStatus.Expired, Status("{\"exp\":1000}"));
        Assert.Equal(JwtStatus.Expired, Status("{\"exp\":5,\"nbf\":2000}"));
        Assert.Equal(JwtStatus.NotYetValid, Status("{\"exp\":3000,\"nbf\":1001}"));
        Assert.Equal(JwtStatus.Valid, Status("{\"exp\":3000,\"nbf\":1000}"));
        Assert.Equal(JwtStatus.NoExpiry, Status("{}"));
        Assert.Equal(JwtStatus.NotYetValid, Status("{\"nbf\":2000}"));
    }
}
