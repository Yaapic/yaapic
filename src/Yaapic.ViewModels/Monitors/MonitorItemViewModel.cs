using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Monitors;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Monitors;

/// <summary>One row of the monitor list.</summary>
public sealed partial class MonitorItemViewModel : ObservableObject
{
    private readonly Action<MonitorItemViewModel> _enabledChanged;
    private bool _updating;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _folderName = "";

    [ObservableProperty]
    private string _environmentName = "";

    [ObservableProperty]
    private string _intervalText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFailing))]
    private bool _enabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastStatusText))]
    [NotifyPropertyChangedFor(nameof(IsFailing))]
    private MonitorRunStatus? _lastStatus;

    [ObservableProperty]
    private string _lastRunText = "";

    [ObservableProperty]
    private string _nextRunText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunningText))]
    private bool _isRunning;

    /// <summary>Initializes a new instance of the <see cref="MonitorItemViewModel"/> class.</summary>
    /// <param name="id">The monitor identifier.</param>
    /// <param name="enabledChanged">Called when the user toggles <see cref="Enabled"/>.</param>
    internal MonitorItemViewModel(Guid id, Action<MonitorItemViewModel> enabledChanged)
    {
        Id = id;
        _enabledChanged = enabledChanged;
    }

    /// <summary>Gets the monitor identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the localized status of the last run, or an empty text when it never ran.</summary>
    public string LastStatusText => StatusText(LastStatus);

    /// <summary>Gets a value indicating whether the monitor is enabled and its last run did not pass.</summary>
    public bool IsFailing => Enabled && LastStatus is { } status && status != MonitorRunStatus.Passed;

    /// <summary>Gets the "Running..." text while the monitor runs, otherwise an empty text.</summary>
    public string RunningText => IsRunning ? Strings.MonitorRunning : "";

    internal static string IntervalToText(int minutes) =>
        minutes < 60 ? $"{minutes.ToString(CultureInfo.InvariantCulture)} min" : $"{(minutes / 60).ToString(CultureInfo.InvariantCulture)} h";

    internal static string StatusText(MonitorRunStatus? status) => status switch
    {
        MonitorRunStatus.Passed => Strings.MonitorStatusPassed,
        MonitorRunStatus.Failed => Strings.MonitorStatusFailed,
        MonitorRunStatus.Error => Strings.MonitorStatusError,
        _ => "",
    };

    internal static string TimeText(DateTimeOffset at) => at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    internal void Update(MonitorDefinition monitor, string? folderName, string? environmentName, MonitorRun? latest, DateTimeOffset? next)
    {
        _updating = true;
        try
        {
            Name = monitor.Name;
            FolderName = folderName ?? Strings.MonitorFolderMissing;
            EnvironmentName = monitor.EnvironmentId is null ? Strings.MonitorNoEnvironment : environmentName ?? Strings.MonitorEnvironmentMissing;
            IntervalText = Strings.MonitorEvery(IntervalToText(monitor.IntervalMinutes));
            Enabled = monitor.Enabled;
            LastStatus = latest?.Status;
            LastRunText = latest is null ? Strings.MonitorNever : TimeText(latest.At);
            NextRunText = next is { } at ? TimeText(at) : "";
        }
        finally
        {
            _updating = false;
        }
    }

    partial void OnEnabledChanged(bool value)
    {
        if (!_updating)
        {
            _enabledChanged(this);
        }
    }
}
