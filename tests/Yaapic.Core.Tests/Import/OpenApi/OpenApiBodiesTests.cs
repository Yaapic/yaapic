using Yaapic.Core.Import;
using Yaapic.Core.Import.OpenApi;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.OpenApi;

public class OpenApiBodiesTests
{
    private static readonly OpenApiImporter Importer = new(new StructuredTextReader(new NoYaml()));

    private sealed class NoYaml : IYamlTreeReader
    {
        public DataNode Parse(string yaml) => throw new NotSupportedException();
    }

    private static HttpRequestDefinition Run(string operation, out ImportContext context, string extra = "")
    {
        var json = "{\"openapi\":\"3.0.3\",\"info\":{\"title\":\"T\"}," + extra + "\"paths\":{\"/x\":{\"post\":" + operation + "}}}";
        context = new ImportContext(SequentialIds.Create(), ImportOptions.Default);
        var bundle = Importer.Import(ImportInput.FromText("api.json", json), context);
        return Assert.IsType<ImportRequest>(Assert.Single(Assert.IsType<ImportFolder>(bundle.Collection).Children)).Request;
    }

    private static HttpRequestDefinition Body(string content, out ImportContext context, string extra = "") =>
        Run("{\"requestBody\":{\"required\":true,\"content\":" + content + "}}", out context, extra);

    private static string Messages(ImportContext context) => string.Join("\n", context.Report.Entries.Select(e => e.Message));

    [Fact]
    public void No_request_body_gives_none()
    {
        var request = Run("{}", out _);
        Assert.Equal(RequestBody.None, request.Body);
    }

    [Fact]
    public void Media_type_choice_follows_the_documented_order()
    {
        var request = Body("""{"text/plain":{"example":"t"},"application/vnd.api+json":{"example":{"a":1}},"application/json; charset=utf-8":{"example":{"b":2}}}""", out var context);
        Assert.Equal(BodyKind.Raw, request.Body.Kind);
        Assert.Contains("\"b\"", request.Body.Text);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Info && e.Message.Contains('2'));

        request = Body("""{"text/csv":{"example":"a"},"application/vnd.api+json":{"example":{"a":1}}}""", out _);
        Assert.Equal(RawContentType.Json, request.Body.ContentType);

        request = Body("""{"application/xml":{"example":"<a/>"},"multipart/form-data":{"schema":{"properties":{"f":{"type":"string"}}}}}""", out _);
        Assert.Equal(BodyKind.FormData, request.Body.Kind);

        request = Body("""{"x/y":{"example":"a"},"text/csv":{"example":"b"}}""", out _);
        Assert.Equal("b", request.Body.Text);

        request = Body("""{"image/png":{},"x/y":{}}""", out _);
        Assert.Equal(BodyKind.Binary, request.Body.Kind);
    }

    [Fact]
    public void Json_comes_from_example_examples_schema_or_string_example()
    {
        var request = Body("""{"application/json":{"example":{"a":1}}}""", out _);
        Assert.Equal(RawContentType.Json, request.Body.ContentType);
        Assert.Contains("\"a\": 1", request.Body.Text);

        request = Body("""{"application/json":{"examples":{"first":{"value":{"b":2}},"second":{"value":{"c":3}}}}}""", out _);
        Assert.Contains("\"b\": 2", request.Body.Text);
        Assert.DoesNotContain("\"c\"", request.Body.Text);

        request = Body("""{"application/json":{"schema":{"type":"object","properties":{"name":{"type":"string"}}}}}""", out _);
        Assert.Contains("\"name\": \"string\"", request.Body.Text);

        request = Body("""{"application/json":{"example":"{\"raw\":true}"}}""", out _);
        Assert.Equal("{\"raw\":true}", request.Body.Text);
    }

    [Fact]
    public void Urlencoded_comes_from_sample_or_from_properties_only()
    {
        var request = Body("""{"application/x-www-form-urlencoded":{"example":{"a":"1","o":{"k":1},"l":[1,2]}}}""", out _);
        Assert.Equal(BodyKind.UrlEncoded, request.Body.Kind);
        Assert.Equal(["a=1", "o={\"k\":1}", "l=[1,2]"], request.Body.Fields.Select(f => f.Key + "=" + f.Value));

        request = Body("""{"application/x-www-form-urlencoded":{"schema":{"properties":{"p":{"type":"string"},"q":{"type":"integer"}}}}}""", out _);
        Assert.Equal(["p", "q"], request.Body.Fields.Select(f => f.Key));
        Assert.Equal(["string", "0"], request.Body.Fields.Select(f => f.Value));
    }

    [Fact]
    public void Multipart_marks_binary_properties_as_empty_file_fields()
    {
        var request = Body("""{"multipart/form-data":{"schema":{"properties":{"name":{"type":"string"},"file":{"type":"string","format":"binary"}}}}}""", out var context);
        Assert.Equal(BodyKind.FormData, request.Body.Kind);
        Assert.Equal(FormFieldKind.Text, request.Body.Fields[0].Kind);
        var file = request.Body.Fields[1];
        Assert.Equal(("file", "", FormFieldKind.File), (file.Key, file.Value, file.Kind));
        Assert.Contains("\"file\"", Messages(context));
    }

    [Fact]
    public void Octet_stream_is_an_empty_binary_body_with_a_warning_and_a_header()
    {
        var request = Body("""{"application/octet-stream":{"schema":{"type":"string","format":"binary"}}}""", out var context);
        Assert.Equal(BodyKind.Binary, request.Body.Kind);
        Assert.Equal("", request.Body.FilePath);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Warning);
        Assert.Contains(request.Headers, h => h is { Key: "Content-Type", Value: "application/octet-stream", Enabled: true });
    }

    [Fact]
    public void Xml_uses_a_string_example_or_a_placeholder_with_an_info()
    {
        var request = Body("""{"application/xml":{"example":"<a/>"}}""", out var context);
        Assert.Equal((RawContentType.Xml, "<a/>"), (request.Body.ContentType, request.Body.Text));
        Assert.DoesNotContain(context.Report.Entries, e => e.Path == "POST /x");

        request = Body("""{"application/xml":{"schema":{"type":"object","properties":{"a":{"type":"string"}}}}}""", out context);
        Assert.Equal(RawContentType.Xml, request.Body.ContentType);
        Assert.Equal("<!-- example body not generated for XML schemas -->", request.Body.Text);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Info && e.Path == "POST /x");
    }

    [Fact]
    public void Text_media_types_map_to_raw_kinds()
    {
        var request = Body("""{"text/html":{"example":"<p/>"}}""", out _);
        Assert.Equal((RawContentType.Html, "<p/>"), (request.Body.ContentType, request.Body.Text));

        request = Body("""{"text/csv":{"example":"a,b"}}""", out _);
        Assert.Equal((RawContentType.Text, "a,b"), (request.Body.ContentType, request.Body.Text));
        Assert.Contains(request.Headers, h => h is { Key: "Content-Type", Value: "text/csv" });

        request = Body("""{"text/plain":{"schema":{"type":"object"}}}""", out _);
        Assert.Equal("", request.Body.Text);
    }

    [Fact]
    public void Exotic_json_types_add_the_header_but_plain_json_does_not()
    {
        var request = Body("""{"application/vnd.api+json":{"example":{}}}""", out _);
        Assert.Contains(request.Headers, h => h is { Key: "Content-Type", Value: "application/vnd.api+json", Enabled: true });

        request = Body("""{"application/json":{"example":{}}}""", out _);
        Assert.DoesNotContain(request.Headers, h => h.Key == "Content-Type");
    }

    [Fact]
    public void Request_body_reference_is_resolved_and_required_is_ignored()
    {
        var request = Run(
            """{"requestBody":{"$ref":"#/components/requestBodies/B"}}""",
            out _,
            "\"components\":{\"requestBodies\":{\"B\":{\"required\":false,\"content\":{\"application/json\":{\"example\":{\"z\":1}}}}}},");
        Assert.Equal(BodyKind.Raw, request.Body.Kind);
        Assert.Contains("\"z\": 1", request.Body.Text);
    }

    [Fact]
    public void Several_media_types_report_how_many_were_not_imported()
    {
        _ = Body("""{"application/json":{"example":{}},"application/xml":{"example":"<a/>"},"text/plain":{"example":"a"}}""", out var context);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Info && e.Message.Contains('2'));
    }
}
