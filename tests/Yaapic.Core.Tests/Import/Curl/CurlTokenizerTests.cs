using Yaapic.Core.CodeGeneration;
using Yaapic.Core.Import.Curl;
using Yaapic.Core.Tests.CodeGeneration;

namespace Yaapic.Core.Tests.Import.Curl;

public class CurlTokenizerTests
{
    private static string[] One(string text)
    {
        var all = CurlTokenizer.Tokenize(text);
        Assert.Single(all);
        return [.. all[0]];
    }

    [Fact]
    public void Simple_command() =>
        Assert.Equal(["curl", "-X", "POST", "https://a.test"], One("curl -X POST https://a.test"));

    [Fact]
    public void Quotes_with_an_embedded_other_quote()
    {
        Assert.Equal(["curl", "a \"b\"", "it's"], One("curl 'a \"b\"' \"it's\""));
        Assert.Equal(["curl", "{\"a\":\"x\"}"], One("curl \"{\\\"a\\\":\\\"x\\\"}\""));
    }

    [Fact]
    public void Double_quote_escapes_are_limited() =>
        Assert.Equal(["curl", "a\\nb\"$x`y\\z$HOME"], One("curl \"a\\nb\\\"\\$x\\`y\\\\z$HOME\""));

    [Fact]
    public void Line_continuations_join_lines() =>
        Assert.Equal(["curl", "-H", "A: b", "-d", "x", "u"], One("curl \\\n  -H 'A: b' \\\r\n  -d \"x\" ^\n  u"));

    [Fact]
    public void Ansi_c_quotes()
    {
        Assert.Equal(["curl", "a\nb'c\"d\\e\te"], One("curl $'a\\nb\\'c\\\"d\\\\e\\te'"));
        Assert.Equal(["curl", "é A Aé\u0001 \u001b\0z"], One("curl $'é \\x41 \\101\\u00e9\\1 \\e\\0z'"));
        Assert.Equal(["curl", "\\q\\x\U0001F600"], One("curl $'\\q\\x\\U0001F600'"));
    }

    [Fact]
    public void Adjacent_pieces_concatenate()
    {
        Assert.Equal(["curl", "-Ha: b"], One("curl -H'a: b'"));
        Assert.Equal(["curl", "ab"], One("curl \"a\"'b'"));
        Assert.Equal(["curl", "a b", ""], One("curl a\\ b ''"));
    }

    [Fact]
    public void Comments_and_prompts()
    {
        var all = CurlTokenizer.Tokenize("# header\n$ curl a # inline\n> curl b#c\n");
        Assert.Equal(2, all.Count);
        Assert.Equal(["curl", "a"], all[0]);
        Assert.Equal(["curl", "b#c"], all[1]);
    }

    [Fact]
    public void Commands_split_on_newline_and_on_separator_before_curl()
    {
        var all = CurlTokenizer.Tokenize("curl a\n\ncurl b; curl c && curl.exe d;echo x\nls\n");
        Assert.Equal(4, all.Count);
        Assert.Equal(["curl", "a"], all[0]);
        Assert.Equal(["curl", "b"], all[1]);
        Assert.Equal(["curl", "c"], all[2]);
        Assert.Equal(["curl.exe", "d;echo", "x"], all[3]);
    }

    [Fact]
    public void Separator_inside_a_word_stays() =>
        Assert.Equal(["curl", "a;b", "c&&d"], One("curl a;b c&&d"));

    [Fact]
    public void Text_without_curl_is_empty()
    {
        Assert.Empty(CurlTokenizer.Tokenize("echo hi\nls -l"));
        Assert.Empty(CurlTokenizer.Tokenize(""));
    }

    [Theory]
    [InlineData("curl 'abc")]
    [InlineData("curl \"abc")]
    [InlineData("curl $'abc")]
    public void Unterminated_quote_throws(string text) => Assert.Throws<FormatException>(() => CurlTokenizer.Tokenize(text));

    [Fact]
    public void Windows_cmd_form_throws() =>
        Assert.Throws<FormatException>(() => CurlTokenizer.Tokenize("curl ^\"https://a.test^\" ^\n -H ^\"A: b^\""));

    [Fact]
    public void Generated_get_reads_back()
    {
        var words = One(new CurlCodeGenerator().Generate(CodeSamples.GetSimple));
        Assert.Equal(["curl", "https://api.example.com/users?id=1&name=a%20b", "--header", "Accept: application/json"], words);
    }

    [Fact]
    public void Generated_post_json_reads_back()
    {
        var words = One(new CurlCodeGenerator().Generate(CodeSamples.PostJson));
        Assert.Equal(
            ["curl", "--request", "POST", "https://api.example.com/users", "--header", "Accept: application/json",
             "--header", "Content-Type: application/json", "--data-raw", CodeSamples.JsonBody],
            words);
    }

    [Fact]
    public void Generated_awkward_headers_read_back()
    {
        var words = One(new CurlCodeGenerator().Generate(CodeSamples.HeadersAwkward));
        Assert.Equal(["curl", "https://api.example.com/h", "--header", "X-Quote: say \"hi\" it's"], words);
    }
}
