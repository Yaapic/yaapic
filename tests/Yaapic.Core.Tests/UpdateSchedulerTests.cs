using Yaapic.Core.Updates;

namespace Yaapic.Core.Tests;

public sealed class UpdateSchedulerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero));


    [Fact]
    public void The_first_start_checks() => Assert.True(new UpdateScheduler(_clock).IsCheckDue(UpdateSettings.Default));

    [Fact]
    public void A_second_start_within_a_day_does_not_check()
    {
        var scheduler = new UpdateScheduler(_clock);
        var settings = scheduler.MarkChecked(UpdateSettings.Default);

        _clock.Advance(TimeSpan.FromHours(23));

        Assert.False(scheduler.IsCheckDue(settings));
    }

    [Fact]
    public void After_a_day_the_check_is_due_again()
    {
        var scheduler = new UpdateScheduler(_clock);
        var settings = scheduler.MarkChecked(UpdateSettings.Default);

        _clock.Advance(TimeSpan.FromHours(24));

        Assert.True(scheduler.IsCheckDue(settings));
    }

    [Fact]
    public void Disabled_automatic_checks_never_run() =>
        Assert.False(new UpdateScheduler(_clock).IsCheckDue(new UpdateSettings(CheckAutomatically: false)));

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
