using System.Security.Cryptography;
using System.Text;
using Yaapic.Core.Auth.Digest;

namespace Yaapic.Core.Tests;

public class DigestTests
{
    const string Rfc2617 =
        "Digest realm=\"testrealm@host.com\", qop=\"auth,auth-int\", nonce=\"dcd98b7102dd2f0e8b11d0f600bfb0c093\", opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"";

    const string Rfc7616Sha256 =
        "Digest realm=\"http-auth@example.org\", qop=\"auth, auth-int\", algorithm=SHA-256, nonce=\"7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v\", opaque=\"FQhe/qaU925kfnzjCev0ciny7QMkPqMAFRtzCUYo5tdS\"";

    static DigestChallenge Parse(string value)
    {
        Assert.True(DigestChallenge.TryParse(value, out var c));
        return c!;
    }

    static string Md5(string s) => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(s)));

    [Fact]
    public void Rfc2617_example_reproduces_published_response()
    {
        var header = DigestResponse.BuildHeader(Parse(Rfc2617), "GET", "/dir/index.html", "Mufasa", "Circle Of Life", "0a4f113b");
        Assert.Contains("response=\"6629fae49393a05397450978507c4ef1\"", header);
        Assert.Contains("qop=auth, nc=00000001, cnonce=\"0a4f113b\"", header);
        Assert.EndsWith("opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"", header);
        Assert.StartsWith("Digest username=\"Mufasa\", realm=\"testrealm@host.com\"", header);
    }

    [Fact]
    public void Rfc7616_sha256_example_reproduces_published_response()
    {
        var header = DigestResponse.BuildHeader(
            Parse(Rfc7616Sha256), "GET", "/dir/index.html", "Mufasa", "Circle of Life", "f2/wE4q74E6zIJEtWaHKaf5wv/H5QzzpXusqGemxURZJ");
        Assert.Contains("response=\"753927fa0e85d155564e2e272a28d1802ca10daf4496794697cf8db5856cb6c1\"", header);
        Assert.Contains("algorithm=SHA-256", header);
    }

    [Fact]
    public void Md5_sess_matches_manual_computation()
    {
        var c = new DigestChallenge("r", "n", null, "md5-sess", ["auth"], false);
        var header = DigestResponse.BuildHeader(c, "GET", "/", "u", "p", "cn");
        var ha1 = Md5($"{Md5("u:r:p")}:n:cn");
        Assert.Contains($"response=\"{Md5($"{ha1}:n:00000001:cn:auth:{Md5("GET:/")}")}\"", header);
    }

    [Fact]
    public void Sha256_sess_is_supported_and_unknown_algorithm_throws()
    {
        var ok = new DigestChallenge("r", "n", null, "SHA-256-sess", ["auth"], false);
        Assert.Contains("algorithm=SHA-256-sess", DigestResponse.BuildHeader(ok, "GET", "/", "u", "p", "cn"));
        var bad = ok with { Algorithm = "SHA-512-256" };
        Assert.Throws<NotSupportedException>(() => DigestResponse.BuildHeader(bad, "GET", "/", "u", "p", "cn"));
    }

    [Fact]
    public void No_qop_uses_rfc2069_form()
    {
        var c = new DigestChallenge("r", "n", null, "MD5", [], false);
        var header = DigestResponse.BuildHeader(c, "GET", "/", "u", "p", "cn");
        Assert.Contains($"response=\"{Md5($"{Md5("u:r:p")}:n:{Md5("GET:/")}")}\"", header);
        Assert.DoesNotContain("qop", header);
        Assert.DoesNotContain("nc=", header);
        Assert.DoesNotContain("cnonce", header);
        Assert.DoesNotContain("opaque", header);
    }

    [Fact]
    public void Auth_int_only_throws()
    {
        var c = new DigestChallenge("r", "n", null, "MD5", ["auth-int"], false);
        Assert.Throws<NotSupportedException>(() => DigestResponse.BuildHeader(c, "GET", "/", "u", "p", "cn"));
    }

    [Fact]
    public void Header_values_are_escaped()
    {
        var c = new DigestChallenge("r", "n", null, "MD5", [], false);
        Assert.Contains("username=\"a\\\"b\\\\c\"", DigestResponse.BuildHeader(c, "GET", "/", "a\"b\\c", "p", "cn"));
    }

    [Fact]
    public void Parses_fields_with_defaults_and_split_qop()
    {
        var c = Parse(Rfc2617);
        Assert.Equal("testrealm@host.com", c.Realm);
        Assert.Equal("MD5", c.Algorithm);
        Assert.Equal(["auth", "auth-int"], c.Qop);
        Assert.False(c.Stale);
    }

    [Fact]
    public void Parses_quoted_commas_and_escapes()
    {
        var c = Parse("Digest realm=\"a, b \\\"c\\\"\", nonce=\"n\"");
        Assert.Equal("a, b \"c\"", c.Realm);
    }

    [Fact]
    public void Picks_the_digest_scheme_among_several()
    {
        var c = Parse("Basic realm=\"basic\", Digest realm=\"dig\", nonce=\"n\", Bearer realm=\"other\"");
        Assert.Equal("dig", c.Realm);
    }

    [Fact]
    public void Names_are_case_insensitive_and_stale_is_read()
    {
        var c = Parse("digest REALM=\"r\", Nonce=n1, STALE=TRUE, Algorithm=sha-256");
        Assert.Equal("n1", c.Nonce);
        Assert.True(c.Stale);
        Assert.Equal("sha-256", c.Algorithm);
    }

    [Theory]
    [InlineData("Digest nonce=\"n\"")]
    [InlineData("Digest realm=\"r\"")]
    [InlineData("Basic realm=\"r\"")]
    [InlineData("")]
    public void Unusable_challenges_fail(string value)
    {
        Assert.False(DigestChallenge.TryParse(value, out var c));
        Assert.Null(c);
    }
}
