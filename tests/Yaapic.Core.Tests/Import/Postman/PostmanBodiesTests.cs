using Yaapic.Core.Import;
using Yaapic.Core.Import.Postman;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.Postman;

public class PostmanBodiesTests
{
    private static (RequestBody Body, string? Added, ImportReport Report) Map(string? body, string? contentType = null)
    {
        ImportReport report = new();
        var (result, added) = PostmanBodies.Map(body is null ? null : DataNodeJson.Parse(body), "r", report, contentType);
        return (result, added, report);
    }

    private static int Count(ImportReport report, ImportSeverity severity) => report.Entries.Count(e => e.Severity == severity);

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"mode": null}""")]
    public void Absent_body_or_mode_is_none(string? body) => Assert.Equal(RequestBody.None, Map(body).Body);

    [Theory]
    [InlineData("json", RawContentType.Json)]
    [InlineData("xml", RawContentType.Xml)]
    [InlineData("html", RawContentType.Html)]
    [InlineData("javascript", RawContentType.JavaScript)]
    [InlineData("text", RawContentType.Text)]
    [InlineData("yaml", RawContentType.Text)]
    public void Raw_language_maps_to_the_content_type(string language, RawContentType expected) =>
        Assert.Equal(
            RequestBody.Raw(expected, "x"),
            Map("""{"mode": "raw", "raw": "x", "options": {"raw": {"language": "LANG"}}}""".Replace("LANG", language), "application/json").Body);

    [Fact]
    public void Raw_without_options_uses_the_header_then_text()
    {
        Assert.Equal(RequestBody.Raw(RawContentType.Xml, "<a/>"), Map("""{"mode": "raw", "raw": "<a/>"}""", "application/xml; charset=utf-8").Body);
        Assert.Equal(RequestBody.Raw(RawContentType.Text, "a"), Map("""{"mode": "raw", "raw": "a"}""").Body);
    }

    [Fact]
    public void Raw_translates_dynamic_variables() =>
        Assert.Equal("{{$guid}}", Map("""{"mode": "raw", "raw": "{{$randomUUID}}"}""").Body.Text);

    [Fact]
    public void Urlencoded_keeps_disabled_state_and_drops_keyless_entries() =>
        Assert.Equal(
            RequestBody.UrlEncoded([new FormField("a", "1"), new FormField("b", "", false)]),
            Map("""{"mode": "urlencoded", "urlencoded": [{"key": "a", "value": "1"}, {"key": "b", "disabled": true}, {"value": "x"}]}""").Body);

    [Fact]
    public void Formdata_maps_text_and_file_fields()
    {
        var (body, _, report) = Map("""
            {"mode": "formdata", "formdata": [
              {"key": "t", "value": "v", "type": "text", "contentType": "text/plain"},
              {"key": "f1", "type": "file", "src": "a.png"},
              {"key": "f2", "type": "file", "src": ["b.png", "c.png"], "disabled": true},
              {"key": "f3", "type": "file"},
              {"key": "f4", "type": "file", "src": ""}
            ]}
            """);

        Assert.Equal(
            RequestBody.FormData([
                new FormField("t", "v"),
                new FormField("f1", "a.png", true, FormFieldKind.File),
                new FormField("f2", "b.png", false, FormFieldKind.File)]),
            body);
        Assert.Equal(4, Count(report, ImportSeverity.Warning));
        Assert.Equal(2, report.Entries.Count(e => e.Message.Contains("local file", StringComparison.Ordinal)));
    }

    [Fact]
    public void File_mode_is_binary_with_a_warning()
    {
        var (body, _, report) = Map("""{"mode": "file", "file": {"src": "d.bin"}}""");

        Assert.Equal(RequestBody.Binary("d.bin"), body);
        Assert.Equal(1, Count(report, ImportSeverity.Warning));
    }

    [Fact]
    public void File_mode_without_src_is_none_with_a_warning()
    {
        var (body, _, report) = Map("""{"mode": "file", "file": {}}""");

        Assert.Equal(RequestBody.None, body);
        Assert.Equal(1, Count(report, ImportSeverity.Warning));
    }

    [Fact]
    public void Graphql_becomes_a_json_body_and_adds_the_content_type_only_when_absent()
    {
        var node = """{"mode": "graphql", "graphql": {"query": "{ a }", "variables": "{\"x\": 1}"}}""";

        var (body, added, report) = Map(node);
        Assert.Equal(RequestBody.Raw(RawContentType.Json, """{"query":"{ a }","variables":{"x":1}}"""), body);
        Assert.Equal("application/json", added);
        Assert.Equal(1, Count(report, ImportSeverity.Info));

        Assert.Null(Map(node, "application/json").Added);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"{oops\"")]
    [InlineData("\"[1]\"")]
    [InlineData("null")]
    public void Graphql_without_or_with_invalid_variables_uses_an_empty_object(string variables) =>
        Assert.Equal(
            """{"query":"q","variables":{}}""",
            Map($$$"""{"mode": "graphql", "graphql": {"query": "q", "variables": {{{variables}}}}}""").Body.Text);

    [Fact]
    public void Unknown_mode_is_none_with_a_warning()
    {
        var (body, _, report) = Map("""{"mode": "weird"}""");

        Assert.Equal(RequestBody.None, body);
        Assert.Equal(1, Count(report, ImportSeverity.Warning));
    }

    [Fact]
    public void Importer_adds_the_graphql_content_type_header_once()
    {
        var importer = new PostmanCollectionImporter();
        ImportContext context = new(SequentialIds.Create(), ImportOptions.Default);
        var bundle = importer.Import(ImportInput.FromText("c.json", """
            {"info": {"name": "n"}, "item": [
              {"name": "a", "request": {"method": "POST", "url": "https://x.test", "body": {"mode": "graphql", "graphql": {"query": "q"}}}},
              {"name": "b", "request": {"method": "POST", "url": "https://x.test", "header": [{"key": "content-type", "value": "application/graphql"}], "body": {"mode": "graphql", "graphql": {"query": "q"}}}}
            ]}
            """), context);

        var requests = bundle.Collection!.Children.Cast<ImportRequest>().Select(r => r.Request).ToList();
        Assert.Equal([new KeyValueEntry("Content-Type", "application/json")], requests[0].Headers);
        Assert.Single(requests[1].Headers);
        Assert.Equal(BodyKind.Raw, requests[1].Body.Kind);
    }
}
