using Yaapic.Core.Import;
using Yaapic.Core.Import.Har;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.Har;

public class HarImporterTests
{
    private static readonly HarImporter Importer = new();

    private static string Har(params string[] entries) =>
        "{\"log\":{\"version\":\"1.2\",\"entries\":[" + string.Join(',', entries) + "]}}";

    private static string Entry(string request, string? response = null) =>
        "{\"request\":" + request + (response is null ? "" : ",\"response\":" + response) + "}";

    private static ImportBundle Run(string json, out ImportContext context)
    {
        context = new ImportContext(SequentialIds.Create(), ImportOptions.Default);
        return Importer.Import(ImportInput.FromText("capture", json), context);
    }

    private static List<HttpRequestDefinition> Requests(ImportBundle bundle) =>
        [.. bundle.Collection!.Children.Cast<ImportRequest>().Select(r => r.Request)];

    private static HttpRequestDefinition One(string request, out ImportContext context) =>
        Assert.Single(Requests(Run(Har(Entry(request)), out context)));

    [Fact]
    public void Identity()
    {
        Assert.Equal("har", Importer.Id);
        Assert.Equal("HAR", Importer.DisplayName);
    }

    [Theory]
    [InlineData("{\"log\":{\"entries\":[]}}", ImportConfidence.Certain)]
    [InlineData("{\"entries\":[{\"request\":{}}]}", ImportConfidence.Possible)]
    [InlineData("{\"entries\":[]}", ImportConfidence.None)]
    [InlineData("{\"info\":{}}", ImportConfidence.None)]
    public void Sniff_cases(string text, ImportConfidence expected) =>
        Assert.Equal(expected, Importer.Sniff(ImportInput.FromText("a", text)));

    [Fact]
    public void Sniff_without_file_is_none() => Assert.Equal(ImportConfidence.None, Importer.Sniff(new ImportInput("a", [])));

    [Theory]
    [InlineData("{\"log\":{}}")]
    [InlineData("{\"log\":{\"entries\":{}}}")]
    [InlineData("[]")]
    public void Not_a_har_throws(string text) =>
        Assert.Throws<ImportException>(() => Run(text, out _));

    [Fact]
    public void Invalid_json_throws() => Assert.Throws<ImportException>(() => Run("{", out _));

    [Fact]
    public void Empty_entries_gives_the_folder_only()
    {
        var bundle = Run(Har(), out var context);

        Assert.Equal("capture", bundle.Collection!.Folder.Name);
        Assert.Empty(bundle.Collection.Children);
        Assert.Empty(context.Report.Entries);
    }

    [Fact]
    public void Get_and_post_json_with_header_filtering()
    {
        var bundle = Run(
            Har(
                Entry("{\"method\":\"GET\",\"url\":\"https://a.test/users?page=2&q=x\",\"headers\":["
                    + "{\"name\":\":authority\",\"value\":\"a.test\"},{\"name\":\"Host\",\"value\":\"a.test\"},"
                    + "{\"name\":\"Accept-Encoding\",\"value\":\"gzip\"},{\"name\":\"Cookie\",\"value\":\"s=1\"},"
                    + "{\"name\":\"User-Agent\",\"value\":\"UA\"}],\"queryString\":[{\"name\":\"page\",\"value\":\"2\"}]}"),
                Entry("{\"method\":\"POST\",\"url\":\"https://a.test/users\",\"headers\":["
                    + "{\"name\":\"Content-Length\",\"value\":\"7\"},{\"name\":\"Content-Type\",\"value\":\"application/json\"}],"
                    + "\"postData\":{\"mimeType\":\"application/json; charset=utf-8\",\"text\":\"{\\\"a\\\":1}\"}}")),
            out _);

        var requests = Requests(bundle);
        Assert.Equal(2, requests.Count);

        var get = requests[0];
        Assert.Equal("GET a.test/users", get.Name);
        Assert.Equal("https://a.test/users", get.Url);
        Assert.Equal([new KeyValueEntry("page", "2"), new KeyValueEntry("q", "x")], get.QueryParams);
        Assert.Equal([new KeyValueEntry("Cookie", "s=1"), new KeyValueEntry("User-Agent", "UA")], get.Headers);
        Assert.Equal(RequestBody.None, get.Body);
        Assert.Equal(RequestAuth.None, get.Auth);

        var post = requests[1];
        Assert.Equal(HttpMethodKind.Post, post.Method);
        Assert.Equal([new KeyValueEntry("Content-Type", "application/json")], post.Headers);
        Assert.Equal(RequestBody.Raw(RawContentType.Json, "{\"a\":1}"), post.Body);
    }

    [Fact]
    public void Urlencoded_from_params()
    {
        var r = One(
            "{\"method\":\"POST\",\"url\":\"https://a.test/f\",\"postData\":{\"mimeType\":\"application/x-www-form-urlencoded\","
            + "\"params\":[{\"name\":\"a b\",\"value\":\"1\"},{\"name\":\"c\",\"value\":\"\"}]}}",
            out _);

        Assert.Equal(BodyKind.UrlEncoded, r.Body.Kind);
        Assert.Equal([new FormField("a b", "1"), new FormField("c", "")], r.Body.Fields);
    }

    [Fact]
    public void Urlencoded_from_text()
    {
        var r = One(
            "{\"method\":\"POST\",\"url\":\"https://a.test/f\",\"postData\":{\"mimeType\":\"application/x-www-form-urlencoded\","
            + "\"text\":\"a=1%202&b=x+y&flag\"}}",
            out _);

        Assert.Equal([new FormField("a", "1 2"), new FormField("b", "x y"), new FormField("flag", "")], r.Body.Fields);
    }

    [Fact]
    public void Multipart_with_file_field_drops_content_type()
    {
        var r = One(
            "{\"method\":\"POST\",\"url\":\"https://a.test/up\",\"headers\":[{\"name\":\"Content-Type\",\"value\":\"multipart/form-data; boundary=x\"}],"
            + "\"postData\":{\"mimeType\":\"multipart/form-data; boundary=x\",\"params\":["
            + "{\"name\":\"title\",\"value\":\"hi\"},{\"name\":\"doc\",\"fileName\":\"a.pdf\",\"contentType\":\"application/pdf\"}]}}",
            out var context);

        Assert.Empty(r.Headers);
        Assert.Equal(BodyKind.FormData, r.Body.Kind);
        Assert.Equal([new FormField("title", "hi"), new FormField("doc", "", true, FormFieldKind.File)], r.Body.Fields);
        var warning = Assert.Single(context.Report.Entries, e => e.Severity == ImportSeverity.Warning);
        Assert.Contains("a.pdf", warning.Message);
        Assert.Contains("doc", warning.Message);
    }

    [Fact]
    public void Raw_xml_adds_no_header()
    {
        var r = One(
            "{\"method\":\"POST\",\"url\":\"https://a.test/x\",\"postData\":{\"mimeType\":\"text/xml\",\"text\":\"<a/>\"}}",
            out _);

        Assert.Equal(RequestBody.Raw(RawContentType.Xml, "<a/>"), r.Body);
        Assert.Empty(r.Headers);
    }

    [Fact]
    public void Exotic_mime_type_adds_the_header_unless_present()
    {
        var added = One(
            "{\"method\":\"POST\",\"url\":\"https://a.test/x\",\"postData\":{\"mimeType\":\"application/vnd.api+json\",\"text\":\"{}\"}}",
            out _);
        var kept = One(
            "{\"method\":\"POST\",\"url\":\"https://a.test/x\",\"headers\":[{\"name\":\"content-type\",\"value\":\"application/vnd.api+json\"}],"
            + "\"postData\":{\"mimeType\":\"application/vnd.api+json\",\"text\":\"{}\"}}",
            out _);

        Assert.Equal([new KeyValueEntry("Content-Type", "application/vnd.api+json")], added.Headers);
        Assert.Equal(RawContentType.Json, added.Body.ContentType);
        Assert.Single(kept.Headers);
    }

    [Fact]
    public void Base64_body_is_dropped_with_a_warning()
    {
        var r = One(
            "{\"method\":\"POST\",\"url\":\"https://a.test/x\",\"postData\":{\"mimeType\":\"text/plain\",\"text\":\"AAA=\",\"encoding\":\"base64\"}}",
            out var context);

        Assert.Equal(RequestBody.None, r.Body);
        Assert.Equal(1, context.Report.Count(ImportSeverity.Warning));
    }

    [Fact]
    public void Missing_url_and_unsupported_method_are_skipped_with_their_index()
    {
        var bundle = Run(
            Har(
                Entry("{\"method\":\"GET\"}"),
                Entry("{\"method\":\"TRACE\",\"url\":\"https://a.test/t\"}"),
                Entry("{\"method\":\"GET\",\"url\":\"https://a.test/ok\"}")),
            out var context);

        Assert.Single(Requests(bundle));
        Assert.Equal(["entries[0]", "entries[1]"], context.Report.Entries.Where(e => e.Severity == ImportSeverity.Skipped).Select(e => e.Path));
        Assert.Contains("0", context.Report.Entries[0].Message);
    }

    [Fact]
    public void Responses_produce_one_info_with_the_count()
    {
        var bundle = Run(
            Har(
                Entry("{\"method\":\"GET\",\"url\":\"https://a.test/1\"}", "{\"status\":200}"),
                Entry("{\"method\":\"GET\",\"url\":\"https://a.test/2\"}", "{\"status\":404}"),
                Entry("{\"method\":\"GET\",\"url\":\"https://a.test/3\"}")),
            out var context);

        Assert.Equal(3, bundle.RequestCount);
        var info = Assert.Single(context.Report.Entries);
        Assert.Equal(ImportSeverity.Info, info.Severity);
        Assert.Contains("2 entries", info.Message);
    }
}
