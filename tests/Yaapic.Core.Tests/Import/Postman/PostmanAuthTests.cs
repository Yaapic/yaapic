using Yaapic.Core.Import;
using Yaapic.Core.Import.Postman;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.Postman;

public class PostmanAuthTests
{
    private static (RequestAuth? Auth, ImportReport Report) Map(string? auth)
    {
        ImportReport report = new();
        return (PostmanAuth.Map(auth is null ? null : DataNodeJson.Parse(auth), "r", report), report);
    }

    private static string Block(string type, params (string Key, string Value)[] pairs) =>
        $$"""{"type": "{{type}}", "{{type}}": [{{string.Join(",", pairs.Select(p => $$"""{"key": "{{p.Key}}", "value": "{{p.Value}}", "type": "string"}"""))}}]}""";

    private static int Count(ImportReport report, ImportSeverity severity) => report.Entries.Count(e => e.Severity == severity);

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("{}")]
    public void Absent_auth_inherits(string? auth) => Assert.Null(Map(auth).Auth);

    [Fact]
    public void Noauth_is_none() => Assert.Equal(RequestAuth.None, Map("""{"type": "noauth"}""").Auth);

    [Fact]
    public void Basic_and_digest_read_the_credentials()
    {
        Assert.Equal(
            RequestAuth.None with { Kind = AuthKind.Basic, Username = "u", Password = "{{p}}" },
            Map(Block("basic", ("username", "u"), ("password", "{{p}}"))).Auth);
        Assert.Equal(
            RequestAuth.None with { Kind = AuthKind.Digest, Username = "u", Password = "{{p}}" },
            Map(Block("digest", ("username", "u"), ("password", "{{p}}"), ("realm", "r"))).Auth);
    }

    [Fact]
    public void Bearer_reads_the_token_in_both_shapes()
    {
        var expected = RequestAuth.None with { Kind = AuthKind.Bearer, Token = "{{t}}" };
        Assert.Equal(expected, Map(Block("bearer", ("token", "{{t}}"))).Auth);
        Assert.Equal(expected, Map("""{"type": "bearer", "bearer": {"token": "{{t}}"}}""").Auth);
    }

    [Theory]
    [InlineData("query", ApiKeyLocation.Query)]
    [InlineData("header", ApiKeyLocation.Header)]
    [InlineData("", ApiKeyLocation.Header)]
    public void Apikey_location(string where, ApiKeyLocation expected) =>
        Assert.Equal(
            RequestAuth.None with { Kind = AuthKind.ApiKey, Key = "k", Value = "{{v}}", Location = expected },
            Map(Block("apikey", ("key", "k"), ("value", "{{v}}"), ("in", where))).Auth);

    [Fact]
    public void Awsv4_maps_the_settings() =>
        Assert.Equal(
            RequestAuth.None with { Kind = AuthKind.AwsV4, AwsV4 = new AwsV4Settings("{{a}}", "{{s}}", "{{t}}", "eu-west-1", "s3") },
            Map(Block("awsv4", ("accessKey", "{{a}}"), ("secretKey", "{{s}}"), ("sessionToken", "{{t}}"), ("region", "eu-west-1"), ("service", "s3"))).Auth);

    [Theory]
    [InlineData("client_credentials", OAuth2Grant.ClientCredentials)]
    [InlineData("authorization_code", OAuth2Grant.AuthorizationCode)]
    [InlineData("authorization_code_with_pkce", OAuth2Grant.AuthorizationCode)]
    [InlineData("password_credentials", OAuth2Grant.Password)]
    public void Oauth2_grants(string grant, OAuth2Grant expected)
    {
        var (auth, report) = Map(Block("oauth2", ("grant_type", grant), ("accessTokenUrl", "https://t/token"), ("authUrl", "https://t/auth"),
            ("clientId", "id"), ("clientSecret", "{{cs}}"), ("scope", "a b"), ("audience", "aud"), ("username", "u"), ("password", "{{p}}"),
            ("redirect_uri", "http://127.0.0.1:1/cb")));

        Assert.Equal(
            RequestAuth.None with
            {
                Kind = AuthKind.OAuth2,
                OAuth2 = new OAuth2Settings(expected, "https://t/token", "https://t/auth", "id", "{{cs}}", "a b", "aud", "u", "{{p}}",
                    "http://127.0.0.1:1/cb", OAuth2ClientAuthentication.BasicHeader, OAuth2TokenPlacement.Header, "Bearer", "access_token"),
            },
            auth);
        Assert.Empty(report.Entries);
    }

    [Fact]
    public void Oauth2_body_authentication_query_placement_and_prefix()
    {
        var settings = Map(Block("oauth2", ("grant_type", "client_credentials"), ("client_authentication", "body"),
            ("addTokenTo", "queryParams"), ("headerPrefix", "Token"))).Auth!.OAuth2!;

        Assert.Equal(OAuth2ClientAuthentication.RequestBody, settings.ClientAuthentication);
        Assert.Equal(OAuth2TokenPlacement.Query, settings.Placement);
        Assert.Equal("Token", settings.HeaderPrefix);
    }

    [Fact]
    public void Oauth2_unknown_grant_warns_and_has_no_auth()
    {
        var (auth, report) = Map(Block("oauth2", ("grant_type", "implicit")));

        Assert.Equal(RequestAuth.None, auth);
        Assert.Equal(1, Count(report, ImportSeverity.Warning));
    }

    [Fact]
    public void Jwt_defaults()
    {
        var (auth, _) = Map(Block("jwt", ("secret", "{{s}}")));

        Assert.Equal(
            RequestAuth.None with { Kind = AuthKind.Jwt, Jwt = new JwtSettings(JwtAlgorithm.Hs256, "{{s}}", false, "", "{}", "", true, 0, JwtPlacement.Header, "Bearer", "token") },
            auth);
    }

    [Fact]
    public void Jwt_maps_every_field()
    {
        var (auth, _) = Map(Block("jwt", ("algorithm", "RS384"), ("secret", "{{s}}"), ("isSecretBase64Encoded", "true"), ("privateKey", "{{k}}"),
            ("payload", "{a:1}"), ("header", "{kid:x}"), ("addTokenTo", "queryParams"), ("headerPrefix", "JWT"), ("queryParamKey", "jwt")));

        Assert.Equal(
            new JwtSettings(JwtAlgorithm.Rs384, "{{s}}", true, "{{k}}", "{a:1}", "{kid:x}", true, 0, JwtPlacement.Query, "JWT", "jwt"),
            auth!.Jwt);
    }

    [Theory]
    [InlineData("HS384", JwtAlgorithm.Hs384)]
    [InlineData("HS512", JwtAlgorithm.Hs512)]
    [InlineData("RS256", JwtAlgorithm.Rs256)]
    [InlineData("RS512", JwtAlgorithm.Rs512)]
    [InlineData("ES256", JwtAlgorithm.Es256)]
    [InlineData("ES384", JwtAlgorithm.Es384)]
    [InlineData("ES512", JwtAlgorithm.Es512)]
    [InlineData("HS256", JwtAlgorithm.Hs256)]
    public void Jwt_algorithms(string name, JwtAlgorithm expected) =>
        Assert.Equal(expected, Map(Block("jwt", ("algorithm", name))).Auth!.Jwt!.Algorithm);

    [Fact]
    public void Jwt_unknown_algorithm_warns_and_uses_hs256()
    {
        var (auth, report) = Map(Block("jwt", ("algorithm", "PS256")));

        Assert.Equal(JwtAlgorithm.Hs256, auth!.Jwt!.Algorithm);
        Assert.Equal(1, Count(report, ImportSeverity.Warning));
    }

    [Theory]
    [InlineData("ntlm")]
    [InlineData("hawk")]
    [InlineData("oauth1")]
    public void Unsupported_types_warn_and_have_no_auth(string type)
    {
        var (auth, report) = Map(Block(type, ("username", "u")));

        Assert.Equal(RequestAuth.None, auth);
        Assert.Equal(1, Count(report, ImportSeverity.Warning));
        Assert.Equal(0, Count(report, ImportSeverity.Info));
    }

    [Fact]
    public void Dynamic_variables_are_translated() =>
        Assert.Equal("{{$guid}}", Map(Block("bearer", ("token", "{{$randomUUID}}"))).Auth!.Token);

    [Fact]
    public void Plain_text_info_only_for_literal_credentials_and_once()
    {
        var (_, variable) = Map(Block("bearer", ("token", "{{token}}")));
        Assert.Empty(variable.Entries);

        var (_, literal) = Map(Block("bearer", ("token", "abc")));
        Assert.Equal(1, Count(literal, ImportSeverity.Info));
        PostmanAuth.Map(DataNodeJson.Parse(Block("basic", ("username", "u"))), "r2", literal);
        Assert.Equal(1, Count(literal, ImportSeverity.Info));
    }

    // --- inheritance, through the importer ---

    private static ImportBundle Run(string json, out ImportReport report)
    {
        ImportContext context = new(SequentialIds.Create(), ImportOptions.Default);
        var bundle = new PostmanCollectionImporter().Import(ImportInput.FromText("c.json", json), context);
        report = context.Report;
        return bundle;
    }

    private static string Bearer(string token) => $$"""{"type": "bearer", "bearer": [{"key": "token", "value": "{{token}}"}]}""";

    private static RequestAuth Req(ImportItem item) => Assert.IsType<ImportRequest>(item).Request.Auth;

    [Fact]
    public void Requests_inherit_from_folder_then_collection_and_can_override()
    {
        var json = $$$"""
            {"info": {"name": "C"}, "auth": {{{Bearer("tc")}}}, "item": [
              {"name": "top", "request": {"url": "https://a.test"}},
              {"name": "F", "auth": {{{Bearer("tf")}}}, "item": [
                {"name": "inF", "request": {"url": "https://a.test"}},
                {"name": "own", "request": {"url": "https://a.test", "auth": {{{Bearer("to")}}}}},
                {"name": "G", "auth": {"type": "noauth"}, "item": [{"name": "inG", "request": {"url": "https://a.test"}}]}
              ]}
            ]}
            """;

        var bundle = Run(json, out var report);

        var root = bundle.Collection!.Children;
        var f = Assert.IsType<ImportFolder>(root.Single(i => i is ImportFolder));
        var g = Assert.IsType<ImportFolder>(f.Children.Single(i => i is ImportFolder));
        Assert.Equal("tc", Req(root.Single(i => i is ImportRequest)).Token);
        var inF = f.Children.OfType<ImportRequest>().Single(r => r.Request.Name == "inF");
        var own = f.Children.OfType<ImportRequest>().Single(r => r.Request.Name == "own");
        Assert.Equal("tf", Req(inF).Token);
        Assert.Equal("to", Req(own).Token);
        Assert.Equal(RequestAuth.None, Req(Assert.Single(g.Children)));
        Assert.Contains(report.Entries, e => e.Severity == ImportSeverity.Info && e.Message == Yaapic.Localization.Strings.ImportAuthInherited(2));
    }

    [Fact]
    public void Requests_without_any_auth_stay_unauthenticated_and_silent()
    {
        var bundle = Run("""{"info": {"name": "C"}, "item": [{"name": "a", "request": {"url": "https://a.test"}}]}""", out var report);

        Assert.Equal(RequestAuth.None, Req(bundle.Collection!.Children[0]));
        Assert.Empty(report.Entries);
    }
}
