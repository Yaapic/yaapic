using Yaapic.Core.Import;
using static Yaapic.Core.Import.DataNodeJson;

namespace Yaapic.Core.Tests.Import;

public sealed class DataNodeYamlTests
{
    [Fact]
    public void A_nested_tree_is_written_in_block_style()
    {
        var tree = Obj(
            ("openapi", Str("3.0.3")),
            ("paths", Obj(("/pets/{id}", Obj(("get", Obj(("responses", Obj(("200", Obj(("description", Str("ok")))))))))))),
            ("tags", Arr(Obj(("name", Str("a")), ("meta", Obj(("x-token", Num(1))))), Obj(("name", Str("b"))))),
            ("matrix", Arr(Arr(Num(1), Num(2)), Arr())),
            ("empty", Obj()),
            ("none", Arr()));

        const string expected = """
            openapi: "3.0.3"
            paths:
              "/pets/{id}":
                get:
                  responses:
                    "200":
                      description: "ok"
            tags:
              - name: "a"
                meta:
                  x-token: 1
              - name: "b"
            matrix:
              -
                - 1
                - 2
              - []
            empty: {}
            none: []

            """;
        Assert.Equal(expected.ReplaceLineEndings("\n"), DataNodeYaml.Write(tree));
    }

    [Fact]
    public void Strings_are_always_quoted_and_escaped()
    {
        var tree = Obj(
            ("q", Str("say \"hi\"")),
            ("b", Str("a\\b")),
            ("n", Str("l1\nl2\t\r")),
            ("u", Str("é日\u0001")),
            ("num", Str("42")),
            ("bool", Str("true")));

        const string expected = "q: \"say \\\"hi\\\"\"\nb: \"a\\\\b\"\nn: \"l1\\nl2\\t\\r\"\nu: \"é日\\u0001\"\nnum: \"42\"\nbool: \"true\"\n";
        Assert.Equal(expected, DataNodeYaml.Write(tree));
    }

    [Fact]
    public void Scalars_and_roots_end_with_a_single_newline()
    {
        Assert.Equal("null\n", DataNodeYaml.Write(DataNode.Null));
        Assert.Equal("true\n", DataNodeYaml.Write(DataNode.Bool(true)));
        Assert.Equal("1.50\n", DataNodeYaml.Write(DataNode.Number("1.50")));
        Assert.Equal("-3\n", DataNodeYaml.Write(DataNode.Number("-3")));
        Assert.Equal("\"x\"\n", DataNodeYaml.Write(Str("x")));
        Assert.Equal("{}\n", DataNodeYaml.Write(Obj()));
        Assert.Equal("- 1\n- null\n- false\n", DataNodeYaml.Write(Arr(Num(1), DataNode.Null, DataNode.Bool(false))));
    }

    [Fact]
    public void Keys_are_bare_only_when_simple()
    {
        var tree = Obj(("x-token", Num(1)), ("_a1", Num(2)), ("200", Num(3)), ("a b", Num(4)), ("", Num(5)), ("é", Num(6)));
        Assert.Equal("x-token: 1\n_a1: 2\n\"200\": 3\n\"a b\": 4\n\"\": 5\n\"é\": 6\n", DataNodeYaml.Write(tree));
    }
}
