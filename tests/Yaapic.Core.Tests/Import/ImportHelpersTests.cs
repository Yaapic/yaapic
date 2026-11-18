using Yaapic.Core.Import;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Tests.Import;

public class ImportHelpersTests
{
    [Theory]
    [InlineData("get", HttpMethodKind.Get)]
    [InlineData("Post", HttpMethodKind.Post)]
    [InlineData(" PUT ", HttpMethodKind.Put)]
    [InlineData("pAtCh", HttpMethodKind.Patch)]
    [InlineData("delete", HttpMethodKind.Delete)]
    [InlineData("Head", HttpMethodKind.Head)]
    [InlineData("OPTIONS", HttpMethodKind.Options)]
    public void TryParseMethod_accepts_the_seven_kinds_in_any_case(string text, HttpMethodKind expected)
    {
        Assert.True(ImportHelpers.TryParseMethod(text, out var method));
        Assert.Equal(expected, method);
    }

    [Theory]
    [InlineData("TRACE")]
    [InlineData("CONNECT")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseMethod_rejects_everything_else(string? text) =>
        Assert.False(ImportHelpers.TryParseMethod(text, out _));

    [Fact]
    public void SafeName_cleans_cuts_and_falls_back()
    {
        Assert.Equal("a b c", ImportHelpers.SafeName("  a\r\nb\t\tc ", "f"));
        Assert.Equal(120, ImportHelpers.SafeName(new string('x', 300), "f").Length);
        Assert.Equal("f", ImportHelpers.SafeName("  \n ", "f"));
        Assert.Equal("f", ImportHelpers.SafeName(null, "f"));
    }

    [Fact]
    public void DescribeRequest_drops_scheme_query_and_fragment()
    {
        Assert.Equal("GET api.example.com/users", ImportHelpers.DescribeRequest(HttpMethodKind.Get, "https://api.example.com/users?x=1#top"));
        Assert.Equal("GET {{baseUrl}}/users", ImportHelpers.DescribeRequest(HttpMethodKind.Get, "{{baseUrl}}/users"));
        Assert.Equal("DELETE", ImportHelpers.DescribeRequest(HttpMethodKind.Delete, ""));
        Assert.Equal(80, ImportHelpers.DescribeRequest(HttpMethodKind.Post, "http://h/" + new string('p', 200)).Length);
    }

    [Fact]
    public void SplitUrl_separates_the_query()
    {
        var (url, query) = ImportHelpers.SplitUrl("http://h/p?a=1&b={{v}}#frag");
        Assert.Equal("http://h/p#frag", url);
        Assert.Equal(["a", "b"], query.Select(q => q.Key));
        Assert.Equal("{{v}}", query[1].Value);

        var (plain, none) = ImportHelpers.SplitUrl("http://h/p");
        Assert.Equal("http://h/p", plain);
        Assert.Empty(none);
    }

    [Fact]
    public void SubstitutePathVariables_only_touches_path_segments()
    {
        var values = new Dictionary<string, string> { ["id"] = "42", ["empty"] = "" };
        Assert.Equal("http://h/users/42", ImportHelpers.SubstitutePathVariables("http://h/users/:id", values));
        Assert.Equal("http://h/users/{{other}}", ImportHelpers.SubstitutePathVariables("http://h/users/:other", values));
        Assert.Equal("http://h/{{empty}}", ImportHelpers.SubstitutePathVariables("http://h/:empty", values));
        Assert.Equal("http://localhost:3000/users/{{x}}", ImportHelpers.SubstitutePathVariables("http://localhost:3000/users/:x", values));
        Assert.Equal("http://h/users?q=:id", ImportHelpers.SubstitutePathVariables("http://h/users?q=:id", values));
        Assert.Equal("{{baseUrl}}/a/42/b", ImportHelpers.SubstitutePathVariables("{{baseUrl}}/a/:id/b", values));
    }

    [Theory]
    [InlineData("application/json; charset=utf-8", RawContentType.Json)]
    [InlineData("application/vnd.api+json", RawContentType.Json)]
    [InlineData("text/xml", RawContentType.Xml)]
    [InlineData("text/html", RawContentType.Html)]
    [InlineData("application/javascript", RawContentType.JavaScript)]
    [InlineData("text/plain", RawContentType.Text)]
    [InlineData(null, RawContentType.Text)]
    public void ContentTypeFromMime_maps_known_types(string? mime, RawContentType expected) =>
        Assert.Equal(expected, ImportHelpers.ContentTypeFromMime(mime));

    [Theory]
    [InlineData("Content-Length")]
    [InlineData("host")]
    [InlineData("CONNECTION")]
    [InlineData("Accept-Encoding")]
    [InlineData("Transfer-Encoding")]
    [InlineData("Keep-Alive")]
    [InlineData("te")]
    [InlineData("Upgrade")]
    [InlineData(":authority")]
    public void IsManagedHeader_is_true_for_stack_headers(string name) => Assert.True(ImportHelpers.IsManagedHeader(name));

    [Fact]
    public void IsManagedHeader_is_false_for_user_headers() => Assert.False(ImportHelpers.IsManagedHeader("X-Api-Key"));

    [Fact]
    public void TranslateDynamicVariables_rewrites_known_and_warns_once_on_unknown()
    {
        var report = new ImportReport();
        Assert.Equal("{{$guid}}-{{ $guid }}-{{normal}}", ImportHelpers.TranslateDynamicVariables("{{$randomUUID}}-{{ $guid }}-{{normal}}", "p", report));
        Assert.Empty(report.Entries);

        var text = "{{$randomFirstName}} {{$randomFirstName}}";
        Assert.Equal(text, ImportHelpers.TranslateDynamicVariables(text, "p", report));
        var entry = Assert.Single(report.Entries);
        Assert.Equal(ImportSeverity.Warning, entry.Severity);
        Assert.Equal(Strings.ImportDynamicVariableUnsupported("$randomFirstName"), entry.Message);
        Assert.Contains("{{$randomFirstName}}", entry.Message);

        Assert.Equal("plain", ImportHelpers.TranslateDynamicVariables("plain", "p", report));
        Assert.Equal("", ImportHelpers.TranslateDynamicVariables("", "p", report));
    }
}
