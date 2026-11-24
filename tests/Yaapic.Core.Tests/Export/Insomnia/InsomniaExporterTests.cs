using System.Text;
using Yaapic.Core.Export;
using Yaapic.Core.Export.Insomnia;
using Yaapic.Core.Import;
using Yaapic.Core.Import.Insomnia;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Export.Insomnia;

public class InsomniaExporterTests
{
    private static readonly InsomniaExporter Exporter = new();
    private static readonly Guid RootId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid FolderId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid RequestId = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid EnvId = new("00000000-0000-0000-0000-000000000004");

    private static HttpRequestDefinition Req(string url = "https://x.test", HttpMethodKind method = HttpMethodKind.Get,
        IReadOnlyList<KeyValueEntry>? query = null, IReadOnlyList<KeyValueEntry>? headers = null, RequestBody? body = null,
        RequestAuth? auth = null, ScriptSet? scripts = null, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "r", method, url, query ?? [], headers ?? [], body ?? RequestBody.None, auth ?? RequestAuth.None,
            RequestSettings.Default, scripts ?? ScriptSet.Empty);

    private static CollectionNode Root(IReadOnlyList<KeyValueEntry> variables, params CollectionNode[] children) =>
        CollectionNode.ForFolder(new FolderDefinition(RootId, "My API", "desc", variables), children);

    private static ExportResult Run(CollectionNode root, params EnvironmentDefinition[] environments) =>
        Exporter.Export(new ExportSource(root, environments, [], root.Name));

    private static string Text(ExportResult result) => Encoding.UTF8.GetString(result.Files[0].Content);

    private static DataNode Resource(HttpRequestDefinition request) =>
        DataNodeJson.Parse(Text(Run(Root([], CollectionNode.ForRequest(request)))))["resources"]![2]!;

    private static string Compact(DataNode? node) => DataNodeJson.Write(node!, indented: false);

    private static RequestAuth Of(AuthKind kind) => RequestAuth.None with { Kind = kind };

    [Fact]
    public void Identity()
    {
        Assert.Equal("insomnia", Exporter.Id);
        Assert.Equal("Insomnia (v4 JSON)", Exporter.DisplayName);
        Assert.Equal(".json", Exporter.FileExtension);
        Assert.True(Exporter.RequiresCollection);
    }

    [Fact]
    public void NeedsACollection() =>
        Assert.Throws<ExportException>(() => Exporter.Export(new ExportSource(null, [], [], "x")));

    [Fact]
    public void WritesTheExactDocument()
    {
        var request = Req("https://x.test/a?q=1", HttpMethodKind.Post, [new("q", "1"), new("off", "2", false)],
            [new("Accept", "*/*"), new("X-Off", "1", false)], RequestBody.Raw(RawContentType.Json, "{}"), id: RequestId);
        var sub = CollectionNode.ForFolder(new FolderDefinition(FolderId, "Sub", "", []), [CollectionNode.ForRequest(request)]);
        var environment = new EnvironmentDefinition(EnvId, "Prod", [new("host", "h"), new("off", "x", false)]);
        var result = Run(Root([new("baseUrl", "u"), new("off", "x", false), new("secret", "", true)], sub), environment);

        var file = Assert.Single(result.Files);
        Assert.Equal("my-api.insomnia.json", file.Path);
        var text = Text(result);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.Equal(
            "{\"_type\":\"export\",\"__export_format\":4,\"__export_source\":\"yaapic\",\"resources\":["
            + "{\"_id\":\"wrk_00000000000000000000000000000001\",\"_type\":\"workspace\",\"parentId\":null,\"name\":\"My API\",\"description\":\"desc\",\"scope\":\"collection\"},"
            + "{\"_id\":\"env_00000000000000000000000000000001\",\"_type\":\"environment\",\"parentId\":\"wrk_00000000000000000000000000000001\",\"name\":\"Base Environment\",\"data\":{\"baseUrl\":\"u\",\"secret\":\"\"},\"metaSortKey\":0},"
            + "{\"_id\":\"env_00000000000000000000000000000004\",\"_type\":\"environment\",\"parentId\":\"env_00000000000000000000000000000001\",\"name\":\"Prod\",\"data\":{\"host\":\"h\"},\"metaSortKey\":1},"
            + "{\"_id\":\"fld_00000000000000000000000000000002\",\"_type\":\"request_group\",\"parentId\":\"wrk_00000000000000000000000000000001\",\"name\":\"Sub\",\"description\":\"\",\"metaSortKey\":0},"
            + "{\"_id\":\"req_00000000000000000000000000000003\",\"_type\":\"request\",\"parentId\":\"fld_00000000000000000000000000000002\",\"name\":\"r\",\"description\":\"\",\"method\":\"POST\",\"url\":\"https://x.test/a?q=1\","
            + "\"parameters\":[{\"name\":\"off\",\"value\":\"2\",\"disabled\":true}],\"headers\":[{\"name\":\"Accept\",\"value\":\"*/*\"},{\"name\":\"X-Off\",\"value\":\"1\",\"disabled\":true}],"
            + "\"body\":{\"mimeType\":\"application/json\",\"text\":\"{}\"},\"authentication\":{},\"settingFollowRedirects\":\"on\",\"metaSortKey\":0}]}",
            Compact(DataNodeJson.Parse(text)));
    }

    [Fact]
    public void EmptyBaseEnvironmentIsStillWritten()
    {
        var resources = DataNodeJson.Parse(Text(Run(Root([]))))["resources"]!;
        Assert.Equal("{}", Compact(resources[1]!["data"]));
    }

    [Fact]
    public void FollowRedirectsOffAndPositions()
    {
        var off = Req(id: new Guid("00000000-0000-0000-0000-00000000000a")) with { Settings = new RequestSettings(false, 0) };
        var on = Req(id: new Guid("00000000-0000-0000-0000-00000000000b"));
        var parsed = DataNodeJson.Parse(Text(Run(Root([], CollectionNode.ForRequest(on), CollectionNode.ForRequest(off)))));
        Assert.Equal("off", parsed["resources"]![2]!.GetString("settingFollowRedirects"));
        Assert.Equal("on", parsed["resources"]![3]!.GetString("settingFollowRedirects"));
        Assert.Equal("0", Compact(parsed["resources"]![2]!["metaSortKey"]));
        Assert.Equal("1", Compact(parsed["resources"]![3]!["metaSortKey"]));
    }

    [Fact]
    public void BodyKinds()
    {
        Assert.Equal("{\"mimeType\":\"text/plain\",\"text\":\"t\"}", Compact(Resource(Req(body: RequestBody.Raw(RawContentType.Text, "t")))["body"]));
        Assert.Equal("{\"mimeType\":\"application/xml\",\"text\":\"t\"}", Compact(Resource(Req(body: RequestBody.Raw(RawContentType.Xml, "t")))["body"]));
        Assert.Equal("{\"mimeType\":\"text/html\",\"text\":\"t\"}", Compact(Resource(Req(body: RequestBody.Raw(RawContentType.Html, "t")))["body"]));
        Assert.Equal("{\"mimeType\":\"application/javascript\",\"text\":\"t\"}", Compact(Resource(Req(body: RequestBody.Raw(RawContentType.JavaScript, "t")))["body"]));
        Assert.Equal(
            "{\"mimeType\":\"application/x-www-form-urlencoded\",\"params\":[{\"name\":\"k\",\"value\":\"v\"},{\"name\":\"o\",\"value\":\"1\",\"disabled\":true}]}",
            Compact(Resource(Req(body: RequestBody.UrlEncoded([new("k", "v"), new("o", "1", false)])))["body"]));
        Assert.Equal(
            "{\"mimeType\":\"multipart/form-data\",\"params\":[{\"name\":\"k\",\"value\":\"v\"},{\"name\":\"f\",\"value\":\"\",\"type\":\"file\",\"fileName\":\"/a.bin\",\"disabled\":true}]}",
            Compact(Resource(Req(body: RequestBody.FormData([new("k", "v"), new("f", "/a.bin", false, FormFieldKind.File)])))["body"]));
        Assert.Equal("{\"mimeType\":\"application/octet-stream\",\"fileName\":\"/a.bin\"}", Compact(Resource(Req(body: RequestBody.Binary("/a.bin")))["body"]));
        Assert.Equal("{}", Compact(Resource(Req())["body"]));
    }

    [Fact]
    public void AuthKinds()
    {
        string A(RequestAuth auth) => Compact(Resource(Req(auth: auth))["authentication"]);

        Assert.Equal("{}", A(RequestAuth.None));
        Assert.Equal("{\"type\":\"basic\",\"username\":\"u\",\"password\":\"p\"}", A(Of(AuthKind.Basic) with { Username = "u", Password = "p" }));
        Assert.Equal("{\"type\":\"digest\",\"username\":\"u\",\"password\":\"p\"}", A(Of(AuthKind.Digest) with { Username = "u", Password = "p" }));
        Assert.Equal("{\"type\":\"bearer\",\"token\":\"t\"}", A(Of(AuthKind.Bearer) with { Token = "t" }));
        Assert.Equal("{\"type\":\"apikey\",\"key\":\"k\",\"value\":\"v\",\"addTo\":\"queryParams\"}",
            A(Of(AuthKind.ApiKey) with { Key = "k", Value = "v", Location = ApiKeyLocation.Query }));
        Assert.Equal("{\"type\":\"apikey\",\"key\":\"k\",\"value\":\"v\",\"addTo\":\"header\"}", A(Of(AuthKind.ApiKey) with { Key = "k", Value = "v" }));
        Assert.Equal("{\"type\":\"iam\",\"accessKeyId\":\"a\",\"secretAccessKey\":\"b\",\"sessionToken\":\"c\",\"region\":\"r\",\"service\":\"s\"}",
            A(Of(AuthKind.AwsV4) with { AwsV4 = new AwsV4Settings("a", "b", "c", "r", "s") }));

        var oauth = OAuth2Settings.Default with
        {
            Grant = OAuth2Grant.AuthorizationCode,
            TokenUrl = "t",
            AuthorizationUrl = "a",
            ClientId = "i",
            ClientSecret = "s",
            Scope = "x y",
            Audience = "au",
            Username = "u",
            Password = "p",
            RedirectUri = "r",
            ClientAuthentication = OAuth2ClientAuthentication.RequestBody,
        };
        Assert.Equal(
            "{\"type\":\"oauth2\",\"grantType\":\"authorization_code\",\"accessTokenUrl\":\"t\",\"authorizationUrl\":\"a\",\"clientId\":\"i\",\"clientSecret\":\"s\","
            + "\"scope\":\"x y\",\"audience\":\"au\",\"username\":\"u\",\"password\":\"p\",\"redirectUrl\":\"r\",\"credentialsInBody\":true,\"headerPrefix\":\"Bearer\"}",
            A(Of(AuthKind.OAuth2) with { OAuth2 = oauth }));
        Assert.Contains("\"grantType\":\"password\"", A(Of(AuthKind.OAuth2) with { OAuth2 = oauth with { Grant = OAuth2Grant.Password } }));
        Assert.Contains("\"grantType\":\"client_credentials\"", A(Of(AuthKind.OAuth2) with { OAuth2 = oauth with { Grant = OAuth2Grant.ClientCredentials } }));
    }

    [Fact]
    public void JwtIsLeftOutWithAWarning()
    {
        var root = Root([], CollectionNode.ForRequest(Req(auth: Of(AuthKind.Jwt) with { Jwt = JwtSettings.Default })));
        var result = Run(root);
        Assert.Equal("{}", Compact(DataNodeJson.Parse(Text(result))["resources"]![2]!["authentication"]));
        var entry = Assert.Single(result.Report.Entries);
        Assert.Equal(ImportSeverity.Warning, entry.Severity);
        Assert.Contains("jwt", entry.Message);
    }

    [Theory]
    [InlineData("{{name}}", "{{ _.name }}")]
    [InlineData("{{ a.b }}", "{{ _.a.b }}")]
    [InlineData("{{my-var}}", "{{ _['my-var'] }}")]
    [InlineData("{{it's}}", "{{it's}}")]
    [InlineData("{{$guid}}", "{% uuid 'v4' %}")]
    [InlineData("{{$isoTimestamp}}", "{% now 'iso-8601', '' %}")]
    [InlineData("{{$timestamp}}", "{% now 'unix', '' %}")]
    [InlineData("a {{x}} b {{y}}", "a {{ _.x }} b {{ _.y }}")]
    [InlineData("plain", "plain")]
    public void TranslatesTemplates(string input, string expected) =>
        Assert.Equal(expected, InsomniaTemplateWriter.Translate(input, "p", new ImportReport()));

    [Fact]
    public void UnknownDynamicVariablesWarnOnce()
    {
        var report = new ImportReport();
        Assert.Equal("{{$randomInt}}{{$randomInt}}", InsomniaTemplateWriter.Translate("{{$randomInt}}{{$randomInt}}", "p", report));
        var entry = Assert.Single(report.Entries);
        Assert.Equal(ImportSeverity.Warning, entry.Severity);
        Assert.Contains("{{$randomInt}}", entry.Message);
    }

    [Fact]
    public void TemplatesAreAppliedToEveryWrittenString()
    {
        var request = Req("{{baseUrl}}/x", headers: [new("{{h}}", "{{v}}")], body: RequestBody.Raw(RawContentType.Text, "{{b}}"),
            auth: Of(AuthKind.Bearer) with { Token = "{{tok}}" });
        var text = Text(Run(Root([new("k", "{{other}}")], CollectionNode.ForRequest(request))));
        foreach (var name in new[] { "baseUrl", "h", "v", "b", "tok", "other" })
        {
            Assert.Contains("{{ _." + name + " }}", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ScriptsAreCountedInOneWarning()
    {
        var block = new ScriptBlock(ScriptLanguage.JavaScript, "x");
        var withScripts = ScriptSet.Empty with { PreRequest = block, PostResponse = block };
        var folder = CollectionNode.ForFolder(new FolderDefinition(FolderId, "F", "", [], ScriptSet.Empty with { PreRequest = block }),
            [CollectionNode.ForRequest(Req(scripts: withScripts))]);
        var entry = Assert.Single(Run(Root([], folder)).Report.Entries);
        Assert.Equal(ImportSeverity.Warning, entry.Severity);
        Assert.Contains("3 ", entry.Message);
    }

    [Fact]
    public void NoScriptsNoWarning() => Assert.Empty(Run(Root([], CollectionNode.ForRequest(Req()))).Report.Entries);

    [Fact]
    public void RoundTripThroughTheImporter()
    {
        var get = Req("https://api.example.com/v1/users", query: [new("a", "1"), new("b", "2", false)],
            headers: [new("Accept", "*/*"), new("X-Off", "1", false)],
            auth: Of(AuthKind.Basic) with { Username = "u", Password = "{{pw}}" });
        var post = Req("{{baseUrl}}/users", HttpMethodKind.Post, body: RequestBody.Raw(RawContentType.Json, """{"a":1}"""));
        var form = Req("http://h.test/f", HttpMethodKind.Put, body: RequestBody.UrlEncoded([new("k", "v"), new("k2", "v2", false)]));
        var sub = CollectionNode.ForFolder(new FolderDefinition(Guid.NewGuid(), "Sub", "", []), [CollectionNode.ForRequest(form)]);
        var root = Root([new("baseUrl", "https://x")], sub, CollectionNode.ForRequest(get), CollectionNode.ForRequest(post));

        var bundle = new InsomniaImporter().Import(ImportInput.FromText("c.json", Text(Run(root))),
            new ImportContext(SequentialIds.Create(), ImportOptions.Default));

        Assert.Equal("My API", bundle.Collection!.Folder.Name);
        var folder = Assert.IsType<ImportFolder>(bundle.Collection.Children.OfType<ImportFolder>().Single());
        AssertSame(form, Assert.IsType<ImportRequest>(Assert.Single(folder.Children)).Request);
        var requests = bundle.Collection.Children.OfType<ImportRequest>().Select(r => r.Request).ToList();
        AssertSame(get, requests.Single(r => r.Method == HttpMethodKind.Get));
        AssertSame(post, requests.Single(r => r.Method == HttpMethodKind.Post));
        Assert.Equal(get.Auth.Password, requests.Single(r => r.Method == HttpMethodKind.Get).Auth.Password);
    }

    private static void AssertSame(HttpRequestDefinition expected, HttpRequestDefinition actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Method, actual.Method);
        Assert.Equal(expected.Url, actual.Url);
        Assert.Equal(expected.QueryParams.OrderBy(q => q.Key), actual.QueryParams.OrderBy(q => q.Key));
        Assert.Equal(expected.Headers, actual.Headers);
        Assert.Equal(expected.Body, actual.Body);
    }
}
