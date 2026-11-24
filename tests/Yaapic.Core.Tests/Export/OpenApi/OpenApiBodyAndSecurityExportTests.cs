using Yaapic.Core.Export;
using Yaapic.Core.Export.OpenApi;
using Yaapic.Core.Import;
using Yaapic.Core.Import.OpenApi;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Export.OpenApi;

public class OpenApiBodyAndSecurityExportTests
{
    private static HttpRequestDefinition Req(string name, string url, RequestBody? body = null, RequestAuth? auth = null) =>
        new(Guid.NewGuid(), name, HttpMethodKind.Post, url, [], [], body ?? RequestBody.None, auth ?? RequestAuth.None, RequestSettings.Default);

    private static CollectionNode Root(params HttpRequestDefinition[] requests) =>
        CollectionNode.ForFolder(new FolderDefinition(Guid.NewGuid(), "My API", "", []), [.. requests.Select(CollectionNode.ForRequest)]);

    private static DataNode Doc(params HttpRequestDefinition[] requests)
    {
        var root = Root(requests);
        return OpenApiDocumentBuilder.Build(new ExportSource(root, [], [], root.Name), new ImportReport());
    }

    private static string Json(DataNode? node) => DataNodeJson.Write(node!, indented: false);

    private static string BodyOf(RequestBody body) => Json(Doc(Req("a", "/a", body))["paths"]!["/a"]!["post"]!["requestBody"]);

    private static RequestAuth Auth(AuthKind kind, string key = "", ApiKeyLocation location = ApiKeyLocation.Header, OAuth2Settings? oauth = null) =>
        RequestAuth.None with { Kind = kind, Key = key, Location = location, OAuth2 = oauth };

    private static string Schemes(params RequestAuth[] auths) =>
        Json(Doc([.. auths.Select((a, i) => Req("r" + i, "/r" + i, auth: a))])["components"]);

    [Fact]
    public void ValidJsonBodyHasInferredSchemaAndExample() =>
        Assert.Equal(
            """{"content":{"application/json":{"schema":{"type":"object","properties":{"a":{"type":"integer"}}},"example":{"a":1}}}}""",
            BodyOf(RequestBody.Raw(RawContentType.Json, "{\"a\":1}")));

    [Fact]
    public void JsonBodyWithVariablesFallsBackToAString() =>
        Assert.Equal(
            """{"content":{"application/json":{"schema":{"type":"string"},"example":"{\"a\":{{x}}}"}}}""",
            BodyOf(RequestBody.Raw(RawContentType.Json, "{\"a\":{{x}}}")));

    [Fact]
    public void XmlBodyIsAString() =>
        Assert.Equal(
            """{"content":{"application/xml":{"schema":{"type":"string"},"example":"<a/>"}}}""",
            BodyOf(RequestBody.Raw(RawContentType.Xml, "<a/>")));

    [Fact]
    public void UrlEncodedBodyKeepsEnabledFields() =>
        Assert.Equal(
            """{"content":{"application/x-www-form-urlencoded":{"schema":{"type":"object","properties":{"k":{"type":"string"}}},"example":{"k":"v"}}}}""",
            BodyOf(RequestBody.UrlEncoded([new("k", "v"), new("off", "x", false)])));

    [Fact]
    public void MultipartBodyMarksFilesAsBinaryWithoutExample() =>
        Assert.Equal(
            """{"content":{"multipart/form-data":{"schema":{"type":"object","properties":{"t":{"type":"string"},"f":{"type":"string","format":"binary"}}}}}}""",
            BodyOf(RequestBody.FormData([new("t", "v"), new("f", "a.png", true, FormFieldKind.File)])));

    [Fact]
    public void BinaryBodyIsOctetStream() =>
        Assert.Equal(
            """{"content":{"application/octet-stream":{"schema":{"type":"string","format":"binary"}}}}""",
            BodyOf(RequestBody.Binary("a.bin")));

    [Fact]
    public void NoBodyMeansNoRequestBody() =>
        Assert.Null(Doc(Req("a", "/a"))["paths"]!["/a"]!["post"]!["requestBody"]);

    [Fact]
    public void InferSchemaCoversEveryKind() =>
        Assert.Equal(
            """{"type":"object","properties":{"o":{"type":"object","properties":{"i":{"type":"integer"}}},"a":{"type":"array","items":{"type":"number"}},"e":{"type":"array","items":{}},"b":{"type":"boolean"},"n":{"nullable":true},"s":{"type":"string"},"x":{"type":"number"}}}""",
            Json(OpenApiBodyWriter.InferSchema(DataNodeJson.Parse("""{"o":{"i":3},"a":[1.5,2],"e":[],"b":true,"n":null,"s":"t","x":1e3}"""))));

    [Fact]
    public void InferSchemaStopsAtDepthEight()
    {
        var schema = Json(OpenApiBodyWriter.InferSchema(DataNodeJson.Parse("[[[[[[[[[1]]]]]]]]]")));

        Assert.EndsWith("\"items\":{}}}}}}}}}", schema);
        Assert.DoesNotContain("integer", schema);
    }

    [Fact]
    public void BasicBearerJwtAndDigestSchemes() =>
        Assert.Equal(
            """{"securitySchemes":{"basicAuth":{"type":"http","scheme":"basic"},"bearerAuth":{"type":"http","scheme":"bearer"},"jwtAuth":{"type":"http","scheme":"bearer","bearerFormat":"JWT"},"digestAuth":{"type":"http","scheme":"digest"}}}""",
            Schemes(Auth(AuthKind.Basic), Auth(AuthKind.Bearer), Auth(AuthKind.Jwt), Auth(AuthKind.Digest)));

    [Fact]
    public void JwtAloneIsNamedBearerAuth() =>
        Assert.Equal(
            """{"securitySchemes":{"bearerAuth":{"type":"http","scheme":"bearer","bearerFormat":"JWT"}}}""",
            Schemes(Auth(AuthKind.Jwt)));

    [Fact]
    public void ApiKeysAreDeduplicatedAndNumbered() =>
        Assert.Equal(
            """{"securitySchemes":{"apiKeyAuth":{"type":"apiKey","name":"X-Key","in":"header"},"apiKeyAuth2":{"type":"apiKey","name":"key","in":"query"}}}""",
            Schemes(Auth(AuthKind.ApiKey, "X-Key"), Auth(AuthKind.ApiKey, "X-Key"), Auth(AuthKind.ApiKey, "key", ApiKeyLocation.Query)));

    [Fact]
    public void AwsV4Scheme() =>
        Assert.Equal(
            """{"securitySchemes":{"awsSigV4":{"type":"apiKey","name":"Authorization","in":"header","description":"AWS Signature Version 4"}}}""",
            Schemes(Auth(AuthKind.AwsV4)));

    [Fact]
    public void OAuth2FlowsAndScopesInTheRequirement()
    {
        var code = OAuth2Settings.Default with { Grant = OAuth2Grant.AuthorizationCode, TokenUrl = "https://t", AuthorizationUrl = "https://a", Scope = "read write" };
        var password = OAuth2Settings.Default with { Grant = OAuth2Grant.Password, TokenUrl = "https://t" };
        var doc = Doc(Req("a", "/a", auth: Auth(AuthKind.OAuth2, oauth: code)), Req("b", "/b", auth: Auth(AuthKind.OAuth2, oauth: password)));

        Assert.Equal(
            """{"securitySchemes":{"oauth2Auth":{"type":"oauth2","flows":{"authorizationCode":{"authorizationUrl":"https://a","tokenUrl":"https://t","scopes":{"read":"","write":""}}}},"oauth2Auth2":{"type":"oauth2","flows":{"password":{"tokenUrl":"https://t","scopes":{}}}}}}""",
            Json(doc["components"]));
        Assert.Equal("""[{"oauth2Auth":["read","write"]}]""", Json(doc["paths"]!["/a"]!["post"]!["security"]));
        Assert.Equal("""[{"oauth2Auth2":[]}]""", Json(doc["paths"]!["/b"]!["post"]!["security"]));
        Assert.Equal(["openapi", "info", "paths", "components"], doc.Members.Select(m => m.Key));
    }

    [Fact]
    public void NoAuthMeansNoComponentsAndNoSecurity()
    {
        var doc = Doc(Req("a", "/a"));

        Assert.Null(doc["components"]);
        Assert.Null(doc["paths"]!["/a"]!["post"]!["security"]);
    }

    [Fact]
    public void ExporterNamesAndEncoding()
    {
        var root = Root(Req("Hi", "https://x.test/a", RequestBody.Raw(RawContentType.Json, "{}")));
        var source = new ExportSource(root, [], [], "My API");

        var json = new OpenApiJsonExporter().Export(source).Files.Single();
        var yaml = new OpenApiYamlExporter().Export(source).Files.Single();

        Assert.Equal("my-api.openapi.json", json.Path);
        Assert.Equal("my-api.openapi.yaml", yaml.Path);
        Assert.Equal(("openapi-json", "openapi-yaml"), (new OpenApiJsonExporter().Id, new OpenApiYamlExporter().Id));
        Assert.NotEqual(0xEF, json.Content[0]);
        Assert.NotEqual(0xEF, yaml.Content[0]);
        Assert.EndsWith("}\n", System.Text.Encoding.UTF8.GetString(json.Content));
        Assert.StartsWith("openapi: ", System.Text.Encoding.UTF8.GetString(yaml.Content));
    }

    [Fact]
    public void ExportThenImportKeepsMethodsNamesPathsAndBodyKinds()
    {
        var root = Root(
            Req("Create user", "{{baseUrl}}/users", RequestBody.Raw(RawContentType.Json, "{\"a\":1}")),
            Req("Upload", "{{baseUrl}}/files", RequestBody.FormData([new("f", "a.png", true, FormFieldKind.File)])));
        var text = System.Text.Encoding.UTF8.GetString(
            new OpenApiJsonExporter().Export(new ExportSource(root, [], [], "My API")).Files.Single().Content);

        var importer = new OpenApiImporter(new StructuredTextReader(new NoYaml()));
        var bundle = importer.Import(ImportInput.FromText("api.json", text), new ImportContext(SequentialIds.Create(), ImportOptions.Default));

        var requests = Assert.IsType<ImportFolder>(bundle.Collection).Children.Cast<ImportRequest>().Select(r => r.Request).ToArray();
        Assert.Equal(["Create user", "Upload"], requests.Select(r => r.Name));
        Assert.All(requests, r => Assert.Equal(HttpMethodKind.Post, r.Method));
        Assert.Equal([BodyKind.Raw, BodyKind.FormData], requests.Select(r => r.Body.Kind));
    }

    private sealed class NoYaml : IYamlTreeReader
    {
        public DataNode Parse(string yaml) => throw new NotSupportedException();
    }
}
