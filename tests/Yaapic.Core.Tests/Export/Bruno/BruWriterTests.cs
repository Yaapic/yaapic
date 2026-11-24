using Yaapic.Core.Export.Bruno;
using Yaapic.Core.Import;
using Yaapic.Core.Import.Bruno;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Tests.Export.Bruno;

public class BruWriterTests
{
    private static HttpRequestDefinition Req(string name = "r", HttpMethodKind method = HttpMethodKind.Get, string url = "https://x.test",
        IReadOnlyList<KeyValueEntry>? query = null, IReadOnlyList<KeyValueEntry>? headers = null, RequestBody? body = null,
        RequestAuth? auth = null, ScriptSet? scripts = null) =>
        new(Guid.NewGuid(), name, method, url, query ?? [], headers ?? [], body ?? RequestBody.None, auth ?? RequestAuth.None,
            RequestSettings.Default, scripts ?? ScriptSet.Empty);

    private static string Write(HttpRequestDefinition request, ImportReport? report = null) =>
        BruWriter.WriteRequest(request, 1, "r", report ?? new ImportReport());

    [Fact]
    public void WritesAGetWithParamsAndHeaders()
    {
        var text = Write(Req("List", url: "https://x.test/a?q=1", query: [new("q", "1"), new("off", "2", false)],
            headers: [new("Accept", "*/*"), new("X-Off", "1", false)]));

        Assert.Equal(
            "meta {\n  name: List\n  type: http\n  seq: 1\n}\n\n"
            + "get {\n  url: https://x.test/a?q=1\n  body: none\n  auth: none\n}\n\n"
            + "params:query {\n  q: 1\n  ~off: 2\n}\n\n"
            + "headers {\n  Accept: */*\n  ~X-Off: 1\n}\n",
            text);
    }

    [Fact]
    public void WritesAnIndentedMultiLineJsonBody()
    {
        var text = Write(Req(method: HttpMethodKind.Post, body: RequestBody.Raw(RawContentType.Json, "{\n\n  \"a\": 1\n}")));

        Assert.Contains("post {\n  url: https://x.test\n  body: json\n", text, StringComparison.Ordinal);
        Assert.EndsWith("body:json {\n  {\n\n    \"a\": 1\n  }\n}\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlAndJavaScriptBodiesAreText()
    {
        var text = Write(Req(body: RequestBody.Raw(RawContentType.Html, "<p/>")));

        Assert.Contains("body: text", text, StringComparison.Ordinal);
        Assert.EndsWith("body:text {\n  <p/>\n}\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesUrlEncodedAndMultipartWithAFile()
    {
        var form = Write(Req(body: RequestBody.UrlEncoded([new("a", "1"), new("b", "2", false)])));
        var multipart = Write(Req(body: RequestBody.FormData([new("t", "x"), new("f", "C:/a.png", true, FormFieldKind.File)])));

        Assert.Contains("body: formUrlEncoded", form, StringComparison.Ordinal);
        Assert.EndsWith("body:form-urlencoded {\n  a: 1\n  ~b: 2\n}\n", form, StringComparison.Ordinal);
        Assert.Contains("body: multipartForm", multipart, StringComparison.Ordinal);
        Assert.EndsWith("body:multipart-form {\n  t: x\n  f: @file(C:/a.png)\n}\n", multipart, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AuthKind.Basic, "basic", "auth:basic {\n  username: u\n  password: p\n}")]
    [InlineData(AuthKind.Digest, "digest", "auth:digest {\n  username: u\n  password: p\n}")]
    [InlineData(AuthKind.Bearer, "bearer", "auth:bearer {\n  token: t\n}")]
    [InlineData(AuthKind.ApiKey, "apikey", "auth:apikey {\n  key: k\n  value: v\n  placement: queryparams\n}")]
    public void WritesSimpleAuthModes(AuthKind kind, string mode, string block)
    {
        var auth = RequestAuth.None with
        {
            Kind = kind,
            Username = "u",
            Password = "p",
            Token = "t",
            Key = "k",
            Value = "v",
            Location = ApiKeyLocation.Query,
        };

        var text = Write(Req(auth: auth));

        Assert.Contains("  auth: " + mode + "\n", text, StringComparison.Ordinal);
        Assert.EndsWith(block + "\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesAwsV4()
    {
        var auth = RequestAuth.None with { Kind = AuthKind.AwsV4, AwsV4 = new AwsV4Settings("id", "key", "tok", "eu-west-1", "s3") };

        Assert.EndsWith(
            "auth:awsv4 {\n  accessKeyId: id\n  secretAccessKey: key\n  sessionToken: tok\n  service: s3\n  region: eu-west-1\n  profileName: \n}\n",
            Write(Req(auth: auth)), StringComparison.Ordinal);
    }

    [Fact]
    public void WritesOAuth2()
    {
        var o = OAuth2Settings.Default with
        {
            Grant = OAuth2Grant.Password,
            TokenUrl = "https://t",
            AuthorizationUrl = "https://a",
            ClientId = "c",
            ClientSecret = "s",
            Scope = "r w",
            Username = "u",
            Password = "p",
            Placement = OAuth2TokenPlacement.Query,
            ClientAuthentication = OAuth2ClientAuthentication.RequestBody,
        };

        var text = Write(Req(auth: RequestAuth.None with { Kind = AuthKind.OAuth2, OAuth2 = o }));

        Assert.Contains("  auth: oauth2\n", text, StringComparison.Ordinal);
        Assert.EndsWith(
            "auth:oauth2 {\n  grant_type: password\n  callback_url: http://127.0.0.1:8742/callback\n  authorization_url: https://a\n"
            + "  access_token_url: https://t\n  client_id: c\n  client_secret: s\n  scope: r w\n  username: u\n  password: p\n"
            + "  token_placement: query\n  token_header_prefix: Bearer\n  token_query_key: access_token\n  credentials_placement: body\n}\n",
            text, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesAScriptPairWithTheCommentLine()
    {
        var scripts = ScriptSet.Empty
            .With(ScriptPhase.PreRequest, new ScriptBlock(ScriptLanguage.JavaScript, "a();"))
            .With(ScriptPhase.PostResponse, new ScriptBlock(ScriptLanguage.JavaScript, "b();\nc();"));
        const string comment = "  // Exported from Yaapic: this script uses the yaapic API, rewrite it for Bruno.\n";

        Assert.EndsWith(
            "script:pre-request {\n" + comment + "  a();\n}\n\nscript:post-response {\n" + comment + "  b();\n  c();\n}\n",
            Write(Req(scripts: scripts)), StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsALuaScriptWithAWarning()
    {
        var report = new ImportReport();
        var scripts = ScriptSet.Empty.With(ScriptPhase.PreRequest, new ScriptBlock(ScriptLanguage.Lua, "x()"));

        var text = Write(Req(scripts: scripts), report);

        Assert.DoesNotContain("script:", text, StringComparison.Ordinal);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(report.Entries).Severity);
    }

    [Fact]
    public void DropsABinaryBodyWithAWarning()
    {
        var report = new ImportReport();

        var text = Write(Req(body: RequestBody.Binary("C:/a.bin")), report);

        Assert.Contains("body: none", text, StringComparison.Ordinal);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(report.Entries).Severity);
    }

    [Fact]
    public void JwtAuthBecomesNoneWithAWarning()
    {
        var report = new ImportReport();

        var text = Write(Req(auth: RequestAuth.None with { Kind = AuthKind.Jwt }), report);

        Assert.Contains("auth: none", text, StringComparison.Ordinal);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(report.Entries).Severity);
    }

    [Fact]
    public void WritesAMultiLineHeaderValue() =>
        Assert.Contains("headers {\n  X: '''\n  a\n  b\n  '''\n}", Write(Req(headers: [new("X", "a\r\nb")])), StringComparison.Ordinal);

    [Fact]
    public void TheParserReadsBackTheSameBlocksAndPairs()
    {
        var request = Req("Round", HttpMethodKind.Post, "https://x.test/p?a=1", [new("a", "1"), new("z", "3", false)],
            [new("X", "a\nb"), new("~odd", "v", false)], RequestBody.Raw(RawContentType.Json, "{\n  \"k\": 1\n}"),
            RequestAuth.None with { Kind = AuthKind.Bearer, Token = "t" },
            ScriptSet.Empty.With(ScriptPhase.PostResponse, new ScriptBlock(ScriptLanguage.JavaScript, "x();")));

        var file = BruParser.Parse(Write(request));

        Assert.Equal(["meta", "post", "params:query", "headers", "auth:bearer", "body:json", "script:post-response"],
            file.Blocks.Select(b => b.Name));
        Assert.Equal([("name", "Round"), ("type", "http"), ("seq", "1")], file.Get("meta")!.Pairs.Select(p => (p.Key, p.Value)));
        Assert.Equal([("a", "1", true), ("z", "3", false)], file.Get("params:query")!.Pairs.Select(p => (p.Key, p.Value, p.Enabled)));
        Assert.Equal("a\nb", file.Get("headers")!.Pairs[0].Value);
        Assert.Equal("{\n  \"k\": 1\n}", file.Get("body:json")!.Text);
        Assert.Equal("t", file.Get("auth:bearer")!.Pairs[0].Value);
        Assert.EndsWith("x();", file.Get("script:post-response")!.Text, StringComparison.Ordinal);
    }
}
