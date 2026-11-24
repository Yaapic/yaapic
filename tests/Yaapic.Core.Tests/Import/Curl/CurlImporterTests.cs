using Yaapic.Core.CodeGeneration;
using Yaapic.Core.Import;
using Yaapic.Core.Import.Curl;
using Yaapic.Core.Tests.CodeGeneration;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.Curl;

public class CurlImporterTests
{
    private static readonly CurlImporter Importer = new();

    private static ImportBundle Run(string text, out ImportContext context)
    {
        context = new ImportContext(SequentialIds.Create(), ImportOptions.Default);
        return Importer.Import(ImportInput.FromText("cmd.sh", text), context);
    }

    private static HttpRequestDefinition One(string text) => One(text, out _);

    private static HttpRequestDefinition One(string text, out ImportContext context) =>
        Assert.Single(Run(text, out context).UnfiledRequests);

    private static KeyValueEntry H(string key, string value) => new(key, value);

    [Fact]
    public void Identity()
    {
        Assert.Equal("curl", Importer.Id);
        Assert.Equal("cURL command", Importer.DisplayName);
    }

    [Theory]
    [InlineData("curl https://a.test", ImportConfidence.Certain)]
    [InlineData("  \n$ curl.exe https://a.test", ImportConfidence.Certain)]
    [InlineData("# note\ncurl https://a.test", ImportConfidence.Possible)]
    [InlineData("echo hi\n  curl https://a.test", ImportConfidence.Possible)]
    [InlineData("curling is fun", ImportConfidence.None)]
    [InlineData("{\"info\": {}}", ImportConfidence.None)]
    public void Sniff_cases(string text, ImportConfidence expected) =>
        Assert.Equal(expected, Importer.Sniff(ImportInput.FromText("a", text)));

    [Fact]
    public void Sniff_without_file_is_none() => Assert.Equal(ImportConfidence.None, Importer.Sniff(new ImportInput("a", [])));

    [Fact]
    public void Minimal_command()
    {
        var r = One("curl https://a.test/users");

        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), r.Id);
        Assert.Equal("GET a.test/users", r.Name);
        Assert.Equal(HttpMethodKind.Get, r.Method);
        Assert.Equal("https://a.test/users", r.Url);
        Assert.Empty(r.QueryParams);
        Assert.Empty(r.Headers);
        Assert.Equal(RequestBody.None, r.Body);
        Assert.Equal(RequestAuth.None, r.Auth);
        Assert.True(r.Settings.FollowRedirects);
        Assert.Equal(0, r.Settings.TimeoutSeconds);
    }

    [Fact]
    public void Query_is_split_into_params()
    {
        var r = One("curl 'https://a.test/x?a=1&b=two%20words'");

        Assert.Equal("https://a.test/x", r.Url);
        Assert.Equal([new("a", "1"), new("b", "two words")], r.QueryParams);
    }

    [Fact]
    public void Post_with_json_body()
    {
        var r = One("""curl -X POST https://a.test/u -H 'Content-Type: application/json' -d '{"a":1}'""");

        Assert.Equal(HttpMethodKind.Post, r.Method);
        Assert.Equal([H("Content-Type", "application/json")], r.Headers);
        Assert.Equal(RequestBody.Raw(RawContentType.Json, """{"a":1}"""), r.Body);
    }

    [Fact]
    public void Data_implies_post_and_several_are_joined()
    {
        var r = One("curl https://a.test -d a=1 --data-raw b=2");

        Assert.Equal(HttpMethodKind.Post, r.Method);
        Assert.Equal(RequestBody.UrlEncoded([new("a", "1"), new("b", "2")]), r.Body);
        Assert.Empty(r.Headers);
    }

    [Fact]
    public void Browser_bash_form()
    {
        var r = One("curl 'https://a.test/api' \\\n  -H $'X-Note: it\\'s' \\\n  -H 'accept: */*' \\\n  --data-raw $'{\"q\":\"caf\\u00e9\"}' \\\n  -H 'content-type: application/json' \\\n  --compressed");

        Assert.Equal(HttpMethodKind.Post, r.Method);
        Assert.Equal([H("X-Note", "it's"), H("accept", "*/*"), H("content-type", "application/json")], r.Headers);
        Assert.Equal(RequestBody.Raw(RawContentType.Json, "{\"q\":\"café\"}"), r.Body);
    }

    [Fact]
    public void Free_text_without_content_type_is_raw_text_with_an_added_header()
    {
        var r = One("curl https://a.test -d 'hello world'");

        Assert.Equal(RequestBody.Raw(RawContentType.Text, "hello world"), r.Body);
        Assert.Equal([H("Content-Type", "application/x-www-form-urlencoded")], r.Headers);
    }

    [Fact]
    public void Form_content_type_with_pairs_is_url_encoded()
    {
        var r = One("curl https://a.test -H 'Content-Type: application/x-www-form-urlencoded' -d 'a=x%20y&b=2'");

        Assert.Equal(RequestBody.UrlEncoded([new("a", "x y"), new("b", "2")]), r.Body);
        Assert.Single(r.Headers);
    }

    [Fact]
    public void Other_content_type_is_raw()
    {
        var r = One("curl https://a.test -H 'Content-Type: text/xml' -d '<a/>'");

        Assert.Equal(RequestBody.Raw(RawContentType.Xml, "<a/>"), r.Body);
    }

    [Fact]
    public void Data_urlencode_pairs()
    {
        var r = One("curl https://a.test --data-urlencode 'a=x y' --data-urlencode '=lonely' --data-urlencode bare");

        Assert.Equal(RequestBody.UrlEncoded([new("a", "x y"), new("", "lonely"), new("", "bare")]), r.Body);
    }

    [Fact]
    public void Data_urlencode_file_is_dropped_with_a_warning()
    {
        var r = One("curl https://a.test --data-urlencode 'a@f.txt'", out var context);

        Assert.Equal(HttpMethodKind.Get, r.Method);
        Assert.Equal(RequestBody.None, r.Body);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(context.Report.Entries).Severity);
    }

    [Fact]
    public void Form_fields_with_file_and_type()
    {
        var r = One("curl https://a.test/up -H 'Content-Type: multipart/form-data' -F title=Report -F 'file=@/tmp/r.pdf;type=application/pdf' -F 'note=<n.txt' --form-string 'raw=@x'", out var context);

        Assert.Empty(r.Headers);
        Assert.Equal(
            RequestBody.FormData([
                new("title", "Report"),
                new("file", "/tmp/r.pdf", true, FormFieldKind.File),
                new("note", "n.txt", true, FormFieldKind.File),
                new("raw", "@x"),
            ]),
            r.Body);
        Assert.Equal(2, context.Report.Count(ImportSeverity.Warning));
    }

    [Fact]
    public void Data_file_is_a_binary_body_with_a_warning()
    {
        var r = One("curl https://a.test --data-binary @/tmp/b.bin", out var context);

        Assert.Equal(RequestBody.Binary("/tmp/b.bin"), r.Body);
        Assert.Equal(1, context.Report.Count(ImportSeverity.Warning));
    }

    [Fact]
    public void Data_raw_keeps_a_leading_at_sign()
    {
        Assert.Equal(RequestBody.Raw(RawContentType.Text, "@x y"), One("curl https://a.test --data-raw '@x y'").Body);
    }

    [Fact]
    public void Basic_digest_and_bearer_auth()
    {
        Assert.Equal(
            RequestAuth.None with { Kind = AuthKind.Basic, Username = "user", Password = "pa:ss" },
            One("curl -u user:pa:ss https://a.test").Auth);
        Assert.Equal(
            RequestAuth.None with { Kind = AuthKind.Digest, Username = "user", Password = "x" },
            One("curl --digest -u user:x https://a.test").Auth);
        Assert.Equal(
            RequestAuth.None with { Kind = AuthKind.Bearer, Token = "tok" },
            One("curl --oauth2-bearer tok https://a.test").Auth);
    }

    [Fact]
    public void Ntlm_is_a_warning()
    {
        One("curl --ntlm -u a:b https://a.test", out var context);

        Assert.Equal(ImportSeverity.Warning, Assert.Single(context.Report.Entries).Severity);
    }

    [Fact]
    public void User_agent_referer_and_cookie()
    {
        var r = One("curl https://a.test -H 'user-agent: old' -A Mine -e https://r.test -b 'a=1; b=2' -b jar.txt", out var context);

        Assert.Equal([H("User-Agent", "Mine"), H("Referer", "https://r.test"), H("Cookie", "a=1; b=2")], r.Headers);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(context.Report.Entries).Severity);
    }

    [Fact]
    public void Header_forms()
    {
        var r = One("curl https://a.test -H 'A: 1' -H 'A: 2' -H 'Empty;' -H 'Gone:' -H 'junk'", out var context);

        Assert.Equal([H("A", "1"), H("A", "2"), H("Empty", "")], r.Headers);
        Assert.Equal(1, context.Report.Count(ImportSeverity.Info));
        Assert.Equal(1, context.Report.Count(ImportSeverity.Warning));
    }

    [Fact]
    public void Head_option() => Assert.Equal(HttpMethodKind.Head, One("curl -I https://a.test").Method);

    [Fact]
    public void Explicit_method_wins()
    {
        Assert.Equal(HttpMethodKind.Put, One("curl -I -X put https://a.test -d a=1").Method);
        Assert.Equal(HttpMethodKind.Delete, One("curl --request=DELETE https://a.test").Method);
    }

    [Fact]
    public void Get_option_moves_data_to_the_query()
    {
        var r = One("curl -G https://a.test/s?x=0 -d a=b -d 'c=d%20e' --data-urlencode 'q=1 2'");

        Assert.Equal(HttpMethodKind.Get, r.Method);
        Assert.Equal("https://a.test/s", r.Url);
        Assert.Equal([new("x", "0"), new("a", "b"), new("c", "d e"), new("q", "1 2")], r.QueryParams);
        Assert.Equal(RequestBody.None, r.Body);
    }

    [Fact]
    public void Settings_options()
    {
        Assert.Equal(3, One("curl -m 2.5 https://a.test").Settings.TimeoutSeconds);
        Assert.Equal(0, One("curl -m 0 https://a.test").Settings.TimeoutSeconds);
        Assert.False(One("curl --max-redirs 0 https://a.test").Settings.FollowRedirects);
        Assert.True(One("curl --max-redirs 0 -L https://a.test").Settings.FollowRedirects);
        Assert.Equal(HttpVersionPreference.Http2, One("curl --http2 https://a.test").Settings.HttpVersion);
        Assert.Equal(HttpVersionPreference.Http1, One("curl --http1.1 https://a.test").Settings.HttpVersion);
        Assert.Equal(HttpVersionPreference.Http3, One("curl --http3 https://a.test").Settings.HttpVersion);
    }

    [Fact]
    public void Flag_group_is_expanded_and_url_option_works()
    {
        var r = One("curl -sSL --url=https://a.test -XPOST", out var context);

        Assert.Equal("https://a.test", r.Url);
        Assert.Equal(HttpMethodKind.Post, r.Method);
        Assert.True(r.Settings.FollowRedirects);
        Assert.Empty(context.Report.Entries);
    }

    [Fact]
    public void Insecure_proxy_and_unknown_options_warn()
    {
        One("curl -k --proxy http://p.test --weird https://a.test", out var context);

        Assert.Equal(3, context.Report.Count(ImportSeverity.Warning));
        Assert.All(context.Report.Entries, e => Assert.Equal("GET a.test", e.Path));
    }

    [Fact]
    public void Output_option_does_not_swallow_the_url()
    {
        var r = One("curl -O https://a.test/f.zip", out var context);

        Assert.Equal("https://a.test/f.zip", r.Url);
        Assert.Equal(1, context.Report.Count(ImportSeverity.Warning));
    }

    [Fact]
    public void Second_url_warns()
    {
        var r = One("curl https://a.test https://b.test", out var context);

        Assert.Equal("https://a.test", r.Url);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(context.Report.Entries).Severity);
    }

    [Fact]
    public void Unsupported_method_is_skipped()
    {
        var bundle = Run("curl -X TRACE https://a.test", out var context);

        Assert.Empty(bundle.UnfiledRequests);
        Assert.Equal(ImportSeverity.Skipped, Assert.Single(context.Report.Entries).Severity);
    }

    [Fact]
    public void Missing_url_is_skipped()
    {
        var bundle = Run("curl -H 'A: b'", out var context);

        Assert.Empty(bundle.UnfiledRequests);
        Assert.Equal(ImportSeverity.Skipped, Assert.Single(context.Report.Entries).Severity);
    }

    [Fact]
    public void Two_commands_give_two_requests()
    {
        var bundle = Run("curl https://a.test/1\ncurl -X POST https://a.test/2", out _);

        Assert.Equal(["https://a.test/1", "https://a.test/2"], bundle.UnfiledRequests.Select(r => r.Url));
        Assert.Equal(2, bundle.RequestCount);
    }

    [Fact]
    public void No_command_or_bad_quote_throws()
    {
        Assert.Throws<ImportException>(() => Run("echo hi", out _));
        Assert.Throws<ImportException>(() => Run("curl 'oops", out _));
    }

    [Theory]
    [InlineData(nameof(CodeSamples.GetSimple))]
    [InlineData(nameof(CodeSamples.PostJson))]
    [InlineData(nameof(CodeSamples.PostForm))]
    [InlineData(nameof(CodeSamples.PostMultipart))]
    [InlineData(nameof(CodeSamples.HeadersAwkward))]
    public void Generated_curl_imports_back(string sample)
    {
        var original = sample switch
        {
            nameof(CodeSamples.GetSimple) => CodeSamples.GetSimple,
            nameof(CodeSamples.PostJson) => CodeSamples.PostJson,
            nameof(CodeSamples.PostForm) => CodeSamples.PostForm,
            nameof(CodeSamples.PostMultipart) => CodeSamples.PostMultipart,
            _ => CodeSamples.HeadersAwkward,
        };

        var r = One(new CurlCodeGenerator().Generate(original));

        Assert.Equal(original.Method, r.Method.ToText());
        var url = original.Url;
        var cut = url.IndexOf('?', StringComparison.Ordinal);
        Assert.Equal(cut < 0 ? url : url[..cut], r.Url);
        foreach (var header in original.Headers)
        {
            Assert.Contains(r.Headers, h => h.Key == header.Key && h.Value == header.Value);
        }

        switch (original.Body)
        {
            case CodeBody.Text text:
                Assert.Equal(RequestBody.Raw(RawContentType.Json, text.Content), r.Body);
                break;
            case CodeBody.Form form:
                Assert.Equal(RequestBody.UrlEncoded([.. form.Fields.Select(f => new FormField(f.Key, f.Value))]), r.Body);
                break;
            case CodeBody.Multipart multipart:
                Assert.Equal(BodyKind.FormData, r.Body.Kind);
                Assert.Equal(multipart.Parts.Count, r.Body.Fields.Count);
                break;
            default:
                Assert.Equal(RequestBody.None, r.Body);
                break;
        }
    }
}
