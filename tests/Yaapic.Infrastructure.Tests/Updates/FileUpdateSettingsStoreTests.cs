using Yaapic.Core.Updates;
using Yaapic.Infrastructure.Updates;

namespace Yaapic.Infrastructure.Tests.Updates;

public sealed class FileUpdateSettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "yaapic-upd-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public async Task Settings_round_trip()
    {
        var store = new FileUpdateSettingsStore(_dir);
        var settings = new UpdateSettings(AppChannel.Preview, false, new DateTimeOffset(2026, 11, 12, 8, 30, 0, TimeSpan.Zero));

        await store.SaveAsync(settings, TestContext.Current.CancellationToken);

        Assert.Equal(settings, await new FileUpdateSettingsStore(_dir).LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_missing_file_gives_the_defaults() =>
        Assert.Equal(UpdateSettings.Default, await new FileUpdateSettingsStore(_dir).LoadAsync(TestContext.Current.CancellationToken));

    [Theory]
    [InlineData("""{ "channel": "nightly", "checkAutomatically": false }""")]
    [InlineData("""{ "channel": "" , "checkAutomatically": false }""")]
    public async Task An_unknown_channel_falls_back_to_the_running_one(string json)
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Yaapic"));
        await File.WriteAllTextAsync(Path.Combine(_dir, "Yaapic", "updates.json"), json, TestContext.Current.CancellationToken);

        var settings = await new FileUpdateSettingsStore(_dir).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(settings.Channel);
        Assert.False(settings.CheckAutomatically);
    }

    [Fact]
    public async Task A_corrupt_file_gives_the_defaults()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Yaapic"));
        await File.WriteAllTextAsync(Path.Combine(_dir, "Yaapic", "updates.json"), "{ not json", TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSettings.Default, await new FileUpdateSettingsStore(_dir).LoadAsync(TestContext.Current.CancellationToken));
    }
}
