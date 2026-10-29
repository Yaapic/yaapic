using System.Globalization;
using Yaapic.Core.Variables;

namespace Yaapic.Core.Tests;

public sealed class DynamicVariableSourceTests
{
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, 89, TimeSpan.Zero);

    private static DynamicVariableSource Create() => new(new FixedTime(Now), Guid.NewGuid, new Random(42));

    [Fact]
    public void Guid_is_lowercase_d_format_and_fresh_each_time()
    {
        var source = Create();
        Assert.True(source.TryGet("$guid", out var a));
        Assert.True(source.TryGet("$guid", out var b));
        Assert.NotEqual(a, b);
        Assert.Equal(a.ToLowerInvariant(), a);
        Assert.Equal(a, Guid.Parse(a).ToString("D"));
    }

    [Fact]
    public void Timestamp_is_unix_seconds()
    {
        Assert.True(Create().TryGet("$timestamp", out var value));
        Assert.Equal(Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), value);
    }

    [Fact]
    public void IsoTimestamp_is_utc_with_milliseconds()
    {
        Assert.True(Create().TryGet("$isoTimestamp", out var value));
        Assert.Equal("2026-03-04T05:06:07.089Z", value);
    }

    [Fact]
    public void RandomInt_stays_within_0_and_1000()
    {
        var source = Create();
        for (var i = 0; i < 2000; i++)
        {
            Assert.True(source.TryGet("$randomInt", out var text));
            Assert.InRange(int.Parse(text, CultureInfo.InvariantCulture), 0, 1000);
        }
    }

    [Theory]
    [InlineData("$unknown")]
    [InlineData("$GUID")]
    [InlineData("guid")]
    [InlineData("")]
    public void Unknown_or_wrong_case_names_are_not_found(string name)
    {
        Assert.False(Create().TryGet(name, out var value));
        Assert.Equal("", value);
    }

    [Fact]
    public void Resolver_resolves_guid_through_the_source_and_each_occurrence_differs()
    {
        var result = VariableResolver.Resolve("{{$guid}}|{{$guid}}", new DynamicVariableSource());
        var parts = result.Text.Split('|');
        Assert.Empty(result.Unresolved);
        Assert.True(Guid.TryParse(parts[0], out _));
        Assert.NotEqual(parts[0], parts[1]);
    }
}
