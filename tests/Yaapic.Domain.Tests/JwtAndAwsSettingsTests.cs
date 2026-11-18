using Yaapic.Domain.Http;

namespace Yaapic.Domain.Tests;

public class JwtAndAwsSettingsTests
{
    [Fact]
    public void Jwt_default_has_the_documented_values()
    {
        var d = JwtSettings.Default;
        Assert.Equal(JwtAlgorithm.Hs256, d.Algorithm);
        Assert.Equal("{}", d.Payload);
        Assert.True(d.AddIssuedAt);
        Assert.Equal(0, d.ExpiresInSeconds);
        Assert.Equal(JwtPlacement.Header, d.Placement);
        Assert.Equal("Bearer", d.HeaderPrefix);
        Assert.Equal("token", d.QueryParameterName);
        Assert.False(d.SecretIsBase64);
        Assert.All(new[] { d.Secret, d.PrivateKey, d.ExtraHeaders }, s => Assert.Equal("", s));
    }

    [Fact]
    public void Aws_default_is_all_empty()
    {
        var d = AwsV4Settings.Default;
        Assert.All(new[] { d.AccessKeyId, d.SecretAccessKey, d.SessionToken, d.Region, d.Service }, s => Assert.Equal("", s));
    }

    [Fact]
    public void Records_compare_by_value()
    {
        Assert.Equal(JwtSettings.Default with { Secret = "a" }, JwtSettings.Default with { Secret = "a" });
        Assert.NotEqual(JwtSettings.Default, JwtSettings.Default with { Secret = "a" });
        Assert.Equal(AwsV4Settings.Default with { Region = "eu" }, AwsV4Settings.Default with { Region = "eu" });
        Assert.NotEqual(AwsV4Settings.Default, AwsV4Settings.Default with { Region = "eu" });
    }

    [Fact]
    public void With_sets_Jwt_and_AwsV4_on_RequestAuth_and_None_is_unchanged()
    {
        var auth = RequestAuth.None with { Jwt = JwtSettings.Default, AwsV4 = AwsV4Settings.Default };
        Assert.Equal(JwtSettings.Default, auth.Jwt);
        Assert.Equal(AwsV4Settings.Default, auth.AwsV4);
        Assert.Null(RequestAuth.None.Jwt);
        Assert.Null(RequestAuth.None.AwsV4);
    }

    [Fact]
    public void Definitions_differing_only_in_Jwt_Payload_are_not_equivalent()
    {
        var a = HttpRequestDefinition.Create("r");
        var withAuth = (string p) => a with { Auth = RequestAuth.None with { Kind = AuthKind.Jwt, Jwt = JwtSettings.Default with { Payload = p } } };
        Assert.True(withAuth("{}").IsEquivalentTo(withAuth("{}")));
        Assert.False(withAuth("{}").IsEquivalentTo(withAuth("{\"a\":1}")));
    }

    [Fact]
    public void Definitions_differing_only_in_AwsV4_Region_are_not_equivalent()
    {
        var a = HttpRequestDefinition.Create("r");
        var withAuth = (string r) => a with { Auth = RequestAuth.None with { Kind = AuthKind.AwsV4, AwsV4 = AwsV4Settings.Default with { Region = r } } };
        Assert.True(withAuth("eu").IsEquivalentTo(withAuth("eu")));
        Assert.False(withAuth("eu").IsEquivalentTo(withAuth("us")));
    }
}
