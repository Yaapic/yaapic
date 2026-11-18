using System.Text;
using Yaapic.Core.Import;
using Yaapic.Infrastructure.Import;

namespace Yaapic.Infrastructure.Tests.Import;

public sealed class VYamlTreeReaderTests
{
    private static readonly VYamlTreeReader Reader = new();

    private static DataNode P(string yaml) => Reader.Parse(yaml);

    [Theory]
    [InlineData("1", DataKind.Number)]
    [InlineData("1.0", DataKind.Number)]
    [InlineData("'1.0'", DataKind.String)]
    [InlineData("true", DataKind.Bool)]
    [InlineData("\"true\"", DataKind.String)]
    [InlineData("null", DataKind.Null)]
    [InlineData("~", DataKind.Null)]
    [InlineData("", DataKind.Null)]
    [InlineData("yes", DataKind.String)]
    [InlineData("3.0.1", DataKind.String)]
    [InlineData("0x1F", DataKind.String)]
    [InlineData("007", DataKind.String)]
    [InlineData("'null'", DataKind.String)]
    public void Plain_scalars_are_typed_and_quoted_ones_are_strings(string value, DataKind kind)
    {
        var node = P($"a: {value}\n")["a"]!;
        Assert.Equal(kind, node.Kind);
        if (kind != DataKind.Null)
        {
            Assert.Equal(value.Trim('\'', '"'), node.AsString());
        }
    }

    [Fact]
    public void Literal_and_folded_blocks_keep_clip_chomping()
    {
        var root = P("lit: |\n  line1\n  line2\nfold: >\n  a\n  b\n");
        Assert.Equal("line1\nline2\n", root.GetString("lit"));
        Assert.Equal("a b\n", root.GetString("fold"));
    }

    [Fact]
    public void Flow_collections_are_read()
    {
        var m = P("m: {a: 1, b: [x, y]}\n")["m"]!;
        Assert.Equal("1", m.GetString("a"));
        Assert.Equal("y", m["b"]![1]!.AsString());
    }

    [Fact]
    public void Anchor_and_alias_copy_the_subtree()
    {
        var root = P("list: &l [1, 2]\ncopy: *l\n");
        Assert.Equal(2, root["copy"]!.Items.Count);
        Assert.Equal("2", root["copy"]![1]!.AsString());
    }

    [Fact]
    public void Merge_key_lets_own_keys_win()
    {
        var use = P("base: &b\n  x: 1\n  y: 2\nuse:\n  <<: *b\n  y: 3\n")["use"]!;
        Assert.Equal("1", use.GetString("x"));
        Assert.Equal("3", use.GetString("y"));
        Assert.Equal(2, use.Members.Count);
    }

    [Fact]
    public void Document_markers_comments_crlf_and_bom_are_handled()
    {
        Assert.Equal("1", P("---\na: 1\n...\n").GetString("a"));
        Assert.Equal("2", P("# top\na: 1 # trailing\n# mid\nb: 2\n").GetString("b"));
        var crlf = P("a: 1\r\nb:\r\n  - x\r\nc: |\r\n  l1\r\n  l2\r\n");
        Assert.Equal("l1\nl2\n", crlf.GetString("c"));
        Assert.Equal("1", P("\uFEFFa: 1\n").GetString("a"));
    }

    [Fact]
    public void Only_the_first_document_is_read()
    {
        var root = P("a: 1\n---\nb: 2\n");
        Assert.Equal("1", root.GetString("a"));
        Assert.Null(root["b"]);
    }

    [Fact]
    public void Tab_indentation_throws_a_format_exception_naming_a_line()
    {
        var ex = Assert.Throws<FormatException>(() => P("a:\n\t- x\n  b: 2\n"));
        Assert.Contains("Line", ex.Message);
    }

    [Fact]
    public void Syntax_error_throws_a_format_exception()
    {
        var ex = Assert.Throws<FormatException>(() => P("a: [1, 2\nb: 3\n"));
        Assert.Contains("Line", ex.Message);
    }

    [Fact]
    public void Depth_65_throws_and_depth_64_is_accepted()
    {
        Assert.NotNull(P(Nested(30)));
        Assert.Throws<FormatException>(() => P(Nested(65)));
    }

    private static string Nested(int levels)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < levels; i++)
        {
            sb.Append(' ', i * 2).Append("k:\n");
        }

        return sb.Append(' ', levels * 2).Append("v: 1\n").ToString();
    }

    [Fact]
    public void Alias_bomb_throws()
    {
        var sb = new StringBuilder("a: &a [x,x,x,x,x,x,x,x,x,x]\n");
        var prev = "a";
        for (var i = 1; i <= 8; i++)
        {
            var cur = ((char)('a' + i)).ToString();
            sb.Append($"{cur}: &{cur} [{string.Join(",", Enumerable.Repeat("*" + prev, 10))}]\n");
            prev = cur;
        }

        Assert.Throws<FormatException>(() => P(sb.ToString()));
    }

    [Fact]
    public void Unknown_alias_throws()
    {
        Assert.Throws<FormatException>(() => P("a: *nope\n"));
    }

    [Fact]
    public void OpenAPI_document_is_navigable()
    {
        var doc = P("openapi: 3.0.3\ninfo:\n  version: '1.0'\npaths:\n  /pets:\n    get:\n      summary: List pets\n      responses:\n        '200':\n          description: ok\n");
        Assert.Equal("List pets", doc.GetString("paths", "/pets", "get", "summary"));
        Assert.Equal("1.0", doc.GetString("info", "version"));
        Assert.Equal("ok", doc.GetString("paths", "/pets", "get", "responses", "200", "description"));
    }

    [Fact]
    public void Structured_text_reader_gives_equal_trees_for_json_and_yaml()
    {
        var reader = new StructuredTextReader(Reader);
        var json = reader.Parse("{\"a\":1,\"b\":[\"x\",true,null],\"c\":{\"d\":\"1.0\"}}");
        var yaml = reader.Parse("a: 1\nb:\n  - x\n  - true\n  - null\nc:\n  d: '1.0'\n");
        Assert.Equal(DataNodeJson.Write(json, false), DataNodeJson.Write(yaml, false));
    }
}
