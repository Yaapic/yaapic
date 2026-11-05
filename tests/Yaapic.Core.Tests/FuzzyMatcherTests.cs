using Yaapic.Core.Search;

namespace Yaapic.Core.Tests;

public class FuzzyMatcherTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_query_matches_everything(string? query)
    {
        var m = FuzzyMatcher.Match(query, "abc");
        Assert.NotNull(m);
        Assert.Equal(0, m.Score);
        Assert.Empty(m.Indices);
    }

    [Fact]
    public void Non_subsequence_returns_null() => Assert.Null(FuzzyMatcher.Match("ba", "ab"));

    [Fact]
    public void Is_case_insensitive_and_reports_indices()
    {
        var m = FuzzyMatcher.Match(" NR ", "New Request");
        Assert.NotNull(m);
        Assert.Equal([0, 4], m.Indices);
    }

    [Fact]
    public void Prefix_beats_mid_word() =>
        Assert.True(FuzzyMatcher.Match("re", "Request")!.Score > FuzzyMatcher.Match("re", "Are")!.Score);

    [Fact]
    public void Word_start_beats_scattered() =>
        Assert.True(FuzzyMatcher.Match("nr", "New Request")!.Score > FuzzyMatcher.Match("nr", "Anchor")!.Score);

    [Fact]
    public void Consecutive_beats_gapped() =>
        Assert.True(FuzzyMatcher.Match("ab", "xab")!.Score > FuzzyMatcher.Match("ab", "xaxb")!.Score);

    [Theory]
    [InlineData("ab", "ab", 14)]
    [InlineData("ab", "a-b", 15)]
    [InlineData("ac", "abc", 9)]
    public void Exact_scores(string query, string text, int score) =>
        Assert.Equal(score, FuzzyMatcher.Match(query, text)!.Score);

    [Fact]
    public void Compare_breaks_ties_by_score_length_then_text()
    {
        var hi = new FuzzyMatch(10, []);
        var lo = new FuzzyMatch(5, []);
        Assert.True(FuzzyMatcher.Compare(hi, "zzz", lo, "a") < 0);
        Assert.True(FuzzyMatcher.Compare(lo, "ab", lo, "abc") < 0);
        Assert.True(FuzzyMatcher.Compare(lo, "ab", lo, "ac") < 0);
        Assert.Equal(0, FuzzyMatcher.Compare(lo, "ab", lo, "ab"));
    }
}
