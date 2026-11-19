using System.Text;
using Yaapic.Core.Import;
using Yaapic.Core.Import.OpenApi;

namespace Yaapic.Core.Tests.Import.OpenApi;

public class OpenApiSchemaSamplerTests
{
    private readonly ImportReport _report = new();

    private string Sample(string schema, string components = "{}")
    {
        var root = DataNodeJson.Parse($$"""{"components": {"schemas": {{components}}}, "s": {{schema}}}""");
        var sampler = new OpenApiSchemaSampler(new OpenApiRefResolver(root, _report), _report);
        return DataNodeJson.Write(sampler.Sample(root["s"]!, "s"), false);
    }

    [Theory]
    [InlineData("""{"type":"string","example":"e","default":"d","enum":["x"],"const":"c"}""", "\"e\"")]
    [InlineData("""{"type":"string","default":"d","enum":["x"],"const":"c"}""", "\"d\"")]
    [InlineData("""{"type":"string","enum":["x","y"],"const":"c"}""", "\"x\"")]
    [InlineData("""{"type":"string","const":"c"}""", "\"c\"")]
    public void Example_default_enum_const_win_in_that_order(string schema, string expected) =>
        Assert.Equal(expected, Sample(schema));

    [Fact]
    public void Objects_nest_arrays_and_skip_read_only_properties()
    {
        const string Schema = """
            {"type":"object","properties":{
              "id":{"type":"integer","readOnly":true},
              "name":{"type":"string"},
              "tags":{"type":"array","items":{"type":"string"}},
              "empty":{"type":"array"},
              "child":{"properties":{"ok":{"type":"boolean"}}},
              "list":{"items":{"type":"number"}}}}
            """;
        Assert.Equal(
            """{"name":"string","tags":["string"],"empty":[],"child":{"ok":false},"list":[0]}""",
            Sample(Schema));
        Assert.Equal("{}", Sample("""{"type":"object"}"""));
    }

    [Theory]
    [InlineData("date-time", "2024-01-01T00:00:00Z")]
    [InlineData("date", "2024-01-01")]
    [InlineData("time", "00:00:00")]
    [InlineData("uuid", "00000000-0000-0000-0000-000000000000")]
    [InlineData("email", "user@example.com")]
    [InlineData("uri", "https://example.com")]
    [InlineData("url", "https://example.com")]
    [InlineData("hostname", "example.com")]
    [InlineData("ipv4", "192.0.2.1")]
    [InlineData("ipv6", "2001:db8::1")]
    [InlineData("byte", "")]
    [InlineData("binary", "")]
    [InlineData("password", "password")]
    [InlineData("other", "string")]
    public void String_formats_have_a_fixed_sample(string format, string expected) =>
        Assert.Equal($"\"{expected}\"", Sample($$"""{"type":"string","format":"{{format}}"}"""));

    [Fact]
    public void Min_length_pads_the_default_text_up_to_256()
    {
        Assert.Equal("\"stringxxxx\"", Sample("""{"type":"string","minLength":10}"""));
        Assert.Equal("\"string\"", Sample("""{"type":"string","minLength":6}"""));
        Assert.Equal($"\"{"string".PadRight(256, 'x')}\"", Sample("""{"type":"string","minLength":1000}"""));
    }

    [Fact]
    public void Numbers_use_the_minimum_or_zero()
    {
        Assert.Equal("5", Sample("""{"type":"integer","minimum":5}"""));
        Assert.Equal("0", Sample("""{"type":"integer"}"""));
        Assert.Equal("1.5", Sample("""{"type":"number","minimum":1.5}"""));
        Assert.Equal("0", Sample("""{"type":"number"}"""));
        Assert.Equal("false", Sample("""{"type":"boolean"}"""));
        Assert.Equal("null", Sample("""{"type":"null"}"""));
    }

    [Fact]
    public void AllOf_merges_object_members_and_a_later_key_replaces_an_earlier_one() =>
        Assert.Equal(
            """{"a":"string","b":0,"c":false}""",
            Sample("""
                {"allOf":[
                  {"type":"object","properties":{"a":{"type":"integer"},"b":{"type":"integer"}}},
                  {"type":"string"},
                  {"type":"object","properties":{"a":{"type":"string"},"c":{"type":"boolean"}}}]}
                """));

    [Fact]
    public void OneOf_and_anyOf_use_the_first_member()
    {
        Assert.Equal("\"string\"", Sample("""{"oneOf":[{"type":"string"},{"type":"integer"}]}"""));
        Assert.Equal("0", Sample("""{"anyOf":[{"type":"integer"},{"type":"string"}]}"""));
    }

    [Fact]
    public void A_3_1_type_list_uses_the_first_non_null_type()
    {
        Assert.Equal("\"string\"", Sample("""{"type":["null","string"]}"""));
        Assert.Equal("\"string\"", Sample("""{"type":["string","null"]}"""));
    }

    [Fact]
    public void A_ref_schema_is_followed() =>
        Assert.Equal(
            """{"pet":{"name":"string"}}""",
            Sample(
                """{"type":"object","properties":{"pet":{"$ref":"#/components/schemas/Pet"}}}""",
                """{"Pet":{"type":"object","properties":{"name":{"type":"string"}}}}"""));

    [Fact]
    public void A_self_referencing_schema_stops_with_null_and_reports_once()
    {
        const string Node = """{"type":"object","properties":{"left":{"$ref":"#/components/schemas/Node"},"right":{"$ref":"#/components/schemas/Node"}}}""";
        Assert.Equal(
            """{"left":null,"right":null}""",
            Sample("""{"$ref":"#/components/schemas/Node"}""", $$"""{"Node":{{Node}}}"""));
        Assert.Single(_report.Entries);
        Assert.Equal(ImportSeverity.Info, _report.Entries[0].Severity);
    }

    [Fact]
    public void Nesting_deeper_than_six_levels_is_cut()
    {
        var schema = """{"type":"string"}""";
        for (var i = 0; i < 7; i++)
        {
            schema = $$"""{"type":"array","items":{{schema}}}""";
        }

        // 7 arrays: the root plus 6 levels below it hold arrays, the leaf at depth 7 is cut.
        Assert.Equal("[[[[[[[null]]]]]]]", Sample(schema));
        Assert.Single(_report.Entries);
    }

    [Fact]
    public void Six_levels_are_kept()
    {
        var schema = """{"type":"string"}""";
        for (var i = 0; i < 6; i++)
        {
            schema = $$"""{"type":"array","items":{{schema}}}""";
        }

        Assert.Equal("""[[[[[["string"]]]]]]""", Sample(schema));
        Assert.Empty(_report.Entries);
    }

    [Fact]
    public void A_very_wide_schema_is_capped_at_2000_nodes()
    {
        StringBuilder sb = new("""{"type":"object","properties":{""");
        for (var i = 0; i < 2100; i++)
        {
            sb.Append(i > 0 ? "," : "").Append($$"""
                "p{{i}}":{"type":"integer"}
                """);
        }

        sb.Append("}}");
        var json = Sample(sb.ToString());
        Assert.Equal(1999, json.Split(":0").Length - 1);
        Assert.Equal(101, json.Split(":null").Length - 1);
        Assert.Single(_report.Entries);
    }

    [Fact]
    public void A_missing_or_unknown_schema_gives_null()
    {
        var root = DataNodeJson.Parse("{}");
        var sampler = new OpenApiSchemaSampler(new OpenApiRefResolver(root, _report), _report);
        Assert.True(sampler.Sample(DataNode.Null, "s").IsNull);
        Assert.Equal("null", Sample("{}"));
        Assert.Empty(_report.Entries);
    }
}
