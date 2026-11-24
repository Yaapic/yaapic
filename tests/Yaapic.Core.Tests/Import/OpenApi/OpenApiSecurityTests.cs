using Yaapic.Core.Import;
using Yaapic.Core.Import.OpenApi;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.OpenApi;

public class OpenApiSecurityTests
{
    private static readonly OpenApiImporter Importer = new(new StructuredTextReader(new NoYaml()));

    private sealed class NoYaml : IYamlTreeReader
    {
        public DataNode Parse(string yaml) => throw new NotSupportedException();
    }

    private static ImportFolder Import(string schemes, string operation, out ImportContext context, string top = "")
    {
        var json = "{\"openapi\":\"3.0.3\",\"info\":{\"title\":\"T\"}," + top
            + "\"components\":{\"securitySchemes\":" + schemes + "},\"paths\":{\"/x\":{\"get\":" + operation + "}}}";
        context = new ImportContext(SequentialIds.Create(), ImportOptions.Default);
        return Assert.IsType<ImportFolder>(Importer.Import(ImportInput.FromText("api.json", json), context).Collection);
    }

    private static RequestAuth Auth(string schemes, string security, out ImportContext context, out ImportFolder folder, string top = "")
    {
        folder = Import(schemes, "{" + security + "}", out context, top);
        return Assert.IsType<ImportRequest>(folder.Children[0]).Request.Auth;
    }

    private static string[] Vars(ImportFolder folder) =>
        folder.Folder.Variables.Select(v => v.Key).Where(k => k != "baseUrl").ToArray();

    [Fact]
    public void Http_schemes_map_with_placeholders()
    {
        var basic = Auth("""{"my-auth":{"type":"http","scheme":"BASIC"}}""", "\"security\":[{\"my-auth\":[]}]", out var context, out var folder);
        Assert.Equal(RequestAuth.None with { Kind = AuthKind.Basic, Username = "{{my_auth_username}}", Password = "{{my_auth_password}}" }, basic);
        Assert.Equal(["my_auth_username", "my_auth_password"], Vars(folder));
        Assert.All(folder.Folder.Variables.Where(v => v.Key != "baseUrl"), v => Assert.Equal("", v.Value));
        Assert.Contains(context.Report.Entries, e => e.Message.Contains("{{my_auth_username}}"));

        var bearer = Auth("""{"b":{"type":"http","scheme":"bearer"}}""", "\"security\":[{\"b\":[]}]", out _, out _);
        Assert.Equal(RequestAuth.None with { Kind = AuthKind.Bearer, Token = "{{b_token}}" }, bearer);

        var digest = Auth("""{"d":{"type":"http","scheme":"digest"}}""", "\"security\":[{\"d\":[]}]", out _, out _);
        Assert.Equal(AuthKind.Digest, digest.Kind);

        var other = Auth("""{"d":{"type":"http","scheme":"negotiate"}}""", "\"security\":[{\"d\":[]}]", out var ctx, out _);
        Assert.Equal(RequestAuth.None, other);
        Assert.Contains(ctx.Report.Entries, e => e.Severity == ImportSeverity.Warning && e.Message.Contains("http negotiate"));
    }

    [Fact]
    public void Api_key_maps_header_and_query_and_warns_for_cookie()
    {
        var header = Auth("""{"k":{"type":"apiKey","name":"X-Key","in":"header"}}""", "\"security\":[{\"k\":[]}]", out _, out _);
        Assert.Equal(RequestAuth.None with { Kind = AuthKind.ApiKey, Key = "X-Key", Value = "{{k_key}}", Location = ApiKeyLocation.Header }, header);

        var query = Auth("""{"k":{"type":"apiKey","name":"api_key","in":"query"}}""", "\"security\":[{\"k\":[]}]", out _, out _);
        Assert.Equal(ApiKeyLocation.Query, query.Location);

        var cookie = Auth("""{"k":{"type":"apiKey","name":"s","in":"cookie"}}""", "\"security\":[{\"k\":[]}]", out var context, out var folder);
        Assert.Equal(RequestAuth.None, cookie);
        Assert.Empty(Vars(folder));
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Warning && e.Message.Contains("apiKey in cookie"));
    }

    [Fact]
    public void OAuth2_picks_the_first_flow_and_joins_scopes()
    {
        const string flows = """{"o":{"type":"oauth2","flows":{"password":{"tokenUrl":"https://p/t"},"authorizationCode":{"authorizationUrl":"https://a/auth","tokenUrl":"https://a/t"},"implicit":{"authorizationUrl":"https://i"}}}}""";
        var code = Auth(flows, "\"security\":[{\"o\":[\"read\",\"write\"]}]", out _, out var folder);
        Assert.Equal(AuthKind.OAuth2, code.Kind);
        Assert.Equal(OAuth2Grant.AuthorizationCode, code.OAuth2!.Grant);
        Assert.Equal("https://a/auth", code.OAuth2.AuthorizationUrl);
        Assert.Equal("https://a/t", code.OAuth2.TokenUrl);
        Assert.Equal("read write", code.OAuth2.Scope);
        Assert.Equal("{{o_client_id}}", code.OAuth2.ClientId);
        Assert.Equal("{{o_client_secret}}", code.OAuth2.ClientSecret);
        Assert.Equal("", code.OAuth2.RedirectUri);
        Assert.Equal(["o_client_id", "o_client_secret"], Vars(folder));

        var cc = Auth("""{"o":{"type":"oauth2","flows":{"password":{"tokenUrl":"t"},"clientCredentials":{"tokenUrl":"https://c/t"}}}}""", "\"security\":[{\"o\":[]}]", out _, out _);
        Assert.Equal(OAuth2Grant.ClientCredentials, cc.OAuth2!.Grant);
        Assert.Equal("", cc.OAuth2.Scope);

        var password = Auth("""{"o":{"type":"oauth2","flows":{"password":{"tokenUrl":"t"}}}}""", "\"security\":[{\"o\":[]}]", out _, out folder);
        Assert.Equal(OAuth2Grant.Password, password.OAuth2!.Grant);
        Assert.Equal("{{o_username}}", password.OAuth2.Username);
        Assert.Equal("{{o_password}}", password.OAuth2.Password);
        Assert.Equal(4, Vars(folder).Length);
    }

    [Fact]
    public void Unsupported_and_unknown_schemes_warn()
    {
        var implicitOnly = Auth("""{"o":{"type":"oauth2","flows":{"implicit":{"authorizationUrl":"u"}}}}""", "\"security\":[{\"o\":[]}]", out var context, out var folder);
        Assert.Equal(RequestAuth.None, implicitOnly);
        Assert.Empty(Vars(folder));
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Warning && e.Message.Contains("oauth2 implicit"));

        var oidc = Auth("""{"o":{"type":"openIdConnect","openIdConnectUrl":"u"}}""", "\"security\":[{\"o\":[]}]", out context, out _);
        Assert.Equal(RequestAuth.None, oidc);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Warning && e.Message.Contains("openIdConnect"));

        var unknown = Auth("{}", "\"security\":[{\"missing\":[]}]", out context, out _);
        Assert.Equal(RequestAuth.None, unknown);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Warning && e.Message.Contains("\"missing\""));
    }

    [Fact]
    public void Global_security_applies_and_an_operation_can_override_it()
    {
        const string schemes = """{"b":{"type":"http","scheme":"bearer"}}""";
        const string top = "\"security\":[{\"b\":[]}],";
        Assert.Equal(AuthKind.Bearer, Auth(schemes, "", out _, out _, top).Kind);
        Assert.Equal(RequestAuth.None, Auth(schemes, "\"security\":[]", out _, out var folder, top));
        Assert.Empty(Vars(folder));
        Assert.Equal(RequestAuth.None, Auth(schemes, "\"security\":[{}]", out _, out _, top));
    }

    [Fact]
    public void No_security_gives_none_and_no_variables_or_placeholder_info()
    {
        var auth = Auth("""{"b":{"type":"http","scheme":"bearer"}}""", "", out var context, out var folder);
        Assert.Equal(RequestAuth.None, auth);
        Assert.Empty(Vars(folder));
        Assert.DoesNotContain(context.Report.Entries, e => e.Message.Contains("empty variables"));
    }

    [Fact]
    public void Variables_are_created_once_and_the_other_security_info_is_reported_once()
    {
        var operations = string.Join(",", Enumerable.Range(0, 10).Select(i =>
            "\"/p" + i + "\":{\"get\":{\"security\":[{\"b\":[]},{\"c\":[]}]}}"));
        var json = "{\"openapi\":\"3.0.3\",\"info\":{\"title\":\"T\"},\"components\":{\"securitySchemes\":{\"b\":{\"type\":\"http\",\"scheme\":\"bearer\"}}},\"paths\":{" + operations + "}}";
        var context = new ImportContext(SequentialIds.Create(), ImportOptions.Default);
        var folder = Assert.IsType<ImportFolder>(Importer.Import(ImportInput.FromText("api.json", json), context).Collection);
        Assert.Equal(["b_token"], Vars(folder));
        Assert.Single(context.Report.Entries, e => e.Message.Contains("first security requirement"));
        Assert.Single(context.Report.Entries, e => e.Message.Contains("empty variables"));
    }
}
