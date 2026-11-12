using Yaapic.Composition;
using Yaapic.Core.Updates;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Shell;
using Yaapic.ViewModels.Updates;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>The update flow through the real composition with a fake update service: start-up check, banner, download, restart, channel, failure.</summary>
public sealed class UpdatesEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
    private readonly string _vaults = Path.Combine(Path.GetTempPath(), "yaapic-vaults-" + Guid.NewGuid().ToString("N"));
    private readonly FakeUpdateService _service = new();
    private readonly InMemoryUpdateSettingsStore _settings = new();

    public void Dispose()
    {
        TempWorkspace.Delete(_root);
        TempWorkspace.Delete(_vaults);
    }

    private ShellViewModel Create() =>
        AppComposition.CreateShell(_root, new FakeDialogService(), null, new ScriptedShellFixture.TempVaultLocation(_vaults), [], null, _service, _settings);

    private static async Task<bool> WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        return condition();
    }

    [Fact]
    public async Task A_new_version_found_at_start_up_is_downloaded_and_applied()
    {
        _service.Result = new UpdateCheckResult.Available(new UpdateInfo(AppVersion.Parse("9.0.0"), "notes"));
        var shell = Create();

        await shell.InitializeAsync();
        Assert.True(await WaitAsync(() => shell.Updates.State == UpdateState.Available));
        Assert.True(shell.Updates.IsBannerVisible);

        await shell.Updates.DownloadCommand.ExecuteAsync(null);
        Assert.Equal(UpdateState.ReadyToRestart, shell.Updates.State);
        Assert.True(_service.AppliedOnRestart);
        Assert.Contains("Restart to update", new Yaapic.ViewModels.Palette.CommandsPaletteSource(shell).GetItems().Select(i => i.Title));

        shell.Updates.RestartAndApplyCommand.Execute(null);
        Assert.True(_service.Restarted);
    }

    [Fact]
    public async Task An_offline_start_up_check_does_not_disturb_the_app_and_is_logged()
    {
        _service.Result = new UpdateCheckResult.Failed("offline");
        var shell = Create();

        await shell.InitializeAsync();
        Assert.True(await WaitAsync(() => _service.Checks.Count == 1 && shell.Updates.State == UpdateState.Idle));

        Assert.False(shell.Updates.IsBannerVisible);
        Assert.Contains(shell.Console.Lines, l => l.Message.Contains("offline", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Switching_the_channel_checks_that_channel_and_survives_a_restart_of_the_app()
    {
        var shell = Create();
        await shell.InitializeAsync();
        Assert.True(await WaitAsync(() => _service.Checks.Count == 1));

        // The running app's own channel depends on the test host's version, so pick the other one.
        var other = shell.Updates.Channel == AppChannel.Preview ? AppChannel.Stable : AppChannel.Preview;
        shell.Updates.Channel = other;
        await shell.Updates.CheckNowCommand.ExecuteAsync(null);
        Assert.Equal(other, _service.Checks[^1]);
        Assert.True(await WaitAsync(() => _settings.Settings.Channel == other));

        var again = Create();
        await again.InitializeAsync();
        Assert.Equal(other, again.Updates.Channel);
    }
}
