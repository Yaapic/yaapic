using Yaapic.Core.CodeGeneration;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests.CodeGeneration;

public class CodeRequestFactoryTests
{
    [Fact]
    public void No_body_maps_to_None_without_content_type()
    {
        var r = CodeSamples.GetSimple;
        Assert.IsType<CodeBody.None>(r.Body);
        Assert.Equal("GET", r.Method);
        Assert.Single(r.Headers);
        Assert.Null(r.AuthNote);
    }

    [Fact]
    public void Json_text_maps_to_Text_and_adds_content_type()
    {
        var r = CodeSamples.PostJson;
        var t = Assert.IsType<CodeBody.Text>(r.Body);
        Assert.Equal(CodeSamples.JsonBody, t.Content);
        Assert.Equal("application/json", t.ContentType);
        Assert.Contains(r.Headers, h => h.Key == "Content-Type" && h.Value == "application/json");
    }

    [Fact]
    public void Content_type_is_not_added_when_user_header_exists()
    {
        var r = CodeRequestFactory.From(CodeSamples.Make(
            HttpMethodKind.Post, "http://a", [new("content-type", "text/x")], "b", "text/plain"));
        Assert.Single(r.Headers, h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Form_is_decoded_with_plus_and_percent()
    {
        var r = CodeRequestFactory.From(CodeSamples.Make(
            HttpMethodKind.Post, "http://a", [], "a=1&b=x+y&c=p%20q&d", "application/x-www-form-urlencoded"));
        var f = Assert.IsType<CodeBody.Form>(r.Body);
        Assert.Equal([("a", "1"), ("b", "x y"), ("c", "p q"), ("d", "")], f.Fields.Select(x => (x.Key, x.Value)));
    }

    [Fact]
    public void Form_sample_decodes_space()
    {
        var f = Assert.IsType<CodeBody.Form>(CodeSamples.PostForm.Body);
        Assert.Equal("x y", f.Fields[1].Value);
    }

    [Fact]
    public void Multipart_has_parts_and_no_content_type_header()
    {
        var r = CodeSamples.PostMultipart;
        var m = Assert.IsType<CodeBody.Multipart>(r.Body);
        Assert.Equal(2, m.Parts.Count);
        Assert.Equal("/tmp/report.pdf", m.Parts[1].FilePath);
        Assert.Empty(r.Headers);
    }

    [Fact]
    public void File_body_maps_and_adds_content_type()
    {
        var r = CodeSamples.PutFile;
        var f = Assert.IsType<CodeBody.File>(r.Body);
        Assert.Equal("/tmp/data.bin", f.Path);
        Assert.Equal("PUT", r.Method);
        Assert.Contains(r.Headers, h => h.Key == "Content-Type");
    }

    [Theory]
    [InlineData(AuthKind.OAuth2)]
    [InlineData(AuthKind.Jwt)]
    public void Bearer_kinds_add_authorization_placeholder(AuthKind kind)
    {
        var r = CodeRequestFactory.From(CodeSamples.Make(
            HttpMethodKind.Get, "http://a", [], auth: new ResolvedAuth(kind, "", "", null, null, null)));
        Assert.Contains(r.Headers, h => h.Key == "Authorization" && h.Value == "Bearer <access token>");
        Assert.Null(r.AuthNote);
    }

    [Fact]
    public void Digest_sets_note_only()
    {
        var r = CodeSamples.DigestNote;
        Assert.Empty(r.Headers);
        Assert.Equal("Digest authentication must be computed by the caller (RFC 7616).", r.AuthNote);
    }

    [Fact]
    public void AwsV4_sets_note_only()
    {
        var r = CodeRequestFactory.From(CodeSamples.Make(
            HttpMethodKind.Get, "http://a", [], auth: new ResolvedAuth(AuthKind.AwsV4, "", "", null, null, null)));
        Assert.Empty(r.Headers);
        Assert.Equal("AWS Signature v4 must be computed by the caller.", r.AuthNote);
    }

    [Fact]
    public void Unresolved_variables_stay_in_the_url() =>
        Assert.Equal("https://{{host}}/users", CodeSamples.Unresolved.Url);

    [Fact]
    public void Awkward_header_value_is_kept() =>
        Assert.Equal("say \"hi\" it's", CodeSamples.HeadersAwkward.Headers[0].Value);

    [Theory]
    [InlineData(HttpMethodKind.Patch, "PATCH")]
    [InlineData(HttpMethodKind.Delete, "DELETE")]
    [InlineData(HttpMethodKind.Options, "OPTIONS")]
    public void Method_is_upper_case(HttpMethodKind kind, string expected) =>
        Assert.Equal(expected, CodeRequestFactory.From(CodeSamples.Make(kind, "http://a", [])).Method);

    [Fact]
    public void CodeText_helpers()
    {
        Assert.Equal(["a", "b", "c"], CodeText.Lines("a\r\nb\nc"));
        Assert.Equal("a\nb\n", CodeText.JoinLines(["a", "b"]));
        Assert.True(CodeText.IsJson("application/json; charset=utf-8"));
        Assert.True(CodeText.IsJson("application/vnd.api+json"));
        Assert.False(CodeText.IsJson("text/plain"));
        Assert.False(CodeText.IsJson(null));
    }
}
