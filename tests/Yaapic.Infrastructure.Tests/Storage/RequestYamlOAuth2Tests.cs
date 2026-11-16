using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class RequestYamlOAuth2Tests
{
    private const string Header = "version: 1\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\nmethod: GET\n";

    private static HttpRequestDefinition With(OAuth2Settings? settings) =>
        HttpRequestDefinition.Create("r") with
        {
            Auth = RequestAuth.None with { Kind = AuthKind.OAuth2, OAuth2 = settings },
        };

    private static OAuth2Settings Full(OAuth2Grant grant) => new(
        grant, "https://idp/token", "https://idp/auth", "{{clientId}}", "{{clientSecret}}", "read write", "api",
        "u", "{{pw}}", "http://127.0.0.1:1/cb", OAuth2ClientAuthentication.RequestBody, OAuth2TokenPlacement.Query,
        "Token", "tok");

    public static TheoryData<OAuth2Grant> Grants() => new()
    {
        OAuth2Grant.ClientCredentials, OAuth2Grant.AuthorizationCode, OAuth2Grant.Password,
    };

    private static RequestAuth ReadAuth(string authYaml) =>
        RequestYamlSerializer.Deserialize(Header + authYaml).Auth;

    [Theory]
    [MemberData(nameof(Grants))]
    public void RoundTripKeepsEveryField(OAuth2Grant grant)
    {
        var request = With(Full(grant));
        var back = RequestYamlSerializer.Deserialize(RequestYamlSerializer.Serialize(request));
        Assert.Equal(Full(grant), back.Auth.OAuth2);
        Assert.Equal(AuthKind.OAuth2, back.Auth.Kind);
    }

    [Fact]
    public void MissingKeysReadAsDefaults()
    {
        var auth = ReadAuth("auth:\n  type: oauth2\n  oauth2:\n    tokenUrl: https://t\n");
        Assert.Equal(OAuth2Settings.Default with { TokenUrl = "https://t" }, auth.OAuth2);
        Assert.Equal(OAuth2Settings.Default, ReadAuth("auth:\n  type: oauth2\n").OAuth2);
    }

    [Fact]
    public void UnknownEnumTextReadsAsDefault()
    {
        var auth = ReadAuth("auth:\n  type: oauth2\n  oauth2:\n    grant: x\n    clientAuth: y\n    placement: z\n");
        Assert.Equal(OAuth2Settings.Default, auth.OAuth2);
    }

    [Fact]
    public void OAuth2SectionOnOtherKindIsIgnored()
    {
        var auth = ReadAuth("auth:\n  type: bearer\n  token: t\n  oauth2:\n    tokenUrl: https://t\n");
        Assert.Equal(AuthKind.Bearer, auth.Kind);
        Assert.Null(auth.OAuth2);
    }

    [Fact]
    public void OtherKindsWriteNoOAuth2Section()
    {
        var request = HttpRequestDefinition.Create("r");
        Assert.DoesNotContain("oauth2", RequestYamlSerializer.Serialize(request));
    }

    [Fact]
    public void OutputIsStable()
    {
        var request = With(Full(OAuth2Grant.AuthorizationCode));
        var yaml = RequestYamlSerializer.Serialize(request);
        Assert.Equal(yaml, RequestYamlSerializer.Serialize(RequestYamlSerializer.Deserialize(yaml)));
        Assert.Contains("grant: authorization_code", yaml);
    }
}
