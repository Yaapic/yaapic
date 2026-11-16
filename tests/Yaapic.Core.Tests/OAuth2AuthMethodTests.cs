using Yaapic.Core.Auth;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class OAuth2AuthMethodTests
{
    private readonly FakeOAuth2TokenService _tokens = new();

    private static OAuth2Token Token(string type = "Bearer") => new("tok", type, null, null, null, DateTimeOffset.UnixEpoch);

    private static ResolvedRequest Request(string url = "https://example.test/a?x=1", params KeyValueEntry[] headers) =>
        new(HttpMethodKind.Get, url, headers, null, null, [], null, null);

    private async Task<AuthenticationResult> Run(ResolvedRequest request, OAuth2Settings? settings = null) =>
        await new OAuth2AuthMethod(_tokens).ApplyAsync(
            request, new ResolvedAuth(AuthKind.OAuth2, "", "", settings ?? OAuth2Settings.Default, null, null), CancellationToken.None);

    private static string? Authorization(AuthenticationResult r) =>
        r.Request!.Headers.SingleOrDefault(h => h.Key == "Authorization")?.Value;

    [Fact]
    public async Task Default_settings_send_a_bearer_header()
    {
        _tokens.TokenResult = OAuth2TokenResult.Ok(Token());
        Assert.Equal("Bearer tok", Authorization(await Run(Request())));
    }

    [Fact]
    public async Task Custom_prefix_is_used()
    {
        _tokens.TokenResult = OAuth2TokenResult.Ok(Token());
        var result = await Run(Request(), OAuth2Settings.Default with { HeaderPrefix = "JWT" });
        Assert.Equal("JWT tok", Authorization(result));
    }

    [Fact]
    public async Task Empty_prefix_falls_back_to_the_token_type()
    {
        _tokens.TokenResult = OAuth2TokenResult.Ok(Token("mac"));
        var result = await Run(Request(), OAuth2Settings.Default with { HeaderPrefix = "" });
        Assert.Equal("mac tok", Authorization(result));
    }

    [Fact]
    public async Task Empty_prefix_and_token_type_send_the_bare_token()
    {
        _tokens.TokenResult = OAuth2TokenResult.Ok(Token(""));
        var result = await Run(Request(), OAuth2Settings.Default with { HeaderPrefix = "" });
        Assert.Equal("tok", Authorization(result));
    }

    [Fact]
    public async Task Query_placement_uses_the_default_parameter_name()
    {
        _tokens.TokenResult = OAuth2TokenResult.Ok(Token());
        var result = await Run(Request(), OAuth2Settings.Default with { Placement = OAuth2TokenPlacement.Query, QueryParameterName = "" });
        Assert.Equal("https://example.test/a?x=1&access_token=tok", result.Request!.Url);
        Assert.Null(Authorization(result));
    }

    [Fact]
    public async Task Query_placement_uses_the_custom_parameter_name()
    {
        _tokens.TokenResult = OAuth2TokenResult.Ok(Token());
        var result = await Run(Request(), OAuth2Settings.Default with { Placement = OAuth2TokenPlacement.Query, QueryParameterName = "t" });
        Assert.Equal("https://example.test/a?x=1&t=tok", result.Request!.Url);
    }

    [Fact]
    public async Task Token_error_is_prefixed_and_propagated()
    {
        _tokens.TokenResult = OAuth2TokenResult.Fail("invalid_client");
        var result = await Run(Request());
        Assert.Null(result.Request);
        Assert.Equal("OAuth 2.0: invalid_client", result.ErrorMessage);
    }

    [Fact]
    public async Task User_authorization_header_wins_without_calling_the_service()
    {
        var request = Request(headers: new KeyValueEntry("authorization", "Basic x"));
        var result = await Run(request);
        Assert.Equal(request, result.Request);
        Assert.Empty(_tokens.TokenCalls);
    }

    [Fact]
    public async Task The_resolved_settings_reach_the_service()
    {
        _tokens.TokenResult = OAuth2TokenResult.Ok(Token());
        var settings = OAuth2Settings.Default with { ClientId = "cid", TokenUrl = "https://idp.test/token" };
        await Run(Request(), settings);
        Assert.Same(settings, Assert.Single(_tokens.TokenCalls));
    }

    [Fact]
    public void Kind_is_OAuth2() => Assert.Equal(AuthKind.OAuth2, new OAuth2AuthMethod(_tokens).Kind);
}
