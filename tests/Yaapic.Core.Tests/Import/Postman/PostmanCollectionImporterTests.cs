using Yaapic.Core.Import;
using Yaapic.Core.Import.Postman;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.Postman;

public class PostmanCollectionImporterTests
{
    private static readonly PostmanCollectionImporter Importer = new();

    private static ImportBundle Run(string json, out ImportContext context)
    {
        context = new ImportContext(SequentialIds.Create(), ImportOptions.Default);
        return Importer.Import(ImportInput.FromText("coll.json", json), context);
    }

    private static string Collection(string items, string info = """{"name": "My API"}""") =>
        $$"""{"info": {{info}}, "item": {{items}}}""";

    private static HttpRequestDefinition OneRequest(string request, string extra = "")
    {
        var bundle = Run(Collection($$"""[{"name": "r", "request": {{request}} {{extra}}}]"""), out _);
        return Assert.IsType<ImportRequest>(Assert.Single(bundle.Collection!.Children)).Request;
    }

    [Fact]
    public void Sniff_tells_the_formats_apart()
    {
        Assert.Equal(ImportConfidence.Certain, Importer.Sniff(ImportInput.FromText("a", """{"info": {"schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"}}""")));
        Assert.Equal(ImportConfidence.Possible, Importer.Sniff(ImportInput.FromText("a", """{"info": {"_postman_id": "x"}, "item": []}""")));
        Assert.Equal(ImportConfidence.None, Importer.Sniff(ImportInput.FromText("a", """{"openapi": "3.0.0"}""")));
        Assert.Equal(ImportConfidence.None, Importer.Sniff(ImportInput.FromText("a", "curl https://x")));
        Assert.Equal(ImportConfidence.None, Importer.Sniff(new ImportInput("a", [])));
    }

    [Fact]
    public void Wrapper_object_is_unwrapped()
    {
        var bundle = Run("""{"collection": {"info": {"name": "Wrapped", "description": {"content": "desc"}}, "item": []}}""", out _);

        Assert.Equal("Wrapped", bundle.Collection!.Folder.Name);
        Assert.Equal("desc", bundle.Collection.Folder.Description);
    }

    [Fact]
    public void Folders_and_requests_are_walked_in_order()
    {
        var bundle = Run(Collection("""
            [
              {"name": "F1", "description": "d1", "item": [
                {"name": "F2", "item": [{"name": "deep", "request": "https://a.test/x"}]},
                {"name": "mid", "request": {"method": "POST", "url": "https://a.test/y"}}
              ]},
              {"name": "top", "request": {"url": "https://a.test/z"}}
            ]
            """), out var context);

        var root = bundle.Collection!;
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), root.Folder.Id);
        Assert.Equal("My API", root.Folder.Name);
        var f1 = Assert.IsType<ImportFolder>(root.Children[0]);
        Assert.Equal(("F1", "d1"), (f1.Folder.Name, f1.Folder.Description));
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), f1.Folder.Id);
        var f2 = Assert.IsType<ImportFolder>(f1.Children[0]);
        Assert.Equal("F2", f2.Folder.Name);
        var deep = Assert.IsType<ImportRequest>(Assert.Single(f2.Children)).Request;
        Assert.Equal(("deep", HttpMethodKind.Get, "https://a.test/x"), (deep.Name, deep.Method, deep.Url));
        Assert.Equal(HttpMethodKind.Post, Assert.IsType<ImportRequest>(f1.Children[1]).Request.Method);
        Assert.Equal("top", Assert.IsType<ImportRequest>(root.Children[1]).Request.Name);
        Assert.Equal(2, bundle.RequestCount - 1);
        Assert.Equal(1, context.Report.Entries.Count(e => e.Severity == ImportSeverity.Info));
    }

    [Fact]
    public void Url_forms_are_read()
    {
        Assert.Equal("https://a.test/x", OneRequest("\"https://a.test/x\"").Url);

        var raw = OneRequest("""{"url": {"raw": "https://a.test/p?a=1&b=2#f"}}""");
        Assert.Equal("https://a.test/p#f", raw.Url);
        Assert.Equal([new KeyValueEntry("a", "1"), new KeyValueEntry("b", "2")], raw.QueryParams);

        var composed = OneRequest("""{"url": {"protocol": "https", "host": ["api", "a", "test"], "port": "8080", "path": ["v1", {"value": "users"}, ""]}}""");
        Assert.Equal("https://api.a.test:8080/v1/users", composed.Url);

        var hostString = OneRequest("""{"url": {"host": "{{base}}"}}""");
        Assert.Equal("{{base}}", hostString.Url);
    }

    [Fact]
    public void Query_array_overrides_the_raw_query()
    {
        var request = OneRequest("""
            {"url": {"raw": "https://a.test/p?old=1", "query": [
              {"key": "a", "value": "1"}, {"key": "b", "value": null, "disabled": true}, {"key": null, "value": null}, {"key": "c", "value": "{{$randomUUID}}"}]}}
            """);

        Assert.Equal("https://a.test/p", request.Url);
        Assert.Equal([new KeyValueEntry("a", "1"), new KeyValueEntry("b", "", false), new KeyValueEntry("c", "{{$guid}}")], request.QueryParams);
    }

    [Fact]
    public void Path_variables_are_substituted()
    {
        var request = OneRequest("""{"url": {"raw": "https://a.test/u/:id/:name", "variable": [{"key": "id", "value": "42"}]}}""");

        Assert.Equal("https://a.test/u/42/{{name}}", request.Url);
    }

    [Fact]
    public void Headers_are_read_in_array_and_string_forms()
    {
        var array = OneRequest("""{"url": "u", "header": [{"key": "A", "value": "1"}, {"key": "B", "value": "2", "disabled": true}, {"value": "x"}]}""");
        Assert.Equal([new KeyValueEntry("A", "1"), new KeyValueEntry("B", "2", false)], array.Headers);

        var text = OneRequest("""{"url": "u", "header": "A: b\nC: d:e\nbad"}""");
        Assert.Equal([new KeyValueEntry("A", "b"), new KeyValueEntry("C", "d:e")], text.Headers);
    }

    [Fact]
    public void Unsupported_method_and_unknown_item_are_skipped()
    {
        var bundle = Run(Collection("""[{"name": "t", "request": {"method": "TRACE", "url": "u"}}, {"name": "what"}]"""), out var context);

        Assert.Empty(bundle.Collection!.Children);
        Assert.Equal(2, context.Report.Count(ImportSeverity.Skipped));
    }

    [Fact]
    public void Dynamic_variables_are_translated_and_warned_once()
    {
        var bundle = Run(Collection("""
            [{"name": "a", "request": {"url": "https://a.test/{{$randomUUID}}", "header": [{"key": "X", "value": "{{$randomFirstName}}"}, {"key": "Y", "value": "{{$randomFirstName}}"}]}}]
            """), out var context);

        var request = Assert.IsType<ImportRequest>(Assert.Single(bundle.Collection!.Children)).Request;
        Assert.Equal("https://a.test/{{$guid}}", request.Url);
        Assert.Equal(1, context.Report.Count(ImportSeverity.Warning));
    }

    [Fact]
    public void Dropped_examples_and_descriptions_are_counted()
    {
        var bundle = Run(Collection("""
            [{"name": "a", "request": {"url": "u", "description": "d"}, "response": [{}, {}]},
             {"name": "b", "request": {"url": "u", "description": {"content": "d"}}}]
            """), out var context);

        Assert.Equal(2, bundle.RequestCount);
        Assert.Contains(context.Report.Entries, e => e.Message.Contains("(2)", StringComparison.Ordinal));
        Assert.Contains(context.Report.Entries, e => e.Message.Contains(" 2 ", StringComparison.Ordinal));
    }

    [Fact]
    public void A_request_without_name_gets_a_description()
    {
        var bundle = Run(Collection("""[{"request": {"method": "PUT", "url": "https://a.test/x"}}]"""), out _);

        Assert.Equal("PUT a.test/x", Assert.IsType<ImportRequest>(Assert.Single(bundle.Collection!.Children)).Request.Name);
    }

    [Theory]
    [InlineData("""{"item": []}""")]
    [InlineData("""{"info": {"name": "x"}}""")]
    [InlineData("not json")]
    public void An_invalid_input_throws(string json) =>
        Assert.Throws<ImportException>(() => Run(json, out _));
}
