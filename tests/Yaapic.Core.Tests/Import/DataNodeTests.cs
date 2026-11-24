using Yaapic.Core.Import;
using static Yaapic.Core.Import.DataNodeJson;

namespace Yaapic.Core.Tests.Import;

public class DataNodeTests
{
    private sealed class FakeYaml : IYamlTreeReader
    {
        public string? Received { get; private set; }

        public DataNode Parse(string yaml)
        {
            Received = yaml;
            return DataNode.Null;
        }
    }

    [Fact]
    public void Parse_reads_nested_values_and_keeps_number_text()
    {
        var node = DataNodeJson.Parse("""
            {"a": [1, 1.0, 12345678901234567890, "é\nA", null, true], "b": {"c": "d"}}
            """);

        var a = node["a"]!;
        Assert.Equal("1", a[0]!.AsString());
        Assert.Equal("1.0", a[1]!.AsString());
        Assert.Equal("12345678901234567890", a[2]!.AsString());
        Assert.Equal(1.0, a[1]!.AsDouble());
        Assert.Equal("é\nA", a[3]!.AsString());
        Assert.True(a[4]!.IsNull);
        Assert.True(a[5]!.AsBool());
        Assert.Equal("true", a[5]!.AsString());
        Assert.Equal("d", node.GetString("b", "c"));
        Assert.Null(node.GetString("b", "x", "c"));
    }

    [Fact]
    public void Parse_accepts_comments_and_trailing_commas()
    {
        var node = DataNodeJson.Parse("{ // note\n \"a\": [1, 2,], /* x */ }");
        Assert.Equal(2, node["a"]!.Items.Count);
    }

    [Fact]
    public void Duplicate_keys_are_kept_and_the_last_wins()
    {
        var node = DataNodeJson.Parse("""{"k": "1", "k": "2"}""");
        Assert.Equal(2, node.Members.Count);
        Assert.Equal("2", node.GetString("k"));
    }

    [Fact]
    public void Depth_over_64_throws_format_exception()
    {
        Assert.NotNull(DataNodeJson.Parse(new string('[', 64) + new string(']', 64)));
        Assert.Throws<FormatException>(() => DataNodeJson.Parse(new string('[', 65) + new string(']', 65)));
    }

    [Fact]
    public void Invalid_json_throws_format_exception()
    {
        Assert.Throws<FormatException>(() => DataNodeJson.Parse("{\"a\": "));
    }

    [Fact]
    public void Accessors_on_wrong_kinds_return_null_or_empty()
    {
        var array = Arr(Str("x"));
        var obj = Obj(("a", Str("b")));

        Assert.Null(array.AsString());
        Assert.Null(obj[0]);
        Assert.Null(array["a"]);
        Assert.Null(array[5]);
        Assert.Empty(obj.Items);
        Assert.Empty(array.Members);
        Assert.Null(Str("maybe").AsBool());
        Assert.False(Str("FALSE").AsBool());
        Assert.Null(Str("abc").AsDouble());
        Assert.Equal(2.5, Str("2.5").AsDouble());
        Assert.Null(DataNode.Null.AsString());
    }

    [Fact]
    public void Write_indented_matches_exact_text()
    {
        var tree = Obj(
            ("name", Str("é\"")),
            ("n", DataNode.Number("1.50")),
            ("list", Arr(Num(1), DataNode.Bool(false), DataNode.Null)),
            ("empty", Obj()));

        const string expected = "{\n  \"name\": \"é\\\"\",\n  \"n\": 1.50,\n  \"list\": [\n    1,\n    false,\n    null\n  ],\n  \"empty\": {}\n}";
        Assert.Equal(expected, Write(tree));
        Assert.Equal("{\"name\":\"é\\\"\",\"n\":1.50,\"list\":[1,false,null],\"empty\":{}}", Write(tree, indented: false));
    }

    [Fact]
    public void Write_emits_invalid_number_text_as_a_string_and_round_trips()
    {
        Assert.Equal("\"0x10\"", Write(DataNode.Number("0x10")));
        var tree = Obj(("a", Arr(Num(1), Str("x"))));
        Assert.Equal(Write(tree), Write(DataNodeJson.Parse(Write(tree))));
    }

    [Fact]
    public void Structured_text_reader_picks_json_for_braces_and_brackets_and_yaml_otherwise()
    {
        var yaml = new FakeYaml();
        var reader = new StructuredTextReader(yaml);

        Assert.True(reader.Parse("  {\"a\": 1}").IsObject);
        Assert.True(reader.Parse("\n[1]").IsArray);
        Assert.Null(yaml.Received);

        reader.Parse("a: 1");
        Assert.Equal("a: 1", yaml.Received);
    }
}
