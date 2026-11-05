using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class QueryStringSyncTests
{
    [Fact]
    public void Parse_without_query()
    {
        var p = QueryStringSync.Parse("http://a/b");
        Assert.Equal("http://a/b", p.Prefix);
        Assert.Empty(p.Params);
        Assert.Equal("", p.Fragment);
    }

    [Fact]
    public void Parse_query_and_fragment()
    {
        var p = QueryStringSync.Parse("{{baseUrl}}/a?b=1&c=%20d#top");
        Assert.Equal("{{baseUrl}}/a", p.Prefix);
        Assert.Equal([new KeyValueEntry("b", "1"), new KeyValueEntry("c", " d")], p.Params);
        Assert.Equal("#top", p.Fragment);
    }

    [Fact]
    public void Parse_question_mark_inside_fragment_belongs_to_fragment()
    {
        var p = QueryStringSync.Parse("http://a#frag?x=1");
        Assert.Equal("http://a", p.Prefix);
        Assert.Empty(p.Params);
        Assert.Equal("#frag?x=1", p.Fragment);
    }

    [Fact]
    public void Parse_skips_empty_segments_and_handles_key_without_equals()
    {
        var p = QueryStringSync.Parse("http://a?&k&&v=&=x&");
        Assert.Equal([new KeyValueEntry("k", ""), new KeyValueEntry("v", ""), new KeyValueEntry("", "x")], p.Params);
    }

    [Fact]
    public void Parse_decodes_percent_and_keeps_plus_and_invalid_sequences()
    {
        var p = QueryStringSync.Parse("http://a?a+b=c%26d&bad=%zz&u=%C3%A9");
        Assert.Equal(
            [new KeyValueEntry("a+b", "c&d"), new KeyValueEntry("bad", "%zz"), new KeyValueEntry("u", "é")],
            p.Params);
    }

    [Fact]
    public void Compose_with_zero_one_and_many_params()
    {
        Assert.Equal("http://a", QueryStringSync.Compose("http://a", [], ""));
        Assert.Equal("http://a?k=v", QueryStringSync.Compose("http://a", [new("k", "v")], ""));
        Assert.Equal("http://a?k=v&e=#f", QueryStringSync.Compose("http://a", [new("k", "v"), new("e", "")], "#f"));
    }

    [Fact]
    public void Compose_skips_empty_keys() =>
        Assert.Equal("http://a?k=v", QueryStringSync.Compose("http://a", [new("", "x"), new("k", "v")], ""));

    [Fact]
    public void Compose_escapes_special_characters() =>
        Assert.Equal(
            "p?a%20b=c%26d%3De&%C3%A9=%C3%BC",
            QueryStringSync.Compose("p", [new("a b", "c&d=e"), new("é", "ü")], ""));

    [Fact]
    public void Compose_keeps_variable_tokens_unescaped() =>
        Assert.Equal(
            "p?{{k}}%20x=a%20{{v}}%26%7B%7B%20",
            QueryStringSync.Compose("p", [new("{{k}} x", "a {{v}}&{{ ")], ""));

    [Theory]
    [InlineData("{{baseUrl}}/a?b=1&c=%20d#top")]
    [InlineData("http://a/b")]
    [InlineData("http://a?k={{v}}&e=#f")]
    public void Compose_of_parse_round_trips_canonical_urls(string url) =>
        Assert.Equal(url, QueryStringSync.Compose(QueryStringSync.Parse(url)));
}
