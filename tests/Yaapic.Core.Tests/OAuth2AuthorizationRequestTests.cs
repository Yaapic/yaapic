using Yaapic.Core.Auth.OAuth2;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class OAuth2AuthorizationRequestTests
{
    private static OAuth2Settings Settings(string url = "https://auth.example/authorize") =>
        OAuth2Settings.Default with { AuthorizationUrl = url, ClientId = "my client", RedirectUri = "http://127.0.0.1:8742/callback" };

    [Fact]
    public void Rfc7636VectorMatches() =>
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            Pkce.FromVerifier("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk").Challenge);

    [Fact]
    public void CreateGivesUnpaddedUrlSafeVerifier()
    {
        var p = Pkce.Create();
        Assert.Equal(43, p.Verifier.Length);
        Assert.All(p.Verifier, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        Assert.Equal(Pkce.FromVerifier(p.Verifier).Challenge, p.Challenge);
        Assert.NotEqual(p.Verifier, Pkce.Create().Verifier);
    }

    [Fact]
    public void NewStateIs22UrlSafeChars()
    {
        var s = OAuth2AuthorizationRequest.NewState();
        Assert.Equal(22, s.Length);
        Assert.DoesNotContain('=', s);
    }

    [Fact]
    public void BuildUrlContainsEveryParameterEncoded()
    {
        var url = OAuth2AuthorizationRequest.BuildUrl(Settings() with { Scope = "a b", Audience = "api/x" }, "st", "ch").AbsoluteUri;
        Assert.StartsWith("https://auth.example/authorize?response_type=code&client_id=my%20client", url);
        Assert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A8742%2Fcallback", url);
        Assert.Contains("&state=st&code_challenge=ch&code_challenge_method=S256", url);
        Assert.Contains("&scope=a%20b&audience=api%2Fx", url);
    }

    [Fact]
    public void BuildUrlKeepsExistingQueryAndOmitsEmptyOptionals()
    {
        var url = OAuth2AuthorizationRequest.BuildUrl(Settings("https://a/authorize?tenant=t"), "s", "c").AbsoluteUri;
        Assert.StartsWith("https://a/authorize?tenant=t&response_type=code", url);
        Assert.DoesNotContain("scope=", url);
        Assert.DoesNotContain("audience=", url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://a/b")]
    public void BuildUrlRejectsInvalidUrl(string url) =>
        Assert.Throws<ArgumentException>(() => OAuth2AuthorizationRequest.BuildUrl(Settings(url), "s", "c"));

    [Fact]
    public void ParseCallbackReadsSuccess() =>
        Assert.Equal(new("abc", null, null, "xyz"), OAuth2AuthorizationRequest.ParseCallback("/callback?code=abc&state=xyz"));

    [Fact]
    public void ParseCallbackReadsErrorDecodedAndFirstWins() =>
        Assert.Equal(new("1", "access_denied", "User said no", null),
            OAuth2AuthorizationRequest.ParseCallback("/cb?error=access_denied&error_description=User%20said+no&code=1&code=2"));

    [Fact]
    public void ParseCallbackWithoutQueryIsEmpty() =>
        Assert.Equal(new(null, null, null, null), OAuth2AuthorizationRequest.ParseCallback("/callback"));

    [Theory]
    [InlineData("http://127.0.0.1:8742/callback", 8742, "/callback")]
    [InlineData("http://localhost:9000/", 9000, "/")]
    public void ListenAddressAccepted(string uri, int port, string path)
    {
        Assert.True(OAuth2AuthorizationRequest.TryGetListenAddress(uri, out var p, out var pa));
        Assert.Equal((port, path), (p, pa));
    }

    [Theory]
    [InlineData("https://127.0.0.1:8742/cb")]
    [InlineData("http://example.com:8742/cb")]
    [InlineData("http://127.0.0.1/cb")]
    [InlineData("http://127.0.0.1:0/cb")]
    [InlineData("nonsense")]
    public void ListenAddressRejected(string uri) =>
        Assert.False(OAuth2AuthorizationRequest.TryGetListenAddress(uri, out _, out _));
}
