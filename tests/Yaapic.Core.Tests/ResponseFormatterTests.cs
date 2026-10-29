using System.Text;
using Yaapic.Core.Responses;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class ResponseFormatterTests
{
    static HttpResponseData Resp(byte[] body, string? contentType = null) =>
        new(200, "OK", contentType is null ? [] : [new KeyValueEntry("Content-Type", contentType)], body, TimeSpan.Zero, "http://x");

    static HttpResponseData Resp(string body, string? contentType = null) => Resp(Encoding.UTF8.GetBytes(body), contentType);

    [Fact]
    public void Json_is_indented_with_nested_array_unicode_and_verbatim_number()
    {
        var f = ResponseFormatter.Format(Resp("{\"a\":{\"b\":[1,2]},\"s\":\"é<\",\"n\":12345678901234567890.10}", "application/json"), BodyViewMode.Pretty);

        Assert.True(f.PrettyApplied);
        Assert.Equal("json", f.Language);
        Assert.Equal("{\n  \"a\": {\n    \"b\": [\n      1,\n      2\n    ]\n  },\n  \"s\": \"é<\",\n  \"n\": 12345678901234567890.10\n}", f.Text);
    }

    [Fact]
    public void Invalid_json_falls_back_to_raw()
    {
        var f = ResponseFormatter.Format(Resp("{oops", "application/json"), BodyViewMode.Pretty);

        Assert.False(f.PrettyApplied);
        Assert.Equal("{oops", f.Text);
    }

    [Fact]
    public void Xml_is_indented_without_declaration()
    {
        var f = ResponseFormatter.Format(Resp("<?xml version=\"1.0\"?><a><b>1</b></a>", "text/xml"), BodyViewMode.Pretty);

        Assert.True(f.PrettyApplied);
        Assert.Equal("xml", f.Language);
        Assert.Equal("<a>\n  <b>1</b>\n</a>", f.Text);
    }

    [Fact]
    public void Xml_with_doctype_falls_back()
    {
        const string xml = "<!DOCTYPE a [<!ENTITY e SYSTEM \"file:///x\">]><a>&e;</a>";
        var f = ResponseFormatter.Format(Resp(xml, "application/xml"), BodyViewMode.Pretty);

        Assert.False(f.PrettyApplied);
        Assert.Equal(xml, f.Text);
    }

    [Theory]
    [InlineData("application/vnd.api+json; charset=utf-8", "json")]
    [InlineData("application/atom+xml", "xml")]
    [InlineData("TEXT/HTML", "html")]
    [InlineData("text/plain", "text")]
    public void Language_comes_from_media_type(string contentType, string language) =>
        Assert.Equal(language, ResponseFormatter.Format(Resp("[]", contentType), BodyViewMode.Raw).Language);

    [Theory]
    [InlineData("  {\"a\":1}", "json")]
    [InlineData("[1]", "json")]
    [InlineData("<a/>", "xml")]
    [InlineData("hello", "text")]
    public void Language_is_sniffed_without_content_type(string body, string language) =>
        Assert.Equal(language, ResponseFormatter.Format(Resp(body), BodyViewMode.Raw).Language);

    [Fact]
    public void Charset_is_honoured()
    {
        var body = Encoding.Latin1.GetBytes("é");
        Assert.Equal("é", ResponseFormatter.Format(Resp(body, "text/plain; charset=iso-8859-1"), BodyViewMode.Raw).Text);
    }

    [Fact]
    public void Unknown_charset_falls_back_to_utf8() =>
        Assert.Equal("é", ResponseFormatter.Format(Resp("é", "text/plain; charset=nope"), BodyViewMode.Raw).Text);

    [Fact]
    public void Utf8_bom_is_stripped()
    {
        byte[] body = [0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i'];
        Assert.Equal("hi", ResponseFormatter.Format(Resp(body, "text/plain"), BodyViewMode.Raw).Text);
    }

    [Fact]
    public void Binary_by_media_type()
    {
        var f = ResponseFormatter.Format(Resp("abc", "image/png"), BodyViewMode.Pretty);

        Assert.True(f.IsBinary);
        Assert.Equal("", f.Text);
        Assert.False(f.PrettyApplied);
    }

    [Fact]
    public void Binary_by_nul_byte() =>
        Assert.True(ResponseFormatter.Format(Resp([1, 0, 2], "text/plain"), BodyViewMode.Raw).IsBinary);

    [Fact]
    public void Raw_mode_leaves_json_untouched()
    {
        var f = ResponseFormatter.Format(Resp("{\"a\":1}", "application/json"), BodyViewMode.Raw);

        Assert.False(f.PrettyApplied);
        Assert.Equal("{\"a\":1}", f.Text);
    }

    [Fact]
    public void Body_above_two_mib_is_not_pretty_printed()
    {
        var body = "[" + new string('1', 2 * 1024 * 1024) + "]";
        var f = ResponseFormatter.Format(Resp(body, "application/json"), BodyViewMode.Pretty);

        Assert.False(f.PrettyApplied);
        Assert.Equal(body, f.Text);
    }
}
