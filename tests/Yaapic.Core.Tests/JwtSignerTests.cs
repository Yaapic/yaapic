using System.Text;
using Yaapic.Core.Auth.Jwt;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class JwtSignerTests
{
    // Vector from RFC 7515 appendix A.1 (header and payload keep their CRLF + space layout).
    [Fact]
    public void Rfc7515_a1_token_is_reproduced()
    {
        var key = Base64Url.Decode("AyM1SysPpbyDfgZld3umj1qzKObwVMkoqQ-EstJQLr_T-1qS0gZH75aKtMN3Yj0iPS4hcgUuTwjAzZr1Z9CAow");
        var token = JwtSigner.Sign(
            "{\"typ\":\"JWT\",\r\n \"alg\":\"HS256\"}",
            "{\"iss\":\"joe\",\r\n \"exp\":1300819380,\r\n \"http://example.com/is_root\":true}",
            JwtAlgorithm.Hs256,
            key);
        Assert.Equal(
            "eyJ0eXAiOiJKV1QiLA0KICJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJqb2UiLA0KICJleHAiOjEzMDA4MTkzODAsDQogImh0dHA6Ly9leGFtcGxlLmNvbS9pc19yb290Ijp0cnVlfQ.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk",
            token);
    }

    // Expected values computed independently with Python: hmac.new(key, signingInput, hashlib.sha384/sha512).
    [Theory]
    [InlineData(JwtAlgorithm.Hs384, "{\"typ\":\"JWT\",\"alg\":\"HS384\"}", "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzM4NCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.tIpRmxNiLCZjR3Sj5hELGB66na4-xS8wS8buhIys4ZzZZ2U2MvSg-O88GCxiUNnU")]
    [InlineData(JwtAlgorithm.Hs512, "{\"typ\":\"JWT\",\"alg\":\"HS512\"}", "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzUxMiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.Nh3MQg3Tu092-bBVIMYpnnEpzgB-s7LRUJxTkTtR2aeCLQ1GXJpwHPXT1k4K9r1dOwaEW4yoCHvrrnBCC5M4ug")]
    public void Hs384_and_hs512_match_independent_vectors(JwtAlgorithm algorithm, string header, string expected)
    {
        var key = Encoding.UTF8.GetBytes("a-string-secret-at-least-256-bits-long");
        Assert.Equal(expected, JwtSigner.Sign(header, "{\"sub\":\"1234567890\"}", algorithm, key));
    }

    [Theory]
    [InlineData(JwtAlgorithm.Rs256)]
    [InlineData(JwtAlgorithm.Es256)]
    public void Non_hmac_algorithms_are_not_supported(JwtAlgorithm algorithm) =>
        Assert.Throws<ArgumentException>(() => JwtSigner.Sign("{}", "{}", algorithm, [1]));
}

public class Base64UrlTests
{
    [Theory]
    [InlineData(new byte[] { }, "")]
    [InlineData(new byte[] { 0xFB, 0xFF }, "-_8")]
    [InlineData(new byte[] { 1, 2, 3, 4 }, "AQIDBA")]
    public void Round_trips_without_padding(byte[] data, string text)
    {
        Assert.Equal(text, Base64Url.Encode(data));
        Assert.Equal(data, Base64Url.Decode(text));
    }

    [Fact]
    public void Decode_accepts_padding() => Assert.Equal(new byte[] { 1, 2, 3, 4 }, Base64Url.Decode("AQIDBA=="));

    [Theory]
    [InlineData("a")]
    [InlineData("ab+d")]
    [InlineData("ab/d")]
    [InlineData("a$cd")]
    public void Decode_rejects_invalid_text(string text) => Assert.Throws<FormatException>(() => Base64Url.Decode(text));
}
