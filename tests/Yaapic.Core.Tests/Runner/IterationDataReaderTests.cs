using Yaapic.Core.Runner;

namespace Yaapic.Core.Tests.Runner;

public class IterationDataReaderTests
{
    private static string[] Flat(IterationDataRow row) => [.. row.Values.Select(v => $"{v.Key}={v.Value}")];

    [Fact]
    public void Json_rows_cover_every_value_kind_and_keep_key_order()
    {
        var rows = JsonIterationData.Parse("""[{"z":"s","a":1.50,"b":true,"n":null,"o":{"x":1},"l":[1,"2"]}]""");
        Assert.Equal(["z=s", "a=1.50", "b=true", "n=", "o={\"x\":1}", "l=[1,\"2\"]"], Flat(rows[0]));
    }

    [Fact]
    public void Json_root_must_be_an_array()
        => Assert.Throws<IterationDataException>(() => JsonIterationData.Parse("""{"a":1}"""));

    [Fact]
    public void Json_element_must_be_an_object()
        => Assert.Throws<IterationDataException>(() => JsonIterationData.Parse("""[{"a":1}, 2]"""));

    [Fact]
    public void Json_invalid_text_is_wrapped()
        => Assert.Throws<IterationDataException>(() => JsonIterationData.Parse("[{"));

    [Fact]
    public void Reader_picks_the_format_by_extension()
    {
        Assert.Equal(["a"], IterationDataReader.Read("d.CSV", "a\n1").Columns);
        Assert.Equal(["a"], IterationDataReader.Read(Path.Combine("x", "d.json"), """[{"a":1}]""").Columns);
        Assert.Equal("d.json", IterationDataReader.Read(Path.Combine("x", "d.json"), "[]").FileName);
    }

    [Fact]
    public void Reader_picks_the_format_by_content()
    {
        Assert.Single(IterationDataReader.Read("data.txt", "  \n[{\"a\":1}]").Rows);
        Assert.Single(IterationDataReader.Read("data.txt", "a,b\n1,2").Rows);
    }

    [Fact]
    public void Reader_removes_a_leading_bom()
        => Assert.Equal(["a"], IterationDataReader.Read("d.csv", "\uFEFFa\n1").Columns);

    [Fact]
    public void Reader_refuses_a_text_over_the_limit()
        => Assert.Throws<IterationDataException>(() =>
            IterationDataReader.Read("d.csv", new string('a', IterationDataLimits.MaxBytes + 1)));

    [Fact]
    public void Columns_are_the_union_in_first_seen_order()
    {
        var data = IterationDataReader.Read("d.json", """[{"a":1,"b":2},{"c":3,"a":4}]""");
        Assert.Equal(["a", "b", "c"], data.Columns);
    }
}
