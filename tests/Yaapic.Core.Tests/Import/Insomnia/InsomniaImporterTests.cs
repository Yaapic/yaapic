using Yaapic.Core.Import;
using Yaapic.Core.Import.Insomnia;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.Insomnia;

public class InsomniaImporterTests
{
    private static readonly InsomniaImporter Importer = new();

    private const string Workspace = "{\"_id\":\"wrk_1\",\"_type\":\"workspace\",\"name\":\"My API\",\"description\":\"Docs\"}";

    private static string Export(params string[] resources) =>
        "{\"_type\":\"export\",\"__export_format\":4,\"resources\":[" + string.Join(',', resources) + "]}";

    private static string Req(string fields, string parent = "wrk_1") =>
        "{\"_type\":\"request\",\"parentId\":\"" + parent + "\",\"_id\":\"req_" + Guid.NewGuid().ToString("N") + "\"," + fields + "}";

    private static ImportBundle Run(string json, out ImportContext context)
    {
        context = new ImportContext(SequentialIds.Create(), ImportOptions.Default);
        return Importer.Import(ImportInput.FromText("insomnia", json), context);
    }

    private static HttpRequestDefinition One(string fields, out ImportContext context) =>
        Assert.IsType<ImportRequest>(Assert.Single(Run(Export(Workspace, Req(fields)), out context).Collection!.Children)).Request;

    [Fact]
    public void Identity()
    {
        Assert.Equal("insomnia", Importer.Id);
        Assert.Equal("Insomnia", Importer.DisplayName);
    }

    [Theory]
    [InlineData("{\"_type\":\"export\",\"__export_format\":4,\"resources\":[]}", ImportConfidence.Certain)]
    [InlineData("{\"_type\":\"export\",\"resources\":[]}", ImportConfidence.Possible)]
    [InlineData("{\"resources\":[]}", ImportConfidence.None)]
    public void Sniff_cases(string text, ImportConfidence expected) =>
        Assert.Equal(expected, Importer.Sniff(ImportInput.FromText("a", text)));

    [Fact]
    public void Sniff_without_file_is_none() => Assert.Equal(ImportConfidence.None, Importer.Sniff(new ImportInput("a", [])));

    [Fact]
    public void Workspace_and_two_folder_levels()
    {
        var bundle = Run(Export(
            Workspace,
            "{\"_type\":\"request_group\",\"_id\":\"fld_1\",\"parentId\":\"wrk_1\",\"name\":\"Users\",\"description\":\"u\"}",
            "{\"_type\":\"request_group\",\"_id\":\"fld_2\",\"parentId\":\"fld_1\",\"name\":\"Admin\"}",
            Req("\"name\":\"Deep\",\"method\":\"GET\",\"url\":\"http://a\"", "fld_2"),
            Req("\"name\":\"Top\",\"method\":\"GET\",\"url\":\"http://a\"")), out var context);

        Assert.Equal("My API", bundle.Collection!.Folder.Name);
        Assert.Equal("Docs", bundle.Collection.Folder.Description);
        Assert.Equal(2, bundle.Collection.Children.Count);
        var users = Assert.IsType<ImportFolder>(bundle.Collection.Children[0]);
        Assert.Equal("Users", users.Folder.Name);
        Assert.Equal("u", users.Folder.Description);
        var admin = Assert.IsType<ImportFolder>(Assert.Single(users.Children));
        Assert.Equal("Deep", Assert.IsType<ImportRequest>(Assert.Single(admin.Children)).Request.Name);
        Assert.Equal("Top", Assert.IsType<ImportRequest>(bundle.Collection.Children[1]).Request.Name);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Info);
    }

    [Fact]
    public void Without_workspace_the_collection_takes_the_input_name_and_orphans_attach_to_it()
    {
        var bundle = Run(Export(Req("\"name\":\"R\",\"method\":\"GET\",\"url\":\"http://a\"", "unknown")), out _);
        Assert.Equal("insomnia", bundle.Collection!.Folder.Name);
        Assert.Equal("R", Assert.IsType<ImportRequest>(Assert.Single(bundle.Collection.Children)).Request.Name);
    }

    [Fact]
    public void Parameters_are_added_after_the_url_query()
    {
        var r = One("\"method\":\"GET\",\"url\":\"http://a/x?a=1\",\"parameters\":[{\"name\":\"b\",\"value\":\"2\"},{\"name\":\"c\",\"value\":\"3\",\"disabled\":true}]", out _);
        Assert.Equal("http://a/x", r.Url);
        Assert.Equal(["a", "b", "c"], r.QueryParams.Select(q => q.Key));
        Assert.Equal([true, true, false], r.QueryParams.Select(q => q.Enabled));
    }

    [Fact]
    public void Headers_keep_disabled_state_and_managed_headers()
    {
        var r = One("\"method\":\"GET\",\"url\":\"http://a\",\"headers\":[{\"name\":\"X\",\"value\":\"1\",\"disabled\":true},{\"name\":\"Host\",\"value\":\"h\"}]", out _);
        Assert.Equal(["X", "Host"], r.Headers.Select(h => h.Key));
        Assert.False(r.Headers[0].Enabled);
    }

    [Fact]
    public void Missing_name_is_described_from_the_request()
    {
        Assert.Equal("POST a/x", One("\"method\":\"POST\",\"url\":\"http://a/x\"", out _).Name);
    }

    [Theory]
    [InlineData("application/json", RawContentType.Json)]
    [InlineData("application/xml", RawContentType.Xml)]
    [InlineData("text/xml", RawContentType.Xml)]
    [InlineData("text/html", RawContentType.Html)]
    [InlineData("text/plain", RawContentType.Text)]
    [InlineData("application/javascript", RawContentType.JavaScript)]
    [InlineData("application/graphql", RawContentType.Json)]
    public void Raw_bodies(string mime, RawContentType expected)
    {
        var r = One("\"method\":\"POST\",\"url\":\"http://a\",\"body\":{\"mimeType\":\"" + mime + "\",\"text\":\"{}\"}", out _);
        Assert.Equal(BodyKind.Raw, r.Body.Kind);
        Assert.Equal(expected, r.Body.ContentType);
        Assert.Equal("{}", r.Body.Text);
        Assert.Empty(r.Headers);
    }

    [Fact]
    public void Json_body_with_parameters_in_mime()
    {
        var r = One("\"method\":\"POST\",\"url\":\"http://a\",\"body\":{\"mimeType\":\"application/json; charset=utf-8\",\"text\":\"1\"}", out _);
        Assert.Equal(RawContentType.Json, r.Body.ContentType);
    }

    [Fact]
    public void Empty_or_absent_body_is_none()
    {
        Assert.Equal(BodyKind.None, One("\"method\":\"POST\",\"url\":\"http://a\",\"body\":{}", out _).Body.Kind);
        Assert.Equal(BodyKind.None, One("\"method\":\"POST\",\"url\":\"http://a\"", out _).Body.Kind);
        Assert.Equal(BodyKind.None, One("\"method\":\"POST\",\"url\":\"http://a\",\"body\":{\"mimeType\":\"application/json\"}", out _).Body.Kind);
    }

    [Fact]
    public void Urlencoded_body()
    {
        var r = One("\"method\":\"POST\",\"url\":\"http://a\",\"body\":{\"mimeType\":\"application/x-www-form-urlencoded\",\"params\":[{\"name\":\"a\",\"value\":\"1\"},{\"name\":\"b\",\"value\":\"2\",\"disabled\":true}]}", out _);
        Assert.Equal(BodyKind.UrlEncoded, r.Body.Kind);
        Assert.Equal(2, r.Body.Fields.Count);
        Assert.False(r.Body.Fields[1].Enabled);
    }

    [Fact]
    public void Multipart_with_a_file()
    {
        var r = One("\"name\":\"Up\",\"method\":\"POST\",\"url\":\"http://a\",\"body\":{\"mimeType\":\"multipart/form-data\",\"params\":[{\"name\":\"t\",\"value\":\"v\"},{\"name\":\"f\",\"type\":\"file\",\"fileName\":\"/tmp/x.png\"}]}", out var context);
        Assert.Equal(BodyKind.FormData, r.Body.Kind);
        Assert.Equal(FormFieldKind.Text, r.Body.Fields[0].Kind);
        Assert.Equal(FormFieldKind.File, r.Body.Fields[1].Kind);
        Assert.Equal("/tmp/x.png", r.Body.Fields[1].Value);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Warning && e.Message.Contains("/tmp/x.png"));
    }

    [Fact]
    public void Octet_stream_with_a_file_is_binary()
    {
        var r = One("\"name\":\"B\",\"method\":\"POST\",\"url\":\"http://a\",\"body\":{\"mimeType\":\"application/octet-stream\",\"fileName\":\"/tmp/b.bin\"}", out var context);
        Assert.Equal(BodyKind.Binary, r.Body.Kind);
        Assert.Equal("/tmp/b.bin", r.Body.FilePath);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Warning);
    }

    [Fact]
    public void Text_without_mime_is_raw_text()
    {
        var r = One("\"method\":\"POST\",\"url\":\"http://a\",\"body\":{\"text\":\"hi\"}", out _);
        Assert.Equal(RawContentType.Text, r.Body.ContentType);
        Assert.Equal("hi", r.Body.Text);
        Assert.Empty(r.Headers);
    }

    [Fact]
    public void Unknown_mime_adds_a_content_type_header()
    {
        var r = One("\"method\":\"POST\",\"url\":\"http://a\",\"body\":{\"mimeType\":\"application/yaml\",\"text\":\"a: 1\"}", out _);
        Assert.Equal(RawContentType.Text, r.Body.ContentType);
        var header = Assert.Single(r.Headers);
        Assert.Equal(("Content-Type", "application/yaml"), (header.Key, header.Value));
    }

    [Fact]
    public void Unknown_mime_keeps_an_existing_content_type_header()
    {
        var r = One("\"method\":\"POST\",\"url\":\"http://a\",\"headers\":[{\"name\":\"content-type\",\"value\":\"x/y\"}],\"body\":{\"mimeType\":\"application/yaml\",\"text\":\"a\"}", out _);
        Assert.Single(r.Headers);
    }

    [Theory]
    [InlineData("\"settingFollowRedirects\":\"off\"", false)]
    [InlineData("\"settingFollowRedirects\":\"on\"", true)]
    [InlineData("\"settingFollowRedirects\":\"global\"", true)]
    [InlineData("\"name\":\"n\"", true)]
    public void Redirect_settings(string field, bool expected) =>
        Assert.Equal(expected, One("\"method\":\"GET\",\"url\":\"http://a\"," + field, out _).Settings.FollowRedirects);

    [Fact]
    public void Unsupported_method_is_skipped()
    {
        var bundle = Run(Export(Workspace, Req("\"name\":\"X\",\"method\":\"PROPFIND\",\"url\":\"http://a\"")), out var context);
        Assert.Empty(bundle.Collection!.Children);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Skipped && e.Path == "My API/X");
    }

    [Fact]
    public void Grpc_and_websocket_requests_give_one_aggregated_entry()
    {
        Run(Export(
            Workspace,
            "{\"_type\":\"grpc_request\",\"_id\":\"g1\",\"parentId\":\"wrk_1\"}",
            "{\"_type\":\"grpc_request\",\"_id\":\"g2\",\"parentId\":\"wrk_1\"}",
            "{\"_type\":\"websocket_request\",\"_id\":\"w1\",\"parentId\":\"wrk_1\"}"), out var context);
        var entry = Assert.Single(context.Report.Entries, e => e.Severity == ImportSeverity.Skipped);
        Assert.Contains("3", entry.Message);
    }

    [Fact]
    public void Ignored_types_give_one_info()
    {
        Run(Export(
            Workspace,
            "{\"_type\":\"cookie_jar\",\"_id\":\"c\",\"parentId\":\"wrk_1\"}",
            "{\"_type\":\"api_spec\",\"_id\":\"a\",\"parentId\":\"wrk_1\"}",
            "{\"_type\":\"unit_test_suite\",\"_id\":\"u\",\"parentId\":\"wrk_1\"}",
            "{\"_type\":\"environment\",\"_id\":\"e\",\"parentId\":\"wrk_1\"}"), out var context);
        var entry = Assert.Single(context.Report.Entries);
        Assert.Equal(ImportSeverity.Info, entry.Severity);
        Assert.Contains("3", entry.Message);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("5")]
    public void Other_format_versions_throw(string version) =>
        Assert.Throws<ImportException>(() => Run("{\"__export_format\":" + version + ",\"resources\":[]}", out _));

    [Theory]
    [InlineData("{\"__export_format\":4}")]
    [InlineData("{\"resources\":{}}")]
    [InlineData("[]")]
    [InlineData("{")]
    public void Not_an_export_throws(string text) => Assert.Throws<ImportException>(() => Run(text, out _));

    [Fact]
    public void Only_the_first_workspace_is_imported()
    {
        var bundle = Run(Export(
            Workspace,
            "{\"_type\":\"workspace\",\"_id\":\"wrk_2\",\"name\":\"Other\"}",
            Req("\"name\":\"Mine\",\"method\":\"GET\",\"url\":\"http://a\""),
            Req("\"name\":\"Theirs\",\"method\":\"GET\",\"url\":\"http://a\"", "wrk_2")), out var context);
        Assert.Equal("Mine", Assert.IsType<ImportRequest>(Assert.Single(bundle.Collection!.Children)).Request.Name);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Skipped && e.Message.Contains('1'));
    }

    [Fact]
    public void Scripts_give_one_warning_and_descriptions_one_info()
    {
        Run(Export(
            Workspace,
            Req("\"name\":\"A\",\"method\":\"GET\",\"url\":\"http://a\",\"preRequestScript\":\"x()\",\"afterResponseScript\":\"y()\",\"description\":\"d\""),
            Req("\"name\":\"B\",\"method\":\"GET\",\"url\":\"http://a\",\"preRequestScript\":\"\"")), out var context);
        var warning = Assert.Single(context.Report.Entries, e => e.Severity == ImportSeverity.Warning);
        Assert.Contains("2", warning.Message);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Info && e.Message.Contains('1'));
    }

    [Fact]
    public void Templates_and_authentication_are_imported()
    {
        var request = One(
            "\"name\":\"R\",\"method\":\"GET\",\"url\":\"{{ _.baseUrl }}/users\",\"authentication\":{\"type\":\"bearer\",\"token\":\"{{ _.token }}\"}",
            out var context);

        Assert.Equal("{{baseUrl}}/users", request.Url);
        Assert.Equal(AuthKind.Bearer, request.Auth.Kind);
        Assert.Equal("{{token}}", request.Auth.Token);
        Assert.DoesNotContain(context.Report.Entries, e => e.Severity == ImportSeverity.Info && e.Path == "" && e.Message.Contains("plain", StringComparison.OrdinalIgnoreCase));
    }
}
