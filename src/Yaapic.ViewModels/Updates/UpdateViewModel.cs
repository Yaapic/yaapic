using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Updates;
using Yaapic.Domain.Scripting;
using Yaapic.ViewModels.Console;

namespace Yaapic.ViewModels.Updates;

/// <summary>What the update UI shows.</summary>
public enum UpdateState
{
    /// <summary>Nothing to show: no check ran yet, an automatic check found nothing or failed silently.</summary>
    Idle,

    /// <summary>A check is running.</summary>
    Checking,

    /// <summary>A newer version can be downloaded.</summary>
    Available,

    /// <summary>The update is downloading.</summary>
    Downloading,

    /// <summary>The update is downloaded and applies on restart.</summary>
    ReadyToRestart,

    /// <summary>A manual check found the running version is the newest.</summary>
    UpToDate,

    /// <summary>A manual check or a download failed.</summary>
    Failed,

    /// <summary>Updates do not work in this build (not installed): the banner and commands are off.</summary>
    Unsupported,
}

/// <summary>The state machine behind the update banner and the update settings.</summary>
public sealed partial class UpdateViewModel : ObservableObject, IDisposable
{
    private readonly IUpdateService _service;
    private readonly IUpdateSettingsStore _store;
    private readonly UpdateScheduler _scheduler;
    private readonly ConsoleViewModel? _console;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _download;
    private UpdateSettings _settings = UpdateSettings.Default;
    private UpdateInfo? _update;
    private AppVersion? _dismissed;
    private readonly IAppInfo _app;
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBannerVisible))]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelDownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestartAndApplyCommand))]
    private UpdateState _state = UpdateState.Idle;

    [ObservableProperty]
    private int _downloadPercent;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private AppChannel _channel;

    [ObservableProperty]
    private bool _checkAutomatically = true;

    [ObservableProperty]
    private DateTimeOffset? _lastCheckUtc;

    /// <summary>Creates the view model.</summary>
    /// <param name="service">The update service.</param>
    /// <param name="store">The per-user update preferences.</param>
    /// <param name="app">The running application.</param>
    /// <param name="scheduler">Decides when an automatic check is due.</param>
    /// <param name="console">Receives the update failures; <see langword="null"/> logs nothing.</param>
    public UpdateViewModel(IUpdateService service, IUpdateSettingsStore store, IAppInfo app, UpdateScheduler scheduler, ConsoleViewModel? console = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(scheduler);
        _service = service;
        _store = store;
        _scheduler = scheduler;
        _console = console;
        CurrentVersion = app.Version;
        _channel = app.Channel;
        _app = app;
        if (!service.IsSupported)
        {
            _state = UpdateState.Unsupported;
        }
    }

    /// <summary>Gets the running version.</summary>
    public AppVersion CurrentVersion { get; }

    /// <summary>Gets the version of the update found, or <see langword="null"/>.</summary>
    public AppVersion? AvailableVersion => _update?.Version;

    /// <summary>Gets the release notes of the update found.</summary>
    public string Notes => _update?.Notes ?? "";

    /// <summary>Gets the channels the user can pick.</summary>
    public IReadOnlyList<AppChannel> Channels { get; } = [AppChannel.Stable, AppChannel.Preview];

    /// <summary>Gets a value indicating whether updates work in this build.</summary>
    public bool IsSupported => _service.IsSupported;

    /// <summary>Gets a value indicating whether a check or a download is running.</summary>
    public bool IsBusy => State is UpdateState.Checking or UpdateState.Downloading;

    /// <summary>Gets a value indicating whether the banner shows: an update is found and the user did not dismiss that version.</summary>
    public bool IsBannerVisible =>
        State is UpdateState.Available or UpdateState.Downloading or UpdateState.ReadyToRestart && _update is not null && _update.Version != _dismissed;

    /// <summary>Loads the preferences and runs the automatic check when it is due; never throws, never blocks start-up.</summary>
    /// <returns>A task completing when the check ended.</returns>
    public async Task InitializeAsync()
    {
        _loading = true;
        try
        {
            _settings = await _store.LoadAsync(_lifetime.Token);
            Channel = _settings.EffectiveChannel(_app);
            CheckAutomatically = _settings.CheckAutomatically;
            LastCheckUtc = _settings.LastCheckUtc;
        }
        finally
        {
            _loading = false;
        }

        if (IsSupported && _scheduler.IsCheckDue(_settings))
        {
            await CheckAsync(manual: false);
        }
    }

    /// <summary>Cancels a running check or download; call when the window closes.</summary>
    public void Dispose()
    {
        _lifetime.Cancel();
        _download?.Cancel();
        _lifetime.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckNowAsync() => CheckAsync(manual: true);

    private bool CanCheck() => IsSupported && !IsBusy && State != UpdateState.ReadyToRestart;

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        var update = _update!;
        using var source = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _download = source;
        DownloadPercent = 0;
        ErrorMessage = null;
        State = UpdateState.Downloading;
        try
        {
            await _service.DownloadAsync(update, new InlineProgress(p => DownloadPercent = p), source.Token);
            _service.ApplyOnRestart();
            State = UpdateState.ReadyToRestart;
        }
        catch (OperationCanceledException)
        {
            State = UpdateState.Available;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Fail($"Update download failed: {ex.Message}");
        }
        finally
        {
            _download = null;
        }
    }

    private bool CanDownload() => IsSupported && _update is not null && State is UpdateState.Available or UpdateState.Failed;

    [RelayCommand(CanExecute = nameof(CanCancelDownload))]
    private void CancelDownload() => _download?.Cancel();

    private bool CanCancelDownload() => State == UpdateState.Downloading;

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private void RestartAndApply() => _service.RestartAndApply();

    private bool CanRestart() => State == UpdateState.ReadyToRestart;

    /// <summary>Hides the banner for the version found; a newer version shows it again.</summary>
    [RelayCommand]
    private void Dismiss()
    {
        _dismissed = _update?.Version;
        OnPropertyChanged(nameof(IsBannerVisible));
    }

    partial void OnChannelChanged(AppChannel value)
    {
        if (_loading)
        {
            return;
        }

        // Another channel is another feed: forget what the previous one offered.
        _update = null;
        _dismissed = null;
        if (State is UpdateState.Available or UpdateState.UpToDate or UpdateState.Failed)
        {
            State = IsSupported ? UpdateState.Idle : UpdateState.Unsupported;
        }

        OnPropertyChanged(nameof(AvailableVersion));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(IsBannerVisible));
        _ = SaveAsync(_settings with { Channel = value });
    }

    partial void OnCheckAutomaticallyChanged(bool value)
    {
        if (!_loading)
        {
            _ = SaveAsync(_settings with { CheckAutomatically = value });
        }
    }

    private async Task CheckAsync(bool manual)
    {
        if (IsBusy || State == UpdateState.ReadyToRestart)
        {
            return;
        }

        var previous = State;
        State = UpdateState.Checking;
        ErrorMessage = null;
        if (manual)
        {
            _dismissed = null;
        }

        UpdateCheckResult result;
        try
        {
            result = await _service.CheckAsync(Channel, _lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            State = previous;
            return;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The port promises not to throw; a broken adapter must still not take the app down from a background check.
            result = new UpdateCheckResult.Failed(ex.Message);
        }

        await SaveAsync(_scheduler.MarkChecked(_settings));
        switch (result)
        {
            case UpdateCheckResult.Available available:
                _update = available.Update;
                OnPropertyChanged(nameof(AvailableVersion));
                OnPropertyChanged(nameof(Notes));
                State = UpdateState.Available;
                OnPropertyChanged(nameof(IsBannerVisible));
                break;
            case UpdateCheckResult.Failed failed:
                _console?.AppendSystem(ConsoleLevel.Warn, $"Update check failed: {failed.Reason}");
                if (manual)
                {
                    Fail($"Update check failed: {failed.Reason}", log: false);
                }
                else
                {
                    State = UpdateState.Idle;
                }

                break;
            default:
                State = manual ? UpdateState.UpToDate : UpdateState.Idle;
                break;
        }
    }

    private void Fail(string message, bool log = true)
    {
        if (log)
        {
            _console?.AppendSystem(ConsoleLevel.Warn, message);
        }

        ErrorMessage = message;
        State = UpdateState.Failed;
    }

    private async Task SaveAsync(UpdateSettings settings)
    {
        _settings = settings;
        LastCheckUtc = settings.LastCheckUtc;
        try
        {
            await _store.SaveAsync(settings, CancellationToken.None);
        }
        catch (Yaapic.Core.Storage.StorageException ex)
        {
            _console?.AppendSystem(ConsoleLevel.Warn, ex.Message);
        }
    }

    // Reports on the calling thread: the service reports from its own, and the UI marshals property changes itself.
    private sealed class InlineProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }
}
