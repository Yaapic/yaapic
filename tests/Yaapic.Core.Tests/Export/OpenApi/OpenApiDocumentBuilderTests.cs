using Yaapic.Core.Export;
using Yaapic.Core.Export.OpenApi;
using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests.Export.OpenApi;

public class OpenApiDocumentBuilderTests
{
    private static HttpRequestDefinition Req(string name, string url, HttpMethodKind method = HttpMethodKind.Get,
        IReadOnlyList<KeyValueEntry>? query = null, IReadOnlyList<KeyValueEntry>? headers = null) =>
        new(Guid.NewGuid(), name, method, url, query ?? [], headers ?? [], RequestBody.None, RequestAuth.None, RequestSettings.Default);

    private static CollectionNode R(HttpRequestDefinition request) => CollectionNode.ForRequest(request);

    private static CollectionNode Folder(string name, string description, params CollectionNode[] children) =>
        CollectionNode.ForFolder(new FolderDefinition(Guid.NewGuid(), name, description, []), children);

    private static CollectionNode Root(string description, KeyValueEntry[] variables, params CollectionNode[] children) =>
        CollectionNode.ForFolder(new FolderDefinition(Guid.NewGuid(), "My API", description, variables), children);

    private static (string Json, ImportReport Report) Build(CollectionNode root)
    {
        var report = new ImportReport();
        var node = OpenApiDocumentBuilder.Build(new ExportSource(root, [], [], root.Name), report);
        return (DataNodeJson.Write(node, indented: false), report);
    }

    private static DataNode Document(CollectionNode root) => DataNodeJson.Parse(Build(root).Json);

    private const string Response = "\"responses\":{\"default\":{\"description\":\"Response\"}}";

    [Fact]
    public void WritesServersPathsAndInfoInOrder()
    {
        var root = Root("desc", [new("baseUrl", "https://api.example.com/"), new("id", "42")],
            R(Req("Get user", "{{baseUrl}}/users/{{id}}")),
            R(Req("Other", "https://other.example.com/v2/items?x=1")));
        var (json, report) = Build(root);

        Assert.Equal(
            """{"openapi":"3.0.3","info":{"title":"My API","version":"1.0.0","description":"desc"},"servers":[{"url":"https://api.example.com"},{"url":"https://other.example.com"}],"paths":{"/users/{id}":{"get":{"summary":"Get user","operationId":"getUser","parameters":[{"name":"id","in":"path","required":true,"schema":{"type":"string"},"example":"42"}],""" + Response + """}},"/v2/items":{"get":{"summary":"Other","operationId":"other",""" + Response + "}}}}",
            json);
        var entry = Assert.Single(report.Entries);
        Assert.Equal(ImportSeverity.Info, entry.Severity);
    }

    [Fact]
    public void UnresolvedVariableOriginBecomesSlashWithAWarning()
    {
        var (json, report) = Build(Root("", [], R(Req("a", "{{host}}/a")), R(Req("b", "{{host}}/b"))));

        Assert.Contains("\"servers\":[{\"url\":\"/\"}]", json);
        Assert.DoesNotContain("\"description\":\"\"", json);
        var warning = Assert.Single(report.Entries, e => e.Severity == ImportSeverity.Warning);
        Assert.Contains("host", warning.Message);
    }

    [Fact]
    public void NoOriginMeansNoServers()
    {
        var (json, _) = Build(Root("", [], R(Req("a", "/a")), R(Req("b", "b"))));

        Assert.DoesNotContain("servers", json);
        Assert.Contains("\"/a\"", json);
        Assert.Contains("\"/b\"", json);
    }

    [Fact]
    public void ColonSegmentsAndVariablesBecomePathParameters()
    {
        var doc = Document(Root("", [new("v", "7")], R(Req("a", "https://x.test/users/:id/{{v}}"))));

        var path = doc["paths"]!.Members[0];
        Assert.Equal("/users/{id}/{v}", path.Key);
        var names = path.Value["get"]!["parameters"]!.Items.Select(p => p["name"]!.AsString()).ToArray();
        Assert.Equal(["id", "v"], names);
        Assert.Null(path.Value["get"]!["parameters"]![0]!["example"]);
        Assert.Equal("7", path.Value["get"]!["parameters"]![1]!["example"]!.AsString());
    }

    [Fact]
    public void SeveralMethodsShareAPathAndDuplicatesAreSkipped()
    {
        var (json, report) = Build(Root("", [],
            R(Req("A List", "https://x.test/items")),
            R(Req("B Create", "https://x.test/items", HttpMethodKind.Post)),
            R(Req("C Again", "https://x.test/items"))));

        Assert.Contains("\"get\":{\"summary\":\"A List\"", json);
        Assert.Contains("\"post\":{\"summary\":\"B Create\"", json);
        Assert.DoesNotContain("C Again", json);
        var warning = Assert.Single(report.Entries, e => e.Severity == ImportSeverity.Warning);
        Assert.Contains("GET", warning.Message);
        Assert.Contains("/items", warning.Message);
    }

    [Fact]
    public void OperationIdsAreCamelCasedAndUnique()
    {
        var doc = Document(Root("", [],
            R(Req("***", "/a")), R(Req("Get users", "/b")), R(Req("Get users!", "/c")), R(Req("Get-users", "/d"))));

        var ids = doc["paths"]!.Members.Select(p => p.Value["get"]!["operationId"]!.AsString()).ToArray();
        Assert.Equal(["operation", "getUsers", "getUsers2", "getUsers3"], ids);
    }

    [Fact]
    public void FolderNamesBecomeTagsWithDescriptions()
    {
        var doc = Document(Root("", [],
            R(Req("Top", "/top")),
            Folder("Users", "User calls", R(Req("In users", "/u")), Folder("Admin", "", R(Req("Deep", "/d"))))));

        Assert.Null(doc["paths"]!["/top"]!["get"]!["tags"]);
        Assert.Equal("Users", doc["paths"]!["/u"]!["get"]!["tags"]![0]!.AsString());
        Assert.Equal("Users / Admin", doc["paths"]!["/d"]!["get"]!["tags"]![0]!.AsString());
        Assert.Equal(
            """[{"name":"Users / Admin"},{"name":"Users","description":"User calls"}]""",
            DataNodeJson.Write(doc["tags"]!, indented: false));
    }

    [Fact]
    public void QueryParamsAndFilteredHeadersBecomeParameters()
    {
        var doc = Document(Root("", [], R(Req("a", "https://x.test/a",
            query: [new("page", "2"), new("q", "", false)],
            headers: [new("X-Trace", "abc"), new("content-type", "json"), new("Accept", "*/*"), new("Authorization", "t"), new("X-Off", "", false)]))));

        Assert.Equal(
            """[{"name":"page","in":"query","required":true,"schema":{"type":"string"},"example":"2"},{"name":"q","in":"query","required":false,"schema":{"type":"string"}},{"name":"X-Trace","in":"header","required":true,"schema":{"type":"string"},"example":"abc"},{"name":"X-Off","in":"header","required":false,"schema":{"type":"string"}}]""",
            DataNodeJson.Write(doc["paths"]!["/a"]!["get"]!["parameters"]!, indented: false));
    }

    [Fact]
    public void RequiresACollection() =>
        Assert.Throws<ExportException>(() => OpenApiDocumentBuilder.Build(new ExportSource(null, [], [], "x"), new ImportReport()));
}
