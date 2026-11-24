using Yaapic.Core.Runner;

namespace Yaapic.Core.Tests.Runner;

public class CsvIterationDataTests
{
    private static string[] Flat(IterationDataRow row) => [.. row.Values.Select(v => $"{v.Key}={v.Value}")];

    [Fact]
    public void Parses_a_simple_file()
    {
        var rows = CsvIterationData.Parse("a,b\n1,2\n3,4");
        Assert.Equal(2, rows.Count);
        Assert.Equal(["a=1", "b=2"], Flat(rows[0]));
        Assert.Equal(["a=3", "b=4"], Flat(rows[1]));
        Assert.True(rows[1].TryGet("b", out string v));
        Assert.Equal("4", v);
        Assert.False(rows[1].TryGet("c", out _));
    }

    [Fact]
    public void Handles_quoted_commas_quotes_and_line_breaks()
    {
        var rows = CsvIterationData.Parse("a,b\n\"x,y\",\"say \"\"hi\"\"\"\n\"l1\r\nl2\",z");
        Assert.Equal(["a=x,y", "b=say \"hi\""], Flat(rows[0]));
        Assert.Equal(["a=l1\r\nl2", "b=z"], Flat(rows[1]));
    }

    [Theory]
    [InlineData("a\r\n1\r\n2")]
    [InlineData("a\n1\n2")]
    [InlineData("a\r1\r2")]
    public void Accepts_every_line_ending(string text) => Assert.Equal(2, CsvIterationData.Parse(text).Count);

    [Fact]
    public void Ignores_a_trailing_newline_and_blank_middle_lines()
    {
        var rows = CsvIterationData.Parse("a\n1\n\n\n2\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal(["a=2"], Flat(rows[1]));
    }

    [Fact]
    public void Pads_short_records_and_refuses_long_ones_with_the_line()
    {
        Assert.Equal(["a=1", "b="], Flat(CsvIterationData.Parse("a,b\n1")[0]));
        var ex = Assert.Throws<IterationDataException>(() => CsvIterationData.Parse("a,b\n1,2\n3,4,5"));
        Assert.Contains("3", ex.Message);
    }

    [Fact]
    public void Reports_the_line_of_an_unclosed_quote()
    {
        var ex = Assert.Throws<IterationDataException>(() => CsvIterationData.Parse("a\n1\n\"oops\n3"));
        Assert.Contains("3", ex.Message);
    }

    [Fact]
    public void Names_empty_and_duplicate_headers()
    {
        var rows = CsvIterationData.Parse(" a , ,a,a\n1,2,3,4");
        Assert.Equal(["a=1", "column2=2", "a_3=3", "a_4=4"], Flat(rows[0]));
    }

    [Fact]
    public void Header_only_or_empty_text_gives_no_rows()
    {
        Assert.Empty(CsvIterationData.Parse("a,b\n"));
        Assert.Empty(CsvIterationData.Parse(""));
    }

    [Fact]
    public void Refuses_too_many_columns_and_rows()
    {
        string wide = string.Join(',', Enumerable.Range(0, IterationDataLimits.MaxColumns + 1));
        Assert.Throws<IterationDataException>(() => CsvIterationData.Parse(wide));
        Assert.Single(CsvIterationData.Parse(string.Join(',', Enumerable.Range(0, IterationDataLimits.MaxColumns)) + "\n"
            + string.Join(',', Enumerable.Range(0, IterationDataLimits.MaxColumns))));

        string tall = "a\n" + string.Concat(Enumerable.Repeat("x\n", IterationDataLimits.MaxRows + 1));
        Assert.Throws<IterationDataException>(() => CsvIterationData.Parse(tall));
        Assert.Equal(IterationDataLimits.MaxRows,
            CsvIterationData.Parse("a\n" + string.Concat(Enumerable.Repeat("x\n", IterationDataLimits.MaxRows))).Count);
    }

    [Fact]
    public void Keeps_unicode_and_does_not_trim_values()
    {
        Assert.Equal(["nom=  é日本😀 "], Flat(CsvIterationData.Parse("nom\n  é日本😀 ")[0]));
    }
}
