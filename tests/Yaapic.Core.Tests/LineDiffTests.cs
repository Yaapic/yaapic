using Yaapic.Core.Diff;

namespace Yaapic.Core.Tests;

public class LineDiffTests
{
    private static string[] L(string s) => [.. s.Select(c => c.ToString())];

    private static string Render(IReadOnlyList<DiffLine> d) =>
        string.Concat(d.Select(x => (x.Op switch { DiffOp.Equal => "", DiffOp.Added => "+", _ => "-" }) + x.Text));

    [Fact]
    public void Identical_inputs_are_all_equal()
    {
        var d = LineDiff.Compute(L("abc"), L("abc"));
        Assert.Equal(3, d.Count);
        Assert.All(d, x => Assert.Equal(DiffOp.Equal, x.Op));
        Assert.False(LineDiff.Summarize(d).HasChanges);
    }

    [Fact]
    public void Both_empty_gives_empty() => Assert.Empty(LineDiff.Compute([], []));

    [Fact]
    public void Empty_to_non_empty_adds_everything()
    {
        var d = LineDiff.Compute([], L("ab"));
        Assert.Equal("+a+b", Render(d));
        Assert.Equal([1, 2], d.Select(x => x.NewLine!.Value));
        Assert.All(d, x => Assert.Null(x.OldLine));
    }

    [Fact]
    public void Non_empty_to_empty_removes_everything()
    {
        var d = LineDiff.Compute(L("ab"), []);
        Assert.Equal("-a-b", Render(d));
        Assert.All(d, x => Assert.Null(x.NewLine));
    }

    [Fact]
    public void One_changed_line_is_removed_then_added() =>
        Assert.Equal("a-b+Xc", Render(LineDiff.Compute(L("abc"), L("aXc"))));

    [Theory]
    [InlineData("bc", "abc", "+abc")]
    [InlineData("ac", "abc", "a+bc")]
    [InlineData("ab", "abc", "ab+c")]
    [InlineData("abc", "bc", "-abc")]
    [InlineData("abc", "ac", "a-bc")]
    [InlineData("abc", "ab", "ab-c")]
    public void Insertions_and_deletions(string o, string n, string expected) =>
        Assert.Equal(expected, Render(LineDiff.Compute(L(o), L(n))));

    [Fact]
    public void Textbook_example_has_an_edit_script_of_length_five()
    {
        var d = LineDiff.Compute(L("ABCABBA"), L("CBABAC"));
        var s = LineDiff.Summarize(d);
        Assert.Equal(5, s.Added + s.Removed);
        Assert.Equal(L("CBABAC"), Replay(d, L("ABCABBA")));
    }

    [Fact]
    public void Reordered_blocks_round_trip()
    {
        string[] o = ["a", "b", "c", "x", "y"];
        string[] n = ["x", "y", "a", "b", "c"];
        Assert.Equal(n, Replay(LineDiff.Compute(o, n), o));
    }

    [Fact]
    public void Duplicated_lines_round_trip()
    {
        var d = LineDiff.Compute(L("aaab"), L("abaa"));
        Assert.Equal(L("abaa"), Replay(d, L("aaab")));
    }

    [Fact]
    public void Whitespace_is_significant_by_default() =>
        Assert.True(LineDiff.Summarize(LineDiff.Compute(["a "], [" a"])).HasChanges);

    [Fact]
    public void Ignore_whitespace_trims_for_comparison_and_keeps_new_text()
    {
        var d = LineDiff.Compute(["a ", "b"], [" a", "c"], ignoreWhitespace: true);
        Assert.Equal(DiffOp.Equal, d[0].Op);
        Assert.Equal(" a", d[0].Text);
        Assert.Equal(["b"], d.Where(x => x.Op == DiffOp.Removed).Select(x => x.Text));
    }

    [Fact]
    public void SplitLines_handles_all_terminators()
    {
        Assert.Equal(["a", "b", "c", "d"], LineDiff.SplitLines("a\r\nb\nc\rd"));
        Assert.Equal(LineDiff.SplitLines("a\nb"), LineDiff.SplitLines("a\r\nb"));
        Assert.Equal(["a"], LineDiff.SplitLines("a\n"));
        Assert.Equal(["a", string.Empty], LineDiff.SplitLines("a\n\n"));
        Assert.Empty(LineDiff.SplitLines(string.Empty));
    }

    [Fact]
    public void Random_inputs_replay_and_number_consistently()
    {
        var rng = new Random(1234);
        string[] Gen() => [.. Enumerable.Range(0, rng.Next(0, 12)).Select(_ => ((char)('a' + rng.Next(4))).ToString())];
        for (var i = 0; i < 300; i++)
        {
            var o = Gen();
            var n = Gen();
            var d = LineDiff.Compute(o, n);
            Assert.Equal(n, Replay(d, o));
            Assert.Equal(n, d.Where(x => x.Op != DiffOp.Removed).Select(x => x.Text));
            Assert.Equal(o, d.Where(x => x.Op != DiffOp.Added).Select(x => x.Text));
            Assert.Equal(Enumerable.Range(1, o.Length), d.Where(x => x.OldLine is not null).Select(x => x.OldLine!.Value));
            Assert.Equal(Enumerable.Range(1, n.Length), d.Where(x => x.NewLine is not null).Select(x => x.NewLine!.Value));
        }
    }

    [Fact]
    public void Too_many_lines_throws()
    {
        var big = new string[LineDiff.MaxTotalLines];
        Assert.Throws<ArgumentException>(() => LineDiff.Compute(big, ["x"]));
    }

    [Fact]
    public void Summarize_counts() =>
        Assert.Equal(new DiffStats(2, 1, true), LineDiff.Summarize(LineDiff.Compute(L("abc"), L("aXYc"))));

    private static List<string> Replay(IReadOnlyList<DiffLine> d, string[] old)
    {
        var result = new List<string>();
        foreach (var x in d)
        {
            if (x.Op != DiffOp.Added)
            {
                Assert.Equal(old[x.OldLine!.Value - 1], x.Text);
            }

            if (x.Op != DiffOp.Removed)
            {
                result.Add(x.Text);
            }
        }

        return result;
    }
}
