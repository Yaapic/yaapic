using Yaapic.Core.Updates;

namespace Yaapic.Core.Tests;

public sealed class UpdateSettingsTests
{
    [Fact]
    public void The_chosen_channel_wins_over_the_running_one()
    {
        var app = new AppInfo(AppVersion.Parse("1.0.0"));

        Assert.Equal(AppChannel.Stable, UpdateSettings.Default.EffectiveChannel(app));
        Assert.Equal(AppChannel.Preview, new UpdateSettings(AppChannel.Preview).EffectiveChannel(app));
    }
}

internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public void Advance(TimeSpan by) => _now += by;

    public override DateTimeOffset GetUtcNow() => _now;
}
