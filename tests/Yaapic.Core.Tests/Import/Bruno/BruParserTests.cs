using Yaapic.Core.Import.Bruno;

namespace Yaapic.Core.Tests.Import.Bruno;

public class BruParserTests
{
    private const string Sample = """
        meta {
          name: Create user
          type: http
          seq: 2
        }

        post {
          url: {{baseUrl}}/users?x=1
          body: json
          auth: bearer
        }

        params:query {
          page: 1
          ~limit: 10
        }

        headers {
          Content-Type: application/json
          Referer: https://a.test:8080/x
        }

        auth:bearer {
          token: {{token}}
        }

        body:json {
          {
            "name": "a",
            "n": { "b": 1 }
          }
        }

        vars:secret [
          token,
          apiKey
        ]

        script:pre-request {
          bru.setVar("a", 1);
        }
        """;

    [Fact]
    public void Parses_the_sample()
    {
        var file = BruParser.Parse(Sample);
        Assert.Equal(
            ["meta", "post", "params:query", "headers", "auth:bearer", "body:json", "vars:secret", "script:pre-request"],
            file.Blocks.Select(b => b.Name));

        var meta = file.Get("meta")!;
        Assert.Equal(BruBlockKind.Dictionary, meta.Kind);
        Assert.Equal(new BruPair("name", "Create user", true), meta.Pairs[0]);
        Assert.Equal("{{baseUrl}}/users?x=1", file.Get("post")!.Pairs[0].Value);
        Assert.Equal("{{token}}", file.Get("auth:bearer")!.Pairs[0].Value);
        Assert.Equal("bru.setVar(\"a\", 1);", file.Get("script:pre-request")!.Text);
    }

    [Fact]
    public void Disabled_entry()
    {
        var pairs = BruParser.Parse(Sample).Get("params:query")!.Pairs;
        Assert.Equal(new BruPair("page", "1", true), pairs[0]);
        Assert.Equal(new BruPair("limit", "10", false), pairs[1]);
    }

    [Fact]
    public void Header_value_keeps_colons() =>
        Assert.Equal("https://a.test:8080/x", BruParser.Parse(Sample).Get("headers")!.Pairs[1].Value);

    [Fact]
    public void Json_body_drops_two_spaces_and_keeps_deeper_indent()
    {
        var body = BruParser.Parse(Sample).Get("body:json")!;
        Assert.Equal(BruBlockKind.Text, body.Kind);
        Assert.Equal("{\n  \"name\": \"a\",\n  \"n\": { \"b\": 1 }\n}", body.Text);
    }

    [Fact]
    public void Secret_list()
    {
        var list = BruParser.Parse(Sample).Get("vars:secret")!;
        Assert.Equal(BruBlockKind.List, list.Kind);
        Assert.Equal(["token", "apiKey"], list.Items);
    }

    [Fact]
    public void Multi_line_value()
    {
        var file = BruParser.Parse("headers {\n  A: '''\n    one\n    two\n  '''\n  B: 2\n}\n");
        var pairs = file.Get("headers")!.Pairs;
        Assert.Equal("  one\n  two", pairs[0].Value);
        Assert.Equal("2", pairs[1].Value);
    }

    [Fact]
    public void Crlf_input()
    {
        var file = BruParser.Parse("meta {\r\n  name: A\r\n}\r\nbody:text {\r\n  x\r\n  y\r\n}\r\n");
        Assert.Equal("A", file.Get("meta")!.Pairs[0].Value);
        Assert.Equal("x\ny", file.Get("body:text")!.Text);
    }

    [Fact]
    public void Unterminated_block_ends_at_end_of_file()
    {
        var file = BruParser.Parse("meta {\n  name: A\n  seq: 1");
        Assert.Equal(2, file.Get("meta")!.Pairs.Count);
    }

    [Fact]
    public void Indented_closing_brace_does_not_terminate_a_text_block()
    {
        var file = BruParser.Parse("body:text {\n  a\n  }\n  b\n}\nmeta {\n  name: A\n}\n");
        Assert.Equal("a\n}\nb", file.Get("body:text")!.Text);
        Assert.NotNull(file.Get("meta"));
    }

    [Fact]
    public void Text_before_the_first_block_is_ignored()
    {
        var file = BruParser.Parse("junk\nmore: stuff\nmeta {\n  name: A\n}\n");
        Assert.Single(file.Blocks);
    }

    [Fact]
    public void Get_and_get_all()
    {
        var file = BruParser.Parse("auth {\n  mode: x\n}\nauth:basic {\n  username: u\n}\nauth:bearer {\n  token: t\n}\n");
        Assert.Equal("auth", file.Get("auth")!.Name);
        Assert.Null(file.Get("missing"));
        Assert.Equal(["auth:basic", "auth:bearer"], file.GetAll("auth:").Select(b => b.Name));
    }

    [Fact]
    public void Empty_file() => Assert.Empty(BruParser.Parse(string.Empty).Blocks);
}
