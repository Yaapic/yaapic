using Yaapic.Core.Import;
using Yaapic.Core.Import.Bruno;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests.Import.Bruno;

public class BrunoRequestsTests
{
    private static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static (HttpRequestDefinition? Request, ImportReport Report, BruAuthMode Mode) Map(string bru)
    {
        ImportReport report = new();
        var request = BrunoRequests.Map(BruParser.Parse(bru), "r", Id, report, out var mode);
        return (request, report, mode);
    }

    private static HttpRequestDefinition MapOk(string bru) => Map(bru).Request!;

    private static RequestBody BodyOf(string mode, string blocks) =>
        MapOk($$"""
            get {
              url: http://a.test
              body: {{mode}}
            }

            {{blocks}}
            """).Body;

    private static RequestAuth AuthOf(string mode, string block) =>
        MapOk($$"""
            get {
              url: http://a.test
              auth: {{mode}}
            }

            {{block}}
            """).Auth;

    [Fact]
    public void Maps_the_sample_request()
    {
        var (r, report, mode) = Map("""
            meta {
              name: Create user
              type: http
              seq: 2
            }

            post {
              url: {{baseUrl}}/users?x=1
              body: json
              auth: bearer
            }

            params:query {
              page: 1
              ~limit: 10
            }

            headers {
              Content-Type: application/json
              ~X-Off: 1
            }

            auth:bearer {
              token: {{token}}
            }

            body:json {
              {
                "name": "a"
              }
            }
            """);

        Assert.NotNull(r);
        Assert.Equal(Id, r.Id);
        Assert.Equal("Create user", r.Name);
        Assert.Equal(HttpMethodKind.Post, r.Method);
        Assert.Equal("{{baseUrl}}/users", r.Url);
        Assert.Equal([new KeyValueEntry("page", "1"), new KeyValueEntry("limit", "10", false)], r.QueryParams);
        Assert.Equal([new KeyValueEntry("Content-Type", "application/json"), new KeyValueEntry("X-Off", "1", false)], r.Headers);
        Assert.Equal(RequestBody.Raw(RawContentType.Json, "{\n  \"name\": \"a\"\n}"), r.Body);
        Assert.Equal(RequestAuth.None with { Kind = AuthKind.Bearer, Token = "{{token}}" }, r.Auth);
        Assert.Equal(BruAuthMode.Mapped, mode);
        Assert.Equal(RequestSettings.Default, r.Settings);
        Assert.DoesNotContain(report.Entries, e => e.Severity != ImportSeverity.Info);
    }

    [Theory]
    [InlineData("get", HttpMethodKind.Get)]
    [InlineData("post", HttpMethodKind.Post)]
    [InlineData("put", HttpMethodKind.Put)]
    [InlineData("patch", HttpMethodKind.Patch)]
    [InlineData("delete", HttpMethodKind.Delete)]
    [InlineData("head", HttpMethodKind.Head)]
    [InlineData("options", HttpMethodKind.Options)]
    public void Maps_each_verb(string verb, HttpMethodKind kind) =>
        Assert.Equal(kind, MapOk($"{verb} {{\n  url: http://a.test\n}}").Method);

    [Theory]
    [InlineData("trace")]
    [InlineData("connect")]
    public void Unsupported_verb_is_skipped(string verb)
    {
        var (r, report, _) = Map($"{verb} {{\n  url: http://a.test\n}}");
        Assert.Null(r);
        Assert.Equal(ImportSeverity.Skipped, Assert.Single(report.Entries).Severity);
    }

    [Fact]
    public void File_without_verb_is_skipped()
    {
        var (r, report, _) = Map("meta {\n  name: x\n}");
        Assert.Null(r);
        Assert.Equal(ImportSeverity.Skipped, Assert.Single(report.Entries).Severity);
    }

    [Fact]
    public void Name_falls_back_to_the_method_and_url() =>
        Assert.Equal("GET a.test/users", MapOk("get {\n  url: http://a.test/users?x=1\n}").Name);

    [Fact]
    public void Query_block_overrides_the_url_query()
    {
        var r = MapOk("get {\n  url: http://a.test/x?a=1&b=2\n}\n\nparams:query {\n  c: 3\n}");
        Assert.Equal("http://a.test/x", r.Url);
        Assert.Equal([new KeyValueEntry("c", "3")], r.QueryParams);
    }

    [Fact]
    public void Url_query_is_kept_without_a_query_block() =>
        Assert.Equal(
            [new KeyValueEntry("a", "1")],
            MapOk("get {\n  url: http://a.test/x?a=1\n}").QueryParams);

    [Fact]
    public void Path_params_are_substituted_and_missing_ones_become_variables() =>
        Assert.Equal(
            "http://a.test/users/42/posts/{{pid}}",
            MapOk("get {\n  url: http://a.test/users/:id/posts/:pid\n}\n\nparams:path {\n  id: 42\n}").Url);

    [Fact]
    public void Process_env_is_warned_once()
    {
        var (r, report, _) = Map("get {\n  url: {{process.env.HOST}}/x\n}\n\nheaders {\n  X: {{process.env.KEY}}\n}");
        Assert.Equal("{{process.env.HOST}}/x", r!.Url);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(report.Entries).Severity);
    }

    [Fact]
    public void Json_text_xml_bodies()
    {
        Assert.Equal(RequestBody.Raw(RawContentType.Json, "{}"), BodyOf("json", "body:json {\n  {}\n}"));
        Assert.Equal(RequestBody.Raw(RawContentType.Text, "hi"), BodyOf("text", "body:text {\n  hi\n}"));
        Assert.Equal(RequestBody.Raw(RawContentType.Xml, "<a/>"), BodyOf("xml", "body:xml {\n  <a/>\n}"));
    }

    [Fact]
    public void Sparql_adds_a_content_type_unless_present()
    {
        var r = MapOk("get {\n  url: http://a.test\n  body: sparql\n}\n\nbody:sparql {\n  select\n}");
        Assert.Equal(RequestBody.Raw(RawContentType.Text, "select"), r.Body);
        Assert.Equal([new KeyValueEntry("Content-Type", "application/sparql-query")], r.Headers);

        var kept = MapOk("get {\n  url: http://a.test\n  body: sparql\n}\n\nheaders {\n  content-type: x/y\n}\n\nbody:sparql {\n  s\n}");
        Assert.Equal([new KeyValueEntry("content-type", "x/y")], kept.Headers);
    }

    [Fact]
    public void Graphql_with_and_without_variables()
    {
        var (r, report, _) = Map("""
            meta {
              type: graphql
            }

            post {
              url: http://a.test
              body: graphql
            }

            body:graphql {
              { me { id } }
            }

            body:graphql:vars {
              {"a": 1}
            }
            """);
        Assert.Equal(RequestBody.Raw(RawContentType.Json, """{"query":"{ me { id } }","variables":{"a":1}}"""), r!.Body);
        Assert.Equal([new KeyValueEntry("Content-Type", "application/json")], r.Headers);
        Assert.Equal(ImportSeverity.Info, Assert.Single(report.Entries).Severity);

        var noVars = MapOk("post {\n  url: http://a.test\n  body: graphql\n}\n\nbody:graphql {\n  q\n}");
        Assert.Equal("""{"query":"q","variables":{}}""", noVars.Body.Text);

        var badVars = MapOk("post {\n  url: http://a.test\n  body: graphql\n}\n\nbody:graphql {\n  q\n}\n\nbody:graphql:vars {\n  not json\n}");
        Assert.Equal("""{"query":"q","variables":{}}""", badVars.Body.Text);
    }

    [Fact]
    public void Form_url_encoded_keeps_disabled_pairs() =>
        Assert.Equal(
            RequestBody.UrlEncoded([new FormField("a", "1"), new FormField("b", "2", false)]),
            BodyOf("formUrlEncoded", "body:form-urlencoded {\n  a: 1\n  ~b: 2\n}"));

    [Fact]
    public void Multipart_maps_files_and_text()
    {
        var r = MapOk("post {\n  url: http://a.test\n  body: multipartForm\n}\n\nbody:multipart-form {\n  t: hello\n  f: @file(C:/x.png) @contentType(image/png)\n  ~g: @file(y.txt)\n}");
        Assert.Equal(
            RequestBody.FormData([
                new FormField("t", "hello"),
                new FormField("f", "C:/x.png", true, FormFieldKind.File),
                new FormField("g", "y.txt", false, FormFieldKind.File),
            ]),
            r.Body);
    }

    [Fact]
    public void File_body_takes_the_first_file_reference()
    {
        var (r, report, _) = Map("post {\n  url: http://a.test\n  body: file\n}\n\nbody:file {\n  file: @file(a.bin) @contentType(x/y)\n  ~file: @file(b.bin)\n}");
        Assert.Equal(RequestBody.Binary("a.bin"), r!.Body);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(report.Entries).Severity);

        Assert.Equal(RequestBody.None, BodyOf("file", "body:file {\n  x: y\n}"));
    }

    [Fact]
    public void None_and_missing_mode_have_no_body()
    {
        Assert.Equal(RequestBody.None, BodyOf("none", ""));
        Assert.Equal(RequestBody.None, MapOk("get {\n  url: http://a.test\n}").Body);
    }

    [Fact]
    public void Unknown_body_mode_warns()
    {
        var (r, report, _) = Map("get {\n  url: http://a.test\n  body: weird\n}");
        Assert.Equal(RequestBody.None, r!.Body);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(report.Entries).Severity);
    }

    [Theory]
    [InlineData("basic", AuthKind.Basic)]
    [InlineData("digest", AuthKind.Digest)]
    public void Username_password_modes(string mode, AuthKind kind) =>
        Assert.Equal(
            RequestAuth.None with { Kind = kind, Username = "u", Password = "p" },
            AuthOf(mode, $"auth:{mode} {{\n  username: u\n  password: p\n}}"));

    [Fact]
    public void Awsv4_maps_its_settings() =>
        Assert.Equal(
            RequestAuth.None with { Kind = AuthKind.AwsV4, AwsV4 = new AwsV4Settings("ak", "sk", "st", "eu-west-1", "s3") },
            AuthOf("awsv4", "auth:awsv4 {\n  accessKeyId: ak\n  secretAccessKey: sk\n  sessionToken: st\n  service: s3\n  region: eu-west-1\n}"));

    [Theory]
    [InlineData("queryparams", ApiKeyLocation.Query)]
    [InlineData("header", ApiKeyLocation.Header)]
    public void Apikey_placement(string placement, ApiKeyLocation location) =>
        Assert.Equal(
            RequestAuth.None with { Kind = AuthKind.ApiKey, Key = "k", Value = "{{v}}", Location = location },
            AuthOf("apikey", $"auth:apikey {{\n  key: k\n  value: {{{{v}}}}\n  placement: {placement}\n}}"));

    [Fact]
    public void Oauth2_maps_every_field()
    {
        var auth = AuthOf("oauth2", """
            auth:oauth2 {
              grant_type: authorization_code
              access_token_url: http://a.test/token
              authorization_url: http://a.test/auth
              client_id: id
              client_secret: {{s}}
              scope: read
              username: u
              password: p
              callback_url: http://127.0.0.1/cb
              token_placement: query
              token_header_prefix: Token
              token_query_key: tk
              credentials_placement: body
            }
            """);
        Assert.Equal(
            RequestAuth.None with
            {
                Kind = AuthKind.OAuth2,
                OAuth2 = new OAuth2Settings(
                    OAuth2Grant.AuthorizationCode, "http://a.test/token", "http://a.test/auth", "id", "{{s}}", "read", "", "u", "p",
                    "http://127.0.0.1/cb", OAuth2ClientAuthentication.RequestBody, OAuth2TokenPlacement.Query, "Token", "tk"),
            },
            auth);
    }

    [Fact]
    public void Oauth2_defaults_and_unknown_grant()
    {
        var d = OAuth2Settings.Default;
        var auth = AuthOf("oauth2", "auth:oauth2 {\n  grant_type: client_credentials\n}");
        Assert.Equal(
            d with { Grant = OAuth2Grant.ClientCredentials, RedirectUri = "" },
            auth.OAuth2);

        var (r, report, mode) = Map("get {\n  url: http://a.test\n  auth: oauth2\n}\n\nauth:oauth2 {\n  grant_type: implicit\n}");
        Assert.Equal(RequestAuth.None, r!.Auth);
        Assert.Equal(BruAuthMode.None, mode);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(report.Entries).Severity);
    }

    [Theory]
    [InlineData("wsse")]
    [InlineData("ntlm")]
    public void Unsupported_auth_warns(string mode)
    {
        var (r, report, _) = Map($"get {{\n  url: http://a.test\n  auth: {mode}\n}}");
        Assert.Equal(RequestAuth.None, r!.Auth);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(report.Entries).Severity);
    }

    [Fact]
    public void Inherit_is_reported_to_the_caller()
    {
        var (r, _, mode) = Map("get {\n  url: http://a.test\n  auth: inherit\n}");
        Assert.Equal(RequestAuth.None, r!.Auth);
        Assert.Equal(BruAuthMode.Inherit, mode);
    }

    [Fact]
    public void Literal_credentials_add_the_plain_text_info_once()
    {
        var (_, report, _) = Map("get {\n  url: http://a.test\n  auth: bearer\n}\n\nauth:bearer {\n  token: abc\n}");
        Assert.Equal(ImportSeverity.Info, Assert.Single(report.Entries).Severity);
    }

    [Fact]
    public void Grpc_is_skipped_and_graphql_type_accepted()
    {
        var (r, report, _) = Map("meta {\n  name: g\n  type: grpc\n}\n\npost {\n  url: x\n}");
        Assert.Null(r);
        Assert.Equal(ImportSeverity.Skipped, Assert.Single(report.Entries).Severity);

        Assert.NotNull(MapOk("meta {\n  type: graphql\n}\n\npost {\n  url: http://a.test\n}"));
    }

    [Fact]
    public void Docs_and_assert_are_reported()
    {
        var (_, report, _) = Map("get {\n  url: http://a.test\n}\n\nassert {\n  res.status: eq 200\n  res.body: isJson\n}\n\ndocs {\n  hello\n}");
        Assert.Equal(
            [ImportSeverity.Info, ImportSeverity.Warning],
            report.Entries.Select(e => e.Severity));
    }
}
