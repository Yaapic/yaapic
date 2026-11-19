using Yaapic.Core.Import;
using Yaapic.Core.Import.Insomnia;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests.Import.Insomnia;

public class InsomniaAuthTests
{
    private static (RequestAuth Auth, ImportReport Report) Map(string? auth)
    {
        ImportReport report = new();
        return (InsomniaAuth.Map(auth is null ? null : DataNodeJson.Parse(auth), "r", report), report);
    }

    private static int Count(ImportReport report, ImportSeverity severity) => report.Entries.Count(e => e.Severity == severity);

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("""{"type": ""}""")]
    [InlineData("""{"type": "basic", "disabled": true, "username": "a"}""")]
    [InlineData("""{"type": "none"}""")]
    public void Absent_empty_or_disabled_is_none(string? auth)
    {
        var (a, report) = Map(auth);
        Assert.Equal(RequestAuth.None, a);
        Assert.Empty(report.Entries);
    }

    [Theory]
    [InlineData("basic", AuthKind.Basic)]
    [InlineData("digest", AuthKind.Digest)]
    public void Username_and_password_types(string type, AuthKind kind) =>
        Assert.Equal(
            RequestAuth.None with { Kind = kind, Username = "{{user}}", Password = "{{pw}}" },
            Map($$$"""{"type": "{{{type}}}", "username": "{{ _.user }}", "password": "{{ _.pw }}"}""").Auth);

    [Fact]
    public void Bearer_prefix_other_than_bearer_warns()
    {
        var (a, report) = Map("""{"type": "bearer", "token": "{{ _.t }}", "prefix": "Token"}""");
        Assert.Equal(RequestAuth.None with { Kind = AuthKind.Bearer, Token = "{{t}}" }, a);
        Assert.Equal(1, Count(report, ImportSeverity.Warning));
        Assert.Equal(0, Count(Map("""{"type": "bearer", "token": "{{ _.t }}", "prefix": "Bearer"}""").Report, ImportSeverity.Warning));
    }

    [Theory]
    [InlineData("queryParams", ApiKeyLocation.Query)]
    [InlineData("header", ApiKeyLocation.Header)]
    public void ApiKey_location(string addTo, ApiKeyLocation expected) =>
        Assert.Equal(
            RequestAuth.None with { Kind = AuthKind.ApiKey, Key = "k", Value = "{{v}}", Location = expected },
            Map($$$"""{"type": "apikey", "key": "k", "value": "{{v}}", "addTo": "{{{addTo}}}"}""").Auth);

    [Fact]
    public void Iam_is_aws_v4() =>
        Assert.Equal(
            RequestAuth.None with { Kind = AuthKind.AwsV4, AwsV4 = new AwsV4Settings("{{a}}", "{{s}}", "{{t}}", "eu-west-1", "s3") },
            Map("""{"type": "iam", "accessKeyId": "{{a}}", "secretAccessKey": "{{s}}", "sessionToken": "{{t}}", "region": "eu-west-1", "service": "s3"}""").Auth);

    [Theory]
    [InlineData("client_credentials", OAuth2Grant.ClientCredentials)]
    [InlineData("authorization_code", OAuth2Grant.AuthorizationCode)]
    [InlineData("password", OAuth2Grant.Password)]
    public void OAuth2_grants(string grant, OAuth2Grant expected)
    {
        var (a, _) = Map($$$"""
            {"type": "oauth2", "grantType": "{{{grant}}}", "accessTokenUrl": "https://t", "authorizationUrl": "https://a",
             "clientId": "id", "clientSecret": "{{cs}}", "scope": "s", "audience": "aud", "username": "u", "password": "{{p}}",
             "redirectUrl": "https://r", "credentialsInBody": true, "headerPrefix": "Token"}
            """);
        var expectedSettings = new OAuth2Settings(
            expected, "https://t", "https://a", "id", "{{cs}}", "s", "aud", "u", "{{p}}", "https://r",
            OAuth2ClientAuthentication.RequestBody, OAuth2TokenPlacement.Header, "Token", OAuth2Settings.Default.QueryParameterName);
        Assert.Equal(RequestAuth.None with { Kind = AuthKind.OAuth2, OAuth2 = expectedSettings }, a);
    }

    [Fact]
    public void OAuth2_defaults_to_basic_header_and_bearer_prefix()
    {
        var (a, _) = Map("""{"type": "oauth2", "grantType": "client_credentials"}""");
        Assert.Equal(OAuth2ClientAuthentication.BasicHeader, a.OAuth2!.ClientAuthentication);
        Assert.Equal(OAuth2Settings.Default.HeaderPrefix, a.OAuth2.HeaderPrefix);
    }

    [Theory]
    [InlineData("""{"type": "oauth2", "grantType": "implicit"}""")]
    [InlineData("""{"type": "oauth2", "grantType": "jwt-bearer"}""")]
    [InlineData("""{"type": "ntlm", "username": "a"}""")]
    [InlineData("""{"type": "hawk"}""")]
    public void Unsupported_warns_and_is_none(string auth)
    {
        var (a, report) = Map(auth);
        Assert.Equal(RequestAuth.None, a);
        Assert.Equal(1, Count(report, ImportSeverity.Warning));
    }

    [Fact]
    public void Plain_text_info_appears_once_and_not_for_variables()
    {
        var (_, report) = Map("""{"type": "basic", "username": "bob", "password": "hunter2"}""");
        Assert.Equal(1, Count(report, ImportSeverity.Info));
        Assert.Equal(0, Count(Map("""{"type": "bearer", "token": "{{ _.token }}"}""").Report, ImportSeverity.Info));
    }
}
