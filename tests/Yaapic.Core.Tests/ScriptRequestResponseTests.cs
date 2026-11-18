using System.Text;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class ScriptRequestResponseTests
{
    private static HttpRequestDefinition Def(
        HttpMethodKind method = HttpMethodKind.Get,
        string url = "https://a.test/x",
        RequestBody? body = null,
        IReadOnlyList<KeyValueEntry>? query = null) =>
        HttpRequestDefinition.Create("r") with
        {
            Method = method,
            Url = url,
            QueryParams = query ?? [],
            Headers = [new KeyValueEntry("X-A", "1"), new KeyValueEntry("X-Off", "2", false)],
            Body = body ?? RequestBody.None,
        };

    [Fact]
    public void RoundTripGetIsLossless()
    {
        var d = Def(query: [new KeyValueEntry("a", "1"), new KeyValueEntry("b", "{{v}}")]);
        var r = ScriptRequest.FromDefinition(d, false);
        Assert.Equal("https://a.test/x?a=1&b={{v}}", r.Url);
        Assert.True(r.ApplyTo(d).IsEquivalentTo(d));
    }

    [Fact]
    public void RoundTripRawJsonIsLossless()
    {
        var d = Def(HttpMethodKind.Post, body: RequestBody.Raw(RawContentType.Json, "{\"a\":1}"));
        var r = ScriptRequest.FromDefinition(d, false);
        Assert.Equal("raw", r.BodyMode);
        Assert.Equal("json", r.BodyContentType);
        Assert.True(r.ApplyTo(d).IsEquivalentTo(d));
    }

    [Fact]
    public void RoundTripUrlEncodedIsLossless()
    {
        var d = Def(HttpMethodKind.Post, body: RequestBody.UrlEncoded([new FormField("k", "v"), new FormField("o", "x", false)]));
        var r = ScriptRequest.FromDefinition(d, false);
        Assert.Equal(2, r.UrlEncoded.Count);
        Assert.True(r.ApplyTo(d).IsEquivalentTo(d));
    }

    [Fact]
    public void DisabledParamsSurvive()
    {
        var d = Def(query: [new KeyValueEntry("a", "1"), new KeyValueEntry("off", "2", false)]);
        var r = ScriptRequest.FromDefinition(d, false);
        Assert.Equal("https://a.test/x?a=1", r.Url);
        Assert.True(r.ApplyTo(d).IsEquivalentTo(d));
    }

    [Fact]
    public void EditsAreApplied()
    {
        var d = Def(HttpMethodKind.Post, body: RequestBody.Raw(RawContentType.Json, "{}"));
        var r = ScriptRequest.FromDefinition(d, false);
        r.Method = "PUT";
        r.Url = "https://b.test/y?q=1#f";
        r.Headers.Add(new KeyValueEntry("X-B", "3"));
        r.BodyText = "[1]";
        r.BodyContentType = "xml";
        var a = r.ApplyTo(d);
        Assert.Equal(HttpMethodKind.Put, a.Method);
        Assert.Equal("https://b.test/y#f", a.Url);
        Assert.Equal([new KeyValueEntry("q", "1")], a.QueryParams);
        Assert.Equal(3, a.Headers.Count);
        Assert.Equal(RequestBody.Raw(RawContentType.Xml, "[1]"), a.Body);
    }

    [Fact]
    public void UnknownMethodIsRejected()
    {
        var d = Def();
        var r = ScriptRequest.FromDefinition(d, false);
        r.Method = "FETCH";
        var ex = Assert.Throws<InvalidOperationException>(() => r.ApplyTo(d));
        Assert.Equal("Unknown HTTP method 'FETCH'", ex.Message);
    }

    [Fact]
    public void ReadOnlyRequestThrowsOnEveryMutation()
    {
        var r = ScriptRequest.FromDefinition(Def(), true);
        Assert.True(r.IsReadOnly);
        Action[] mutations =
        [
            () => r.Method = "POST",
            () => r.Url = "x",
            () => r.BodyMode = "raw",
            () => r.BodyText = "x",
            () => r.BodyContentType = "json",
            () => r.Headers.Add(new KeyValueEntry("a", "b")),
            () => r.Headers.RemoveAt(0),
            () => r.Headers[0] = new KeyValueEntry("a", "b"),
            () => r.Headers.Clear(),
            () => r.UrlEncoded.Add(new KeyValueEntry("a", "b")),
        ];
        foreach (var mutation in mutations)
        {
            var ex = Assert.Throws<InvalidOperationException>(mutation);
            Assert.Equal("yaapic.request is read-only in post-response scripts", ex.Message);
        }
    }

    [Fact]
    public void FormDataAndBinaryBodiesSurviveApplyTo()
    {
        var form = Def(HttpMethodKind.Post, body: RequestBody.FormData([new FormField("f", "p", true, FormFieldKind.File)]));
        var rf = ScriptRequest.FromDefinition(form, false);
        Assert.Single(rf.FormData);
        rf.Url = "https://c.test";
        Assert.Equal(form.Body, rf.ApplyTo(form).Body);

        var bin = Def(HttpMethodKind.Post, body: RequestBody.Binary("a.bin"));
        var rb = ScriptRequest.FromDefinition(bin, false);
        Assert.Equal("binary", rb.BodyMode);
        Assert.Equal(bin.Body, rb.ApplyTo(bin).Body);
    }

    private static HttpResponseData Resp(string? contentType, byte[] body, params KeyValueEntry[] extra)
    {
        List<KeyValueEntry> headers = [.. extra];
        if (contentType is not null)
        {
            headers.Add(new KeyValueEntry("Content-Type", contentType));
        }

        return new HttpResponseData(200, "OK", headers, body, TimeSpan.FromMilliseconds(12.6), "https://a.test/");
    }

    [Fact]
    public void ResponseDecodesUtf8()
    {
        var r = ScriptResponse.FromData(Resp("text/plain; charset=utf-8", Encoding.UTF8.GetBytes("héllo")));
        Assert.Equal("héllo", r.BodyText);
        Assert.Equal(13, r.ResponseTimeMs);
        Assert.Equal(6, r.SizeBytes);
        Assert.Equal(200, r.Code);
        Assert.Equal("OK", r.Status);
    }

    [Fact]
    public void ResponseDecodesLatin1()
    {
        var r = ScriptResponse.FromData(Resp("text/plain; charset=ISO-8859-1", [0x68, 0xE9]));
        Assert.Equal("hé", r.BodyText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("text/plain; charset=nope")]
    public void ResponseFallsBackToUtf8(string? contentType)
    {
        var r = ScriptResponse.FromData(Resp(contentType, Encoding.UTF8.GetBytes("é")));
        Assert.Equal("é", r.BodyText);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("application/octet-stream")]
    [InlineData("application/pdf")]
    [InlineData("audio/mpeg")]
    [InlineData("video/mp4")]
    [InlineData("application/zip")]
    public void BinaryContentTypeGivesEmptyText(string contentType)
    {
        var r = ScriptResponse.FromData(Resp(contentType, [1, 2, 3]));
        Assert.Equal("", r.BodyText);
        Assert.Equal(3, r.SizeBytes);
    }

    [Fact]
    public void ResponseCarriesCookies()
    {
        var r = ScriptResponse.FromData(Resp("text/plain", [], new KeyValueEntry("Set-Cookie", "a=b")));
        Assert.Equal("a", Assert.Single(r.Cookies).Name);
    }

    [Fact]
    public void CookieFlagsAndAttributes()
    {
        var cookies = SetCookieParser.Parse([new KeyValueEntry("set-cookie",
            "sid=abc; DOMAIN=a.test; path=/p; Expires=Wed, 21 Oct 2026 07:28:00 GMT; HttpOnly; secure; SameSite=Lax; Max-Age=60")]);
        var c = Assert.Single(cookies);
        Assert.Equal(new ScriptCookie("sid", "abc", "a.test", "/p", new DateTimeOffset(2026, 10, 21, 7, 28, 0, TimeSpan.Zero), true, true, "Lax"), c);
    }

    [Fact]
    public void CookieMaxAgeIsIgnoredAndValueUnquoted()
    {
        var c = Assert.Single(SetCookieParser.Parse([new KeyValueEntry("Set-Cookie", "q=\"x y\"; Max-Age=60")]));
        Assert.Equal("x y", c.Value);
        Assert.Null(c.Expires);
        Assert.False(c.HttpOnly);
        Assert.Null(c.Domain);
    }

    [Fact]
    public void TwoCookiesAndMalformedHeaders()
    {
        var cookies = SetCookieParser.Parse(
        [
            new KeyValueEntry("Set-Cookie", "a=1"),
            new KeyValueEntry("Set-Cookie", "garbage"),
            new KeyValueEntry("Set-Cookie", "=novalue"),
            new KeyValueEntry("X-Other", "z=9"),
            new KeyValueEntry("Set-Cookie", "b=2; Expires=notadate"),
        ]);
        Assert.Equal(["a", "b"], cookies.Select(c => c.Name));
        Assert.Null(cookies[1].Expires);
    }
}
