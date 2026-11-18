using System.Text;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class OAuth2TokenRequestsTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static OAuth2Settings Settings(OAuth2ClientAuthentication auth = OAuth2ClientAuthentication.BasicHeader) =>
        OAuth2Settings.Default with { TokenUrl = "https://t/token", ClientId = "a b", ClientSecret = "c:d", ClientAuthentication = auth };

    private static string? Header(Yaapic.Core.Http.ResolvedRequest r, string name) =>
        r.Headers.FirstOrDefault(h => h.Key == name)?.Value;

    [Fact]
    public void ClientCredentials_BasicHeader_FormEncodesPartsBeforeBase64()
    {
        var r = OAuth2TokenRequests.ClientCredentials(Settings() with { Scope = "x y" });
        Assert.Equal(HttpMethodKind.Post, r.Method);
        Assert.Equal("https://t/token", r.Url);
        Assert.Equal("grant_type=client_credentials&scope=x%20y", r.BodyText);
        Assert.Equal("application/x-www-form-urlencoded", r.BodyContentType);
        Assert.Equal("application/json", Header(r, "Accept"));
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("a%20b:c%3Ad")), Header(r, "Authorization"));
        Assert.False(r.FollowRedirects);
        Assert.Equal(30, r.TimeoutSeconds);
        Assert.Null(r.PendingAuth);
    }

    [Fact]
    public void ClientCredentials_RequestBody_AddsClientFieldsAndAudience()
    {
        var r = OAuth2TokenRequests.ClientCredentials(Settings(OAuth2ClientAuthentication.RequestBody) with { Audience = "api" });
        Assert.Equal("grant_type=client_credentials&audience=api&client_id=a%20b&client_secret=c%3Ad", r.BodyText);
        Assert.Null(Header(r, "Authorization"));
    }

    [Fact]
    public void RequestBody_EmptySecret_OmitsClientSecret() =>
        Assert.Equal("grant_type=client_credentials&client_id=a",
            OAuth2TokenRequests.ClientCredentials(Settings(OAuth2ClientAuthentication.RequestBody) with { ClientId = "a", ClientSecret = "" }).BodyText);

    [Fact]
    public void BasicHeader_EmptyCredentials_AddsNoHeader() =>
        Assert.Null(Header(OAuth2TokenRequests.ClientCredentials(Settings() with { ClientId = "", ClientSecret = "" }), "Authorization"));

    [Fact]
    public void Password_SendsUserAndPassword() =>
        Assert.Equal("grant_type=password&username=u&password=p%26q&scope=s",
            OAuth2TokenRequests.Password(Settings() with { Username = "u", Password = "p&q", Scope = "s" }).BodyText);

    [Fact]
    public void AuthorizationCode_BasicHeader_StillSendsClientId() =>
        Assert.Equal("grant_type=authorization_code&code=c1&redirect_uri=http%3A%2F%2Fh%2Fcb&code_verifier=v&client_id=a%20b",
            OAuth2TokenRequests.AuthorizationCode(Settings(), "c1", "v", "http://h/cb").BodyText);

    [Fact]
    public void Refresh_OmitsEmptyScope() =>
        Assert.Equal("grant_type=refresh_token&refresh_token=r",
            OAuth2TokenRequests.Refresh(Settings(), "r").BodyText);

    private static OAuth2TokenParseResult Parse(int status, string body) =>
        OAuth2TokenResponses.Parse(new HttpResponseData(status, "", [], Encoding.UTF8.GetBytes(body), TimeSpan.Zero, "u"), Now);

    [Fact]
    public void Parse_FullResponse()
    {
        var t = Parse(200, """{"access_token":"a","token_type":"MAC","expires_in":60,"refresh_token":"r","scope":"s"}""").Token!;
        Assert.Equal(("a", "MAC", "r", "s"), (t.AccessToken, t.TokenType, t.RefreshToken, t.Scope));
        Assert.Equal(Now.AddSeconds(60), t.ExpiresAt);
        Assert.Equal(Now, t.ObtainedAt);
    }

    [Fact]
    public void Parse_StringExpiresInAndDefaultTokenType()
    {
        var t = Parse(200, """{"access_token":"a","expires_in":"120"}""").Token!;
        Assert.Equal("Bearer", t.TokenType);
        Assert.Equal(Now.AddSeconds(120), t.ExpiresAt);
    }

    [Fact]
    public void Parse_NoExpiresIn_NullExpiry() =>
        Assert.Null(Parse(200, """{"access_token":"a"}""").Token!.ExpiresAt);

    [Theory]
    [InlineData(400, """{"error":"invalid_grant","error_description":"expired"}""", "invalid_grant: expired")]
    [InlineData(400, """{"error":"invalid_client"}""", "invalid_client")]
    [InlineData(200, """{"error":"oops"}""", "oops")]
    public void Parse_Error(int status, string body, string expected)
    {
        var r = Parse(status, body);
        Assert.Null(r.Token);
        Assert.Equal(expected, r.Error);
    }

    [Theory]
    [InlineData(401, "Unauthorized secret-body")]
    [InlineData(200, "<html>hi</html>")]
    [InlineData(200, """{"token_type":"Bearer"}""")]
    [InlineData(200, """{"access_token":5}""")]
    [InlineData(500, """{"x":1}""")]
    public void Parse_Invalid_DoesNotEchoBody(int status, string body)
    {
        var r = Parse(status, body);
        Assert.Null(r.Token);
        Assert.Contains(status.ToString(), r.Error);
        Assert.DoesNotContain("secret-body", r.Error);
    }
}
