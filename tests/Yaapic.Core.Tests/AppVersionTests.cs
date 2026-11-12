using Yaapic.Core.Updates;

namespace Yaapic.Core.Tests;

public sealed class AppVersionTests
{
    [Theory]
    [InlineData("1.0.0-preview.1", "1.0.0")]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.0.0-preview.2", "1.0.0-preview.10")]
    [InlineData("1.0.0-preview.9", "1.0.0-preview.10")]
    [InlineData("1.0.0-1", "1.0.0-alpha")]
    [InlineData("1.0.0-preview", "1.0.0-preview.1")]
    [InlineData("0.0.0-dev", "0.0.1")]
    [InlineData("1.9.0", "1.10.0")]
    public void Lower_versions_order_before_higher_ones(string lower, string higher)
    {
        Assert.True(AppVersion.Parse(lower) < AppVersion.Parse(higher));
        Assert.True(AppVersion.Parse(higher) > AppVersion.Parse(lower));
    }

    [Fact]
    public void Build_metadata_is_ignored() => Assert.Equal(AppVersion.Parse("1.2.3"), AppVersion.Parse("1.2.3+abcdef"));

    [Theory]
    [InlineData("1.2.3", false, "1.2.3")]
    [InlineData("1.2.3-preview.4+sha", true, "1.2.3-preview.4")]
    public void Parse_round_trips(string text, bool prerelease, string expected)
    {
        var version = AppVersion.Parse(text);

        Assert.Equal(prerelease, version.IsPrerelease);
        Assert.Equal(expected, version.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("a.b.c")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-a..b")]
    [InlineData("-1.2.3")]
    public void Invalid_text_is_refused(string text)
    {
        Assert.False(AppVersion.TryParse(text, out _));
        Assert.Throws<FormatException>(() => AppVersion.Parse(text));
    }

    [Theory]
    [InlineData("1.0.0", AppChannel.Stable)]
    [InlineData("1.0.0-preview.1", AppChannel.Preview)]
    [InlineData("0.0.0-dev", AppChannel.Preview)]
    public void The_channel_follows_the_suffix(string text, AppChannel channel) =>
        Assert.Equal(channel, new AppInfo(AppVersion.Parse(text)).Channel);
}
