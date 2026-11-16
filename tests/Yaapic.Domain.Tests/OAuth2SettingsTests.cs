using Yaapic.Domain.Http;

namespace Yaapic.Domain.Tests;

public class OAuth2SettingsTests
{
    [Fact]
    public void Default_has_the_documented_values()
    {
        var d = OAuth2Settings.Default;
        Assert.Equal(OAuth2Grant.ClientCredentials, d.Grant);
        Assert.Equal("http://127.0.0.1:8742/callback", d.RedirectUri);
        Assert.Equal("Bearer", d.HeaderPrefix);
        Assert.Equal("access_token", d.QueryParameterName);
        Assert.Equal(OAuth2ClientAuthentication.BasicHeader, d.ClientAuthentication);
        Assert.Equal(OAuth2TokenPlacement.Header, d.Placement);
        Assert.All(new[] { d.TokenUrl, d.AuthorizationUrl, d.ClientId, d.ClientSecret, d.Scope, d.Audience, d.Username, d.Password }, s => Assert.Equal("", s));
    }

    [Fact]
    public void Records_compare_by_value() =>
        Assert.Equal(OAuth2Settings.Default with { ClientId = "a" }, OAuth2Settings.Default with { ClientId = "a" });

    [Fact]
    public void With_sets_OAuth2_on_RequestAuth_and_None_is_unchanged()
    {
        var auth = RequestAuth.None with { Kind = AuthKind.OAuth2, OAuth2 = OAuth2Settings.Default };
        Assert.Equal(OAuth2Settings.Default, auth.OAuth2);
        Assert.Null(RequestAuth.None.OAuth2);
    }

    [Fact]
    public void Definitions_differing_only_in_OAuth2_ClientId_are_not_equivalent()
    {
        var a = HttpRequestDefinition.Create("r");
        var withAuth = (string id) => a with { Auth = RequestAuth.None with { Kind = AuthKind.OAuth2, OAuth2 = OAuth2Settings.Default with { ClientId = id } } };
        Assert.True(withAuth("x").IsEquivalentTo(withAuth("x")));
        Assert.False(withAuth("x").IsEquivalentTo(withAuth("y")));
    }
}
