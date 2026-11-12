using Yaapic.Core.Updates;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Updates;

namespace Yaapic.ViewModels.Tests;

public sealed class UpdateViewModelTests
{
    private static readonly UpdateInfo V2 = new(AppVersion.Parse("2.0.0"), "notes");
    private static readonly UpdateInfo V3 = new(AppVersion.Parse("3.0.0"));

    private readonly FakeUpdateService _service = new();
    private readonly InMemoryUpdateSettingsStore _store = new();
    private readonly ConsoleViewModel _console = new();

    private UpdateViewModel Create(string version = "1.0.0") =>
        new(_service, _store, new AppInfo(AppVersion.Parse(version)), new UpdateScheduler(TimeProvider.System), _console);

    [Fact]
    public async Task The_first_start_checks_and_shows_the_banner()
    {
        _service.Result = new UpdateCheckResult.Available(V2);
        var vm = Create();

        await vm.InitializeAsync();

        Assert.Equal(UpdateState.Available, vm.State);
        Assert.True(vm.IsBannerVisible);
        Assert.Equal(V2.Version, vm.AvailableVersion);
        Assert.Equal([AppChannel.Stable], _service.Checks);
        Assert.NotNull(_store.Settings.LastCheckUtc);
    }

    [Fact]
    public async Task A_second_start_within_a_day_does_not_check()
    {
        await Create().InitializeAsync();
        _service.Checks.Clear();

        await Create().InitializeAsync();

        Assert.Empty(_service.Checks);
    }

    [Fact]
    public async Task An_automatic_check_with_nothing_new_stays_silent()
    {
        var vm = Create();

        await vm.InitializeAsync();

        Assert.Equal(UpdateState.Idle, vm.State);
        Assert.False(vm.IsBannerVisible);
    }

    [Fact]
    public async Task A_failed_automatic_check_is_silent_in_the_ui_and_logged()
    {
        _service.Result = new UpdateCheckResult.Failed("offline");
        var vm = Create();

        await vm.InitializeAsync();

        Assert.Equal(UpdateState.Idle, vm.State);
        Assert.Null(vm.ErrorMessage);
        Assert.Contains("offline", Assert.Single(_console.Lines).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_manual_check_reports_up_to_date_and_failure()
    {
        var vm = Create();

        await vm.CheckNowCommand.ExecuteAsync(null);
        Assert.Equal(UpdateState.UpToDate, vm.State);

        _service.Result = new UpdateCheckResult.Failed("offline");
        await vm.CheckNowCommand.ExecuteAsync(null);
        Assert.Equal(UpdateState.Failed, vm.State);
        Assert.Contains("offline", vm.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Download_then_restart_applies_the_update()
    {
        _service.Result = new UpdateCheckResult.Available(V2);
        var vm = Create();
        await vm.InitializeAsync();

        await vm.DownloadCommand.ExecuteAsync(null);

        Assert.Equal(UpdateState.ReadyToRestart, vm.State);
        Assert.Equal(100, vm.DownloadPercent);
        Assert.True(_service.AppliedOnRestart);
        vm.RestartAndApplyCommand.Execute(null);
        Assert.True(_service.Restarted);
    }

    [Fact]
    public async Task Cancelling_a_download_returns_to_available()
    {
        TaskCompletionSource gate = new();
        _service.Result = new UpdateCheckResult.Available(V2);
        _service.DownloadGate = gate.Task;
        var vm = Create();
        await vm.InitializeAsync();

        var download = vm.DownloadCommand.ExecuteAsync(null);
        Assert.Equal(UpdateState.Downloading, vm.State);
        Assert.True(vm.CancelDownloadCommand.CanExecute(null));
        vm.CancelDownloadCommand.Execute(null);
        await download;

        Assert.Equal(UpdateState.Available, vm.State);
        Assert.Equal(0, _service.Downloads);
    }

    [Fact]
    public async Task A_failed_download_can_be_retried()
    {
        _service.Result = new UpdateCheckResult.Available(V2);
        _service.DownloadError = new IOException("disk full");
        var vm = Create();
        await vm.InitializeAsync();

        await vm.DownloadCommand.ExecuteAsync(null);
        Assert.Equal(UpdateState.Failed, vm.State);
        Assert.Contains("disk full", vm.ErrorMessage, StringComparison.Ordinal);
        Assert.True(vm.DownloadCommand.CanExecute(null));

        _service.DownloadError = null;
        await vm.DownloadCommand.ExecuteAsync(null);

        Assert.Equal(UpdateState.ReadyToRestart, vm.State);
    }

    [Fact]
    public async Task A_dismissed_version_stays_hidden_until_a_newer_one_exists()
    {
        _service.Result = new UpdateCheckResult.Available(V2);
        var vm = Create();
        await vm.InitializeAsync();

        vm.DismissCommand.Execute(null);
        Assert.False(vm.IsBannerVisible);

        _store.Settings = _store.Settings with { LastCheckUtc = null };
        await Create().InitializeAsync();
        var again = Create();
        await again.InitializeAsync();
        again.DismissCommand.Execute(null);
        _service.Result = new UpdateCheckResult.Available(V3);
        await again.CheckNowCommand.ExecuteAsync(null);

        Assert.True(again.IsBannerVisible);
        Assert.Equal(V3.Version, again.AvailableVersion);
    }

    [Fact]
    public async Task Dismissing_survives_an_automatic_recheck_of_the_same_version()
    {
        _service.Result = new UpdateCheckResult.Available(V2);
        var vm = Create();
        await vm.InitializeAsync();
        vm.DismissCommand.Execute(null);

        _store.Settings = _store.Settings with { LastCheckUtc = null };
        await vm.InitializeAsync();

        Assert.Equal(UpdateState.Available, vm.State);
        Assert.False(vm.IsBannerVisible);
    }

    [Fact]
    public async Task An_unsupported_build_has_no_banner_and_no_commands()
    {
        _service.IsSupported = false;
        var vm = Create();

        await vm.InitializeAsync();

        Assert.Equal(UpdateState.Unsupported, vm.State);
        Assert.False(vm.IsBannerVisible);
        Assert.False(vm.CheckNowCommand.CanExecute(null));
        Assert.Empty(_service.Checks);
    }

    [Fact]
    public async Task Disabling_automatic_checks_is_saved_and_stops_the_startup_check()
    {
        var vm = Create();
        await vm.InitializeAsync();

        vm.CheckAutomatically = false;
        await Task.Yield();
        _service.Checks.Clear();
        _store.Settings = _store.Settings with { LastCheckUtc = null };
        await Create().InitializeAsync();

        Assert.False(_store.Settings.CheckAutomatically);
        Assert.Empty(_service.Checks);
    }

    [Fact]
    public async Task Switching_channel_is_saved_and_checks_that_channel()
    {
        _service.Result = new UpdateCheckResult.Available(V2);
        var vm = Create();
        await vm.InitializeAsync();

        vm.Channel = AppChannel.Preview;
        Assert.Equal(UpdateState.Idle, vm.State);
        Assert.Null(vm.AvailableVersion);
        await vm.CheckNowCommand.ExecuteAsync(null);

        Assert.Equal(AppChannel.Preview, _service.Checks[^1]);
        Assert.Equal(AppChannel.Preview, _store.Settings.Channel);
    }

    [Fact]
    public async Task The_channel_defaults_to_the_running_versions()
    {
        var vm = Create("1.0.0-preview.2");

        await vm.InitializeAsync();

        Assert.Equal(AppChannel.Preview, vm.Channel);
        Assert.Equal(AppChannel.Preview, _service.Checks[0]);
    }

    [Fact]
    public async Task The_texts_and_buttons_follow_the_state()
    {
        _service.Result = new UpdateCheckResult.Available(V2);
        var vm = Create();
        await vm.InitializeAsync();

        Assert.Equal("Version 2.0.0 is available.", vm.BannerText);
        Assert.True(vm.ShowDownload);
        Assert.False(vm.ShowRestart);

        await vm.DownloadCommand.ExecuteAsync(null);

        Assert.Equal("Version 2.0.0 is ready. Restart to update.", vm.StatusText);
        Assert.True(vm.ShowRestart);
        Assert.False(vm.ShowDownload);
    }

    [Fact]
    public async Task The_status_line_reports_check_results()
    {
        var vm = Create();
        Assert.Equal("Not checked yet.", vm.StatusText);

        await vm.CheckNowCommand.ExecuteAsync(null);
        Assert.Equal("Yaapic is up to date.", vm.StatusText);

        _service.IsSupported = false;
        Assert.Equal("Updates are only available in the installed app.", Create().StatusText);
    }

    [Fact]
    public void Opening_the_updates_dialog_raises_the_event()
    {
        var vm = Create();
        var raised = 0;
        vm.SettingsRequested += (_, _) => raised++;

        vm.OpenSettingsCommand.Execute(null);

        Assert.Equal(1, raised);
    }
}
