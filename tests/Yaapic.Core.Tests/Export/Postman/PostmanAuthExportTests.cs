using System.Text;
using Yaapic.Core.Export;
using Yaapic.Core.Export.Postman;
using Yaapic.Core.Import;
using Yaapic.Core.Import.Postman;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Export.Postman;

public class PostmanAuthExportTests
{
    private static readonly PostmanCollectionExporter Exporter = new();

    private static CollectionNode Root(IReadOnlyList<KeyValueEntry> variables, params CollectionNode[] children) =>
        CollectionNode.ForFolder(new FolderDefinition(new Guid("00000000-0000-0000-0000-000000000001"), "My API", "", variables), children);

    private static HttpRequestDefinition Req(RequestAuth auth) =>
        new(Guid.NewGuid(), "r", HttpMethodKind.Get, "https://x.test", [], [], RequestBody.None, auth, RequestSettings.Default);

    private static DataNode Export(CollectionNode root) =>
        DataNodeJson.Parse(Encoding.UTF8.GetString(Exporter.Export(new ExportSource(root, [], [], root.Name)).Files[0].Content));

    private static DataNode Request(RequestAuth auth) => Export(Root([], CollectionNode.ForRequest(Req(auth))))["item"]![0]!["request"]!;

    private static string Expected(string type, params (string K, string V)[] parameters) =>
        "{\"type\":\"" + type + "\",\"" + type + "\":[" + string.Join(",", parameters.Select(p =>
            "{\"key\":\"" + p.K + "\",\"value\":\"" + p.V + "\",\"type\":\"string\"}")) + "]}";

    private static void AssertAuth(RequestAuth auth, string expected) =>
        Assert.Equal(expected, DataNodeJson.Write(Request(auth)["auth"]!, indented: false));

    private static RequestAuth Of(AuthKind kind) => RequestAuth.None with { Kind = kind };

    [Fact]
    public void NoneWritesNoAuth() => Assert.Null(Request(RequestAuth.None)["auth"]);

    [Fact]
    public void WritesAuthAfterUrl() =>
        Assert.Equal(["method", "header", "url", "auth"], Request(Of(AuthKind.Bearer) with { Token = "t" }).Members.Select(m => m.Key));

    [Fact]
    public void BasicAndDigest()
    {
        AssertAuth(Of(AuthKind.Basic) with { Username = "u", Password = "p" }, Expected("basic", ("username", "u"), ("password", "p")));
        AssertAuth(Of(AuthKind.Digest) with { Username = "u", Password = "p" }, Expected("digest", ("username", "u"), ("password", "p")));
    }

    [Fact]
    public void BearerAndApiKey()
    {
        AssertAuth(Of(AuthKind.Bearer) with { Token = "{{tok}}" }, Expected("bearer", ("token", "{{tok}}")));
        AssertAuth(Of(AuthKind.ApiKey) with { Key = "k", Value = "v", Location = ApiKeyLocation.Query }, Expected("apikey", ("key", "k"), ("value", "v"), ("in", "query")));
        AssertAuth(Of(AuthKind.ApiKey) with { Key = "k", Value = "v" }, Expected("apikey", ("key", "k"), ("value", "v"), ("in", "header")));
    }

    [Fact]
    public void AwsV4() =>
        AssertAuth(Of(AuthKind.AwsV4) with { AwsV4 = new AwsV4Settings("a", "b", "", "eu-west-1", "s3") },
            Expected("awsv4", ("accessKey", "a"), ("secretKey", "b"), ("sessionToken", ""), ("region", "eu-west-1"), ("service", "s3")));

    [Theory]
    [InlineData(OAuth2Grant.ClientCredentials, "client_credentials", OAuth2TokenPlacement.Header, "header", OAuth2ClientAuthentication.BasicHeader, "header")]
    [InlineData(OAuth2Grant.AuthorizationCode, "authorization_code", OAuth2TokenPlacement.Query, "queryParams", OAuth2ClientAuthentication.RequestBody, "body")]
    [InlineData(OAuth2Grant.Password, "password_credentials", OAuth2TokenPlacement.Header, "header", OAuth2ClientAuthentication.RequestBody, "body")]
    public void OAuth2(OAuth2Grant grant, string grantName, OAuth2TokenPlacement placement, string placementName, OAuth2ClientAuthentication client, string clientName) =>
        AssertAuth(Of(AuthKind.OAuth2) with { OAuth2 = new OAuth2Settings(grant, "t", "a", "id", "cs", "sc", "au", "u", "p", "r", client, placement, "Bearer", "access_token") },
            Expected("oauth2", ("grant_type", grantName), ("accessTokenUrl", "t"), ("authUrl", "a"), ("clientId", "id"), ("clientSecret", "cs"),
                ("scope", "sc"), ("audience", "au"), ("username", "u"), ("password", "p"), ("redirect_uri", "r"),
                ("addTokenTo", placementName), ("headerPrefix", "Bearer"), ("client_authentication", clientName)));

    [Theory]
    [InlineData(true, JwtPlacement.Header, "header", "true")]
    [InlineData(false, JwtPlacement.Query, "queryParams", "false")]
    public void Jwt(bool base64, JwtPlacement placement, string placementName, string flag) =>
        AssertAuth(Of(AuthKind.Jwt) with { Jwt = new JwtSettings(JwtAlgorithm.Hs512, "s", base64, "", "{}", "", true, 0, placement, "Bearer", "token") },
            Expected("jwt", ("algorithm", "HS512"), ("secret", "s"), ("isSecretBase64Encoded", flag), ("privateKey", ""), ("payload", "{}"),
                ("header", ""), ("addTokenTo", placementName), ("headerPrefix", "Bearer"), ("queryParamKey", "token")));

    public static TheoryData<RequestAuth> RoundTripAuths() => new()
    {
        Of(AuthKind.Basic) with { Username = "u", Password = "p" },
        Of(AuthKind.Digest) with { Username = "u", Password = "p" },
        Of(AuthKind.Bearer) with { Token = "t" },
        Of(AuthKind.ApiKey) with { Key = "k", Value = "v", Location = ApiKeyLocation.Query },
        Of(AuthKind.AwsV4) with { AwsV4 = new AwsV4Settings("a", "b", "c", "r", "s") },
        Of(AuthKind.OAuth2) with { OAuth2 = new OAuth2Settings(OAuth2Grant.Password, "t", "a", "id", "cs", "sc", "au", "u", "p", "r", OAuth2ClientAuthentication.RequestBody, OAuth2TokenPlacement.Query, "Tok", "access_token") },
        Of(AuthKind.Jwt) with { Jwt = new JwtSettings(JwtAlgorithm.Es256, "s", true, "pk", "{\"a\":1}", "{\"kid\":\"1\"}", true, 0, JwtPlacement.Query, "Tok", "tk") },
    };

    [Theory]
    [MemberData(nameof(RoundTripAuths))]
    public void RoundTripsThroughTheImporter(RequestAuth auth)
    {
        var report = new ImportReport();
        var node = Request(auth);
        var back = PostmanAuth.Map(node["auth"], "r", report);
        Assert.Equal(auth, back);
    }

    [Fact]
    public void WritesCollectionVariables()
    {
        var root = Root([new("a", "1"), new("b", "2", false), new("c", "", true, true)]);
        Assert.Equal(
            "[{\"key\":\"a\",\"value\":\"1\",\"type\":\"string\"},{\"key\":\"b\",\"value\":\"2\",\"type\":\"string\",\"disabled\":true},{\"key\":\"c\",\"value\":\"\",\"type\":\"secret\"}]",
            DataNodeJson.Write(Export(root)["variable"]!, indented: false));
    }

    [Fact]
    public void OmitsEmptyVariables() => Assert.Null(Export(Root([]))["variable"]);
}
