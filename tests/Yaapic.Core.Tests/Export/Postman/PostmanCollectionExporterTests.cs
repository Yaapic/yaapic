using System.Text;
using Yaapic.Core.Export;
using Yaapic.Core.Export.Postman;
using Yaapic.Core.Import;
using Yaapic.Core.Import.Postman;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Export.Postman;

public class PostmanCollectionExporterTests
{
    private static readonly PostmanCollectionExporter Exporter = new();

    private static HttpRequestDefinition Req(string name, string url = "https://x.test", HttpMethodKind method = HttpMethodKind.Get,
        IReadOnlyList<KeyValueEntry>? query = null, IReadOnlyList<KeyValueEntry>? headers = null, RequestBody? body = null) =>
        new(Guid.NewGuid(), name, method, url, query ?? [], headers ?? [], body ?? RequestBody.None, RequestAuth.None, RequestSettings.Default);

    private static CollectionNode Root(string description, params CollectionNode[] children) =>
        CollectionNode.ForFolder(new FolderDefinition(new Guid("00000000-0000-0000-0000-000000000001"), "My API", description, []), children);

    private static ExportResult Run(CollectionNode root) => Exporter.Export(new ExportSource(root, [], [], root.Name));

    private static string Text(ExportResult result) => Encoding.UTF8.GetString(result.Files[0].Content);

    private static DataNode RequestNode(HttpRequestDefinition request) =>
        DataNodeJson.Parse(Text(Run(Root("", CollectionNode.ForRequest(request)))))["item"]![0]!["request"]!;

    private static string Compact(DataNode? node) => DataNodeJson.Write(node!, indented: false);

    private static string UrlOf(string url, params KeyValueEntry[] query) => Compact(RequestNode(Req("r", url, query: query))["url"]);

    private static string BodyOf(RequestBody body) => Compact(RequestNode(Req("r", method: HttpMethodKind.Post, body: body))["body"]);

    [Fact]
    public void Metadata()
    {
        Assert.Equal("postman", Exporter.Id);
        Assert.Equal("Postman collection (v2.1)", Exporter.DisplayName);
        Assert.Equal(".json", Exporter.FileExtension);
        Assert.True(Exporter.RequiresCollection);
        Assert.Throws<ExportException>(() => Exporter.Export(new ExportSource(null, [], [], "Environments")));
    }

    [Fact]
    public void WritesTheExactDocument()
    {
        var list = Req("list", "https://api.example.com:8080/v1/users/1",
            query: [new("x", "1"), new("y", "2", false)], headers: [new("Accept", "*/*")]);
        var root = Root("desc", CollectionNode.ForFolder(new FolderDefinition(Guid.NewGuid(), "Sub", "", []), [CollectionNode.ForRequest(list)]));
        var result = Run(root);

        Assert.Equal("my-api.postman_collection.json", Assert.Single(result.Files).Path);
        Assert.Empty(result.Report.Entries);
        Assert.Equal("""
            {
              "info": {
                "_postman_id": "00000000-0000-0000-0000-000000000001",
                "name": "My API",
                "description": "desc",
                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
              },
              "item": [
                {
                  "name": "Sub",
                  "item": [
                    {
                      "name": "list",
                      "request": {
                        "method": "GET",
                        "header": [
                          {
                            "key": "Accept",
                            "value": "*/*",
                            "type": "text"
                          }
                        ],
                        "url": {
                          "raw": "https://api.example.com:8080/v1/users/1?x=1",
                          "protocol": "https",
                          "host": [
                            "api",
                            "example",
                            "com"
                          ],
                          "port": "8080",
                          "path": [
                            "v1",
                            "users",
                            "1"
                          ],
                          "query": [
                            {
                              "key": "x",
                              "value": "1"
                            },
                            {
                              "key": "y",
                              "value": "2",
                              "disabled": true
                            }
                          ]
                        }
                      },
                      "response": []
                    }
                  ]
                }
              ]
            }

            """.ReplaceLineEndings("\n"), Text(result));
        Assert.NotEqual(0xEF, result.Files[0].Content[0]);
    }

    [Fact]
    public void DescriptionsAreOmittedWhenEmpty()
    {
        var sub = CollectionNode.ForFolder(new FolderDefinition(Guid.NewGuid(), "Sub", "folder text", []), []);
        var node = DataNodeJson.Parse(Text(Run(Root("", sub))));
        Assert.Null(node["info"]!["description"]);
        Assert.Equal("folder text", node["item"]![0]!.GetString("description"));
        Assert.Null(DataNodeJson.Parse(Text(Run(Root("", CollectionNode.ForFolder(new FolderDefinition(Guid.NewGuid(), "S", "", []), [])))))["item"]![0]!["description"]);
    }

    [Fact]
    public void HeadersKeepDisabledState()
    {
        var headers = RequestNode(Req("r", headers: [new("A", "1"), new("B", "2", false)]))["header"];
        Assert.Equal("""[{"key":"A","value":"1","type":"text"},{"key":"B","value":"2","disabled":true,"type":"text"}]""", Compact(headers));
    }

    [Theory]
    [InlineData("https://api.example.com:8080/v1/users/1",
        """{"raw":"https://api.example.com:8080/v1/users/1?x=1","protocol":"https","host":["api","example","com"],"port":"8080","path":["v1","users","1"],"query":[{"key":"x","value":"1"}]}""")]
    [InlineData("{{baseUrl}}/users/{{id}}",
        """{"raw":"{{baseUrl}}/users/{{id}}?x=1","host":["{{baseUrl}}"],"path":["users","{{id}}"],"query":[{"key":"x","value":"1"}]}""")]
    [InlineData("http://localhost",
        """{"raw":"http://localhost?x=1","protocol":"http","host":["localhost"],"query":[{"key":"x","value":"1"}]}""")]
    [InlineData("http://h.test/a#frag",
        """{"raw":"http://h.test/a?x=1#frag","protocol":"http","host":["h","test"],"path":["a"],"query":[{"key":"x","value":"1"}]}""")]
    [InlineData("https://{{a.b}}.example.com/p",
        """{"raw":"https://{{a.b}}.example.com/p?x=1","protocol":"https","host":["{{a.b}}","example","com"],"path":["p"],"query":[{"key":"x","value":"1"}]}""")]
    public void UrlsAreSplit(string url, string expected) => Assert.Equal(expected, UrlOf(url, new KeyValueEntry("x", "1")));

    [Fact]
    public void RawKeepsOnlyEnabledParams()
    {
        Assert.Equal("""{"raw":"http://h.test?a=1","protocol":"http","host":["h","test"],"query":[{"key":"a","value":"1"},{"key":"b","value":"2","disabled":true}]}""",
            UrlOf("http://h.test", new("a", "1"), new("b", "2", false)));
        Assert.Equal("""{"raw":"http://h.test","protocol":"http","host":["h","test"]}""", UrlOf("http://h.test"));
    }

    [Fact]
    public void BodiesByKind()
    {
        Assert.Equal("""{"mode":"raw","raw":"{}","options":{"raw":{"language":"json"}}}""", BodyOf(RequestBody.Raw(RawContentType.Json, "{}")));
        Assert.Equal("""{"mode":"raw","raw":"t","options":{"raw":{"language":"text"}}}""", BodyOf(RequestBody.Raw(RawContentType.Text, "t")));
        Assert.Equal("""{"mode":"urlencoded","urlencoded":[{"key":"a","value":"1","type":"text"},{"key":"b","value":"2","disabled":true,"type":"text"}]}""",
            BodyOf(RequestBody.UrlEncoded([new("a", "1"), new("b", "2", false)])));
        Assert.Equal("""{"mode":"formdata","formdata":[{"key":"t","value":"v","type":"text"},{"key":"f","src":"C:\\a.png","disabled":true,"type":"file"}]}""",
            BodyOf(RequestBody.FormData([new("t", "v"), new("f", "C:\\a.png", false, FormFieldKind.File)])));
        Assert.Equal("""{"mode":"file","file":{"src":"/tmp/x.bin"}}""", BodyOf(RequestBody.Binary("/tmp/x.bin")));
        Assert.Null(RequestNode(Req("r"))["body"]);
    }

    [Fact]
    public void FileReferencesAreReportedOncePerRequest()
    {
        var a = Req("a", body: RequestBody.Binary("/x"));
        var b = Req("b", body: RequestBody.FormData([new("f", "/y", true, FormFieldKind.File), new("g", "/z", true, FormFieldKind.File)]));
        var c = Req("c", body: RequestBody.FormData([new("t", "v")]));
        var report = Run(Root("", CollectionNode.ForRequest(a), CollectionNode.ForRequest(b), CollectionNode.ForRequest(c))).Report;
        var entry = Assert.Single(report.Entries);
        Assert.Equal(ImportSeverity.Info, entry.Severity);
        Assert.Contains("2 ", entry.Message);
    }

    [Fact]
    public void RoundTripThroughTheImporter()
    {
        var get = Req("get users", "https://api.example.com/v1/users", query: [new("a", "1"), new("b", "2", false)],
            headers: [new("Accept", "*/*"), new("X-Off", "1", false)]);
        var post = Req("create", "{{baseUrl}}/users", HttpMethodKind.Post, body: RequestBody.Raw(RawContentType.Json, """{"a":1}"""));
        var form = Req("form", "http://h.test/f", HttpMethodKind.Put, body: RequestBody.UrlEncoded([new("k", "v"), new("k2", "v2", false)]));
        var sub = CollectionNode.ForFolder(new FolderDefinition(Guid.NewGuid(), "Sub", "", []), [CollectionNode.ForRequest(form)]);
        var root = Root("desc", sub, CollectionNode.ForRequest(get), CollectionNode.ForRequest(post));

        var json = Text(Run(root));
        var bundle = new PostmanCollectionImporter().Import(ImportInput.FromText("c.json", json),
            new ImportContext(SequentialIds.Create(), ImportOptions.Default));

        Assert.Equal("My API", bundle.Collection!.Folder.Name);
        Assert.Equal("desc", bundle.Collection.Folder.Description);
        var folder = Assert.IsType<ImportFolder>(bundle.Collection.Children[0]);
        Assert.Equal("Sub", folder.Folder.Name);
        AssertSame(form, Assert.IsType<ImportRequest>(Assert.Single(folder.Children)).Request);
        AssertSame(post, ((ImportRequest)bundle.Collection.Children[1]).Request);
        AssertSame(get, ((ImportRequest)bundle.Collection.Children[2]).Request);
    }

    private static void AssertSame(HttpRequestDefinition expected, HttpRequestDefinition actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Method, actual.Method);
        Assert.Equal(expected.Url, actual.Url);
        Assert.Equal(expected.QueryParams, actual.QueryParams);
        Assert.Equal(expected.Headers, actual.Headers);
        Assert.Equal(expected.Body, actual.Body);
    }
}
