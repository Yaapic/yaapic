using Yaapic.Core.Http;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class RequestResolverTests
{
    static IVariableSource Env(params (string K, string V)[] vars) =>
        new EnvironmentVariableSource(EnvironmentDefinition.Create("e") with { Variables = [.. vars.Select(v => new KeyValueEntry(v.K, v.V))] });

    static HttpRequestDefinition Req(string url, HttpMethodKind method = HttpMethodKind.Get, KeyValueEntry[]? query = null,
        KeyValueEntry[]? headers = null, RequestBody? body = null) =>
        HttpRequestDefinition.Create("r") with { Url = url, Method = method, QueryParams = query ?? [], Headers = headers ?? [], Body = body ?? RequestBody.None };

    static ResolvedRequest R(HttpRequestDefinition r, params (string K, string V)[] vars) => RequestResolver.Resolve(r, Env(vars));

    [Fact]
    public void Prefixes_scheme_only_when_missing()
    {
        Assert.Equal("http://a.com", R(Req(" a.com ")).Url);
        Assert.Equal("https://a.com", R(Req("https://a.com")).Url);
        Assert.Equal("", R(Req("  ")).Url);
    }

    [Theory]
    [InlineData("http://a", "http://a?k=v")]
    [InlineData("http://a?x=1", "http://a?x=1&k=v")]
    [InlineData("http://a?", "http://a?k=v")]
    [InlineData("http://a?x=1&", "http://a?x=1&k=v")]
    [InlineData("http://a#frag", "http://a?k=v#frag")]
    [InlineData("http://a?x=1#frag", "http://a?x=1&k=v#frag")]
    public void Appends_params(string url, string expected) =>
        Assert.Equal(expected, R(Req(url, query: [new("k", "v")])).Url);

    [Fact]
    public void Escapes_keys_and_values() =>
        Assert.Equal("http://a?a%20b=c%26d%3De&%C3%A9=x",
            R(Req("http://a", query: [new("a b", "c&d=e"), new("é", "x")])).Url);

    [Fact]
    public void Drops_disabled_and_empty_key_entries()
    {
        var r = R(Req("http://a", query: [new("a", "1", false), new("", "2"), new("{{k}}", "3"), new("b", "4")],
            headers: [new("H", "1", false), new("{{k}}", "x"), new("G", "2")]), ("k", ""));
        Assert.Equal("http://a?b=4", r.Url);
        Assert.Equal([new KeyValueEntry("G", "2")], r.Headers);
    }

    [Fact]
    public void Resolves_variables_everywhere()
    {
        var r = R(Req("{{h}}/p", HttpMethodKind.Post, [new("{{k}}", "{{v}}")], [new("X-{{k}}", "{{v}}")], RequestBody.Raw(RawContentType.Text, "b={{v}}")),
            ("h", "a.com"), ("k", "key"), ("v", "val"));
        Assert.Equal("http://a.com/p?key=val", r.Url);
        Assert.Equal([new KeyValueEntry("X-key", "val")], r.Headers);
        Assert.Equal("b=val", r.BodyText);
    }

    [Fact]
    public void Get_drops_body()
    {
        var r = R(Req("http://a", body: RequestBody.Raw(RawContentType.Json, "{}")));
        Assert.Null(r.BodyText);
        Assert.Null(r.BodyContentType);
    }

    [Theory]
    [InlineData(RawContentType.Text, "text/plain; charset=utf-8")]
    [InlineData(RawContentType.Json, "application/json")]
    [InlineData(RawContentType.Xml, "application/xml")]
    public void Post_default_content_type(RawContentType type, string expected)
    {
        var r = R(Req("http://a", HttpMethodKind.Post, body: RequestBody.Raw(type, "x")));
        Assert.Equal(expected, r.BodyContentType);
    }

    [Fact]
    public void Post_without_raw_body_has_no_body() =>
        Assert.Null(R(Req("http://a", HttpMethodKind.Post)).BodyText);

    [Fact]
    public void User_content_type_overrides_and_is_removed_for_post_body()
    {
        var r = R(Req("http://a", HttpMethodKind.Post, headers: [new("content-type", "x/first"), new("Content-Type", "x/second"), new("A", "1")],
            body: RequestBody.Raw(RawContentType.Json, "{}")));
        Assert.Equal("x/first", r.BodyContentType);
        Assert.Equal([new KeyValueEntry("A", "1")], r.Headers);
    }

    [Fact]
    public void User_content_type_stays_without_body()
    {
        var r = R(Req("http://a", headers: [new("Content-Type", "x/y")]));
        Assert.Equal([new KeyValueEntry("Content-Type", "x/y")], r.Headers);
        Assert.Null(r.BodyContentType);
    }

    [Fact]
    public void Unresolved_aggregated_in_order()
    {
        var r = R(Req("{{a}}{{b}}", HttpMethodKind.Post, [new("{{c}}", "{{a}}")], [new("h", "{{d}}")], RequestBody.Raw(RawContentType.Text, "{{e}}{{b}}")));
        Assert.Equal(["a", "b", "c", "d", "e"], r.Unresolved);
    }
}
