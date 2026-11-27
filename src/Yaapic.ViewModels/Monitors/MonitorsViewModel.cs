using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Monitors;
using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Monitors;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Console;

namespace Yaapic.ViewModels.Monitors;

/// <summary>The Monitors window: monitor list, editor, run history and the failing-monitors status item (ADR 0021).</summary>
public sealed partial class MonitorsViewModel : ObservableObject
{
    private const int HistorySize = 50;

    private readonly MonitorScheduler _scheduler;
    private readonly IMonitorStore _monitors;
    private readonly IMonitorRunStore _runs;
    private readonly ICollectionStore _collections;
    private readonly IEnvironmentStore _environments;
    private readonly ConsoleViewModel _console;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly Dictionary<Guid, MonitorDefinition> _definitions = [];
    private MonitorDefinition? _saved;
    private MonitorItemViewModel? _selectedMonitor;
    private IReadOnlyList<CollectionNode> _topFolders = [];
    private readonly SemaphoreSlim _refreshing = new(1, 1);
    private bool _loading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = "";

    [ObservableProperty]
    private IReadOnlyList<MonitorFolderChoice> _folderChoices = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private MonitorFolderChoice? _selectedFolder;

    [ObservableProperty]
    private IReadOnlyList<MonitorEnvironmentChoice> _environmentChoices = [];

    [ObservableProperty]
    private MonitorEnvironmentChoice? _selectedEnvironment;

    [ObservableProperty]
    private MonitorIntervalChoice? _selectedInterval;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isDirty;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private MonitorRunRowViewModel? _selectedRun;

    /// <summary>Initializes a new instance of the <see cref="MonitorsViewModel"/> class.</summary>
    /// <param name="scheduler">The monitor scheduler.</param>
    /// <param name="monitors">The monitor store.</param>
    /// <param name="runs">The run history store.</param>
    /// <param name="collections">The collection store; holds the folders monitors run.</param>
    /// <param name="environments">The environment store.</param>
    /// <param name="console">The console that receives the failure warnings.</param>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="time">The clock.</param>
    public MonitorsViewModel(
        MonitorScheduler scheduler,
        IMonitorStore monitors,
        IMonitorRunStore runs,
        ICollectionStore collections,
        IEnvironmentStore environments,
        ConsoleViewModel console,
        IDialogService dialogs,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(monitors);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(collections);
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(time);
        _scheduler = scheduler;
        _monitors = monitors;
        _runs = runs;
        _collections = collections;
        _environments = environments;
        _console = console;
        _dialogs = dialogs;
        _time = time;
        IntervalChoices =
        [
            .. MonitorDefinition.AllowedIntervals.Select(static m => new MonitorIntervalChoice(m, MonitorItemViewModel.IntervalToText(m))),
        ];
        scheduler.RunCompleted += (_, run) => Post(() => OnRunCompleted(run));
        scheduler.StateChanged += (_, _) => Post(RefreshRunning);
    }

    /// <summary>Gets the monitors.</summary>
    public ObservableCollection<MonitorItemViewModel> Monitors { get; } = [];

    /// <summary>Gets the selected monitor; change it with <see cref="SelectMonitorAsync"/>, which asks about unsaved edits.</summary>
    public MonitorItemViewModel? SelectedMonitor => _selectedMonitor;

    /// <summary>Gets the intervals a monitor can run at.</summary>
    public IReadOnlyList<MonitorIntervalChoice> IntervalChoices { get; }

    /// <summary>Gets the last runs of the selected monitor, newest first.</summary>
    public ObservableCollection<MonitorRunRowViewModel> History { get; } = [];

    /// <summary>Gets the number of enabled monitors whose last run did not pass.</summary>
    public int FailingCount => Monitors.Count(static m => m.IsFailing);

    /// <summary>Gets the status bar text; empty when no monitor fails.</summary>
    public string FailingText => FailingCount == 0 ? "" : Strings.MonitorStatusBar(FailingCount);

    /// <summary>Loads the monitors, folders and environments, and selects the first monitor.</summary>
    /// <returns>A task that completes when the list is shown.</returns>
    public async Task LoadAsync()
    {
        try
        {
            await RefreshAsync();
            Error = null;
        }
        catch (StorageException ex)
        {
            Error = ex.Message;
            return;
        }

        if (_selectedMonitor is null && Monitors.Count > 0)
        {
            await SelectMonitorAsync(Monitors[0]);
        }
    }

    /// <summary>Selects a monitor; with unsaved edits, asks first and keeps the selection when canceled.</summary>
    /// <param name="monitor">The monitor to select, or <see langword="null"/> for none.</param>
    /// <returns><see langword="true"/> when <paramref name="monitor"/> is now selected.</returns>
    public async Task<bool> SelectMonitorAsync(MonitorItemViewModel? monitor)
    {
        if (monitor == _selectedMonitor)
        {
            return true;
        }

        if (IsDirty && _selectedMonitor is not null)
        {
            switch (await _dialogs.ConfirmUnsavedAsync(Name))
            {
                case UnsavedChoice.Save:
                    if (!await SaveEditedAsync())
                    {
                        return false;
                    }

                    break;
                case UnsavedChoice.Discard:
                    break;
                default:
                    return false;
            }
        }

        _selectedMonitor = monitor;
        OnPropertyChanged(nameof(SelectedMonitor));
        LoadEditor(monitor is not null && _definitions.TryGetValue(monitor.Id, out var definition) ? definition : null);
        await ReloadHistoryAsync();
        return true;
    }

    /// <summary>Creates a monitor for a folder, saves it and selects it.</summary>
    /// <param name="folderId">The folder to monitor.</param>
    /// <returns>A task that completes when the monitor is created.</returns>
    public async Task NewForFolderAsync(Guid folderId)
    {
        var monitor = MonitorDefinition.Create(Strings.MonitorNewName, folderId);
        try
        {
            await _monitors.SaveAsync(monitor, CancellationToken.None);
            await RefreshAsync();
            Error = null;
        }
        catch (StorageException ex)
        {
            Error = ex.Message;
            return;
        }

        await SelectMonitorAsync(Monitors.FirstOrDefault(m => m.Id == monitor.Id));
    }

    private bool CanSave() => IsDirty && !string.IsNullOrWhiteSpace(Name) && SelectedFolder is not null;

    private bool CanCreate() => _topFolders.Count > 0;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task NewMonitorAsync()
    {
        if (_topFolders.Count > 0)
        {
            await NewForFolderAsync(_topFolders[0].Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveEditedAsync();

    [RelayCommand]
    private void Revert() => LoadEditor(_saved);

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_selectedMonitor is not { } item || !await _dialogs.ConfirmDeleteAsync(item.Name))
        {
            return;
        }

        try
        {
            await _monitors.DeleteAsync(item.Id, CancellationToken.None);
            await _runs.DeleteAsync(item.Id, CancellationToken.None);
            await RefreshAsync();
            Error = null;
        }
        catch (StorageException ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void RunNow()
    {
        if (_selectedMonitor is { } item)
        {
            _scheduler.RunNow(item.Id);
        }
    }

    partial void OnNameChanged(string value) => Touch();

    partial void OnSelectedFolderChanged(MonitorFolderChoice? value) => Touch();

    partial void OnSelectedEnvironmentChanged(MonitorEnvironmentChoice? value) => Touch();

    partial void OnSelectedIntervalChanged(MonitorIntervalChoice? value) => Touch();

    // A folder, environment or interval that is gone (null selection) keeps its saved value, so it never makes the editor dirty.
    private MonitorDefinition BuildDefinition()
    {
        var saved = _saved!;
        return saved with
        {
            Name = Name,
            FolderId = SelectedFolder?.Id ?? saved.FolderId,
            EnvironmentId = SelectedEnvironment is null ? saved.EnvironmentId : SelectedEnvironment.Id,
            IntervalMinutes = SelectedInterval?.Minutes ?? saved.IntervalMinutes,
            Enabled = _definitions.TryGetValue(saved.Id, out var current) ? current.Enabled : saved.Enabled,
        };
    }

    private void Touch()
    {
        if (!_loading)
        {
            IsDirty = _saved is not null && BuildDefinition() != _saved;
        }
    }

    private void LoadEditor(MonitorDefinition? monitor)
    {
        _loading = true;
        try
        {
            _saved = monitor;
            Name = monitor?.Name ?? "";
            SelectedFolder = monitor is null ? null : FolderChoices.FirstOrDefault(c => c.Id == monitor.FolderId);
            SelectedEnvironment = monitor is null ? null : EnvironmentChoices.FirstOrDefault(c => c.Id == monitor.EnvironmentId);
            SelectedInterval = monitor is null ? null : IntervalChoices.FirstOrDefault(c => c.Minutes == monitor.IntervalMinutes);
            IsDirty = false;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task<bool> SaveEditedAsync()
    {
        if (_saved is null || !CanSave())
        {
            return false;
        }

        var definition = BuildDefinition();
        try
        {
            await _monitors.SaveAsync(definition, CancellationToken.None);
            Error = null;
        }
        catch (StorageException ex)
        {
            Error = ex.Message;
            return false;
        }

        _saved = definition;
        _definitions[definition.Id] = definition;
        IsDirty = false;
        await UpdateItemAsync(definition);
        return true;
    }

    // Reloads monitors, folders and environments from the stores and rebuilds the list, keeping the selection when it still exists.
    private async Task RefreshAsync()
    {
        // Serialized: the startup load and a user action can overlap, and two interleaved rebuilds would duplicate the items.
        await _refreshing.WaitAsync();
        try
        {
            await RefreshCoreAsync();
        }
        finally
        {
            _refreshing.Release();
        }
    }

    private async Task RefreshCoreAsync()
    {
        var definitions = await _monitors.ListAsync(CancellationToken.None);
        var tree = await _collections.LoadTreeAsync(CancellationToken.None);
        var environments = await _environments.ListAsync(CancellationToken.None);
        var latest = new Dictionary<Guid, MonitorRun?>();
        foreach (var d in definitions)
        {
            latest[d.Id] = await _runs.LatestAsync(d.Id, CancellationToken.None);
        }

        _topFolders = [.. tree.Where(static n => n.Kind == CollectionNodeKind.Folder)];
        FolderChoices = [.. FolderPaths(tree)];
        EnvironmentChoices = [new MonitorEnvironmentChoice(null, Strings.MonitorNoEnvironment), .. environments.Select(static e => new MonitorEnvironmentChoice(e.Id, e.Name))];
        NewMonitorCommand.NotifyCanExecuteChanged();

        var existing = Monitors.ToDictionary(static m => m.Id);
        _definitions.Clear();
        Monitors.Clear();
        foreach (var d in definitions)
        {
            _definitions[d.Id] = d;
            if (!existing.TryGetValue(d.Id, out var item))
            {
                item = new MonitorItemViewModel(d.Id, OnEnabledToggled);
            }

            item.Update(d, FolderPath(d.FolderId), EnvironmentName(d), latest[d.Id], MonitorScheduler.NextRun(d, latest[d.Id], _time.GetUtcNow()));
            Monitors.Add(item);
        }

        RefreshRunning();
        if (_selectedMonitor is not null && Monitors.All(m => m.Id != _selectedMonitor.Id))
        {
            _selectedMonitor = null;
            OnPropertyChanged(nameof(SelectedMonitor));
            LoadEditor(null);
            History.Clear();
            SelectedRun = null;
        }

        NotifyFailing();
    }

    private string? FolderPath(Guid folderId) => FolderChoices.FirstOrDefault(c => c.Id == folderId)?.Path;

    private string? EnvironmentName(MonitorDefinition definition) =>
        EnvironmentChoices.FirstOrDefault(c => c.Id is not null && c.Id == definition.EnvironmentId)?.Name;

    // Top-level folders first, then deeper ones, each level by path.
    private static IEnumerable<MonitorFolderChoice> FolderPaths(IReadOnlyList<CollectionNode> tree)
    {
        var all = new List<(int Depth, MonitorFolderChoice Choice)>();
        Walk(tree, "", 0);
        return all.OrderBy(static x => x.Depth).ThenBy(static x => x.Choice.Path, StringComparer.OrdinalIgnoreCase).Select(static x => x.Choice);

        void Walk(IReadOnlyList<CollectionNode> nodes, string prefix, int depth)
        {
            foreach (var node in nodes.Where(static n => n.Kind == CollectionNodeKind.Folder))
            {
                var path = prefix.Length == 0 ? node.Name : $"{prefix}/{node.Name}";
                all.Add((depth, new MonitorFolderChoice(node.Id, path)));
                Walk(node.Children, path, depth + 1);
            }
        }
    }

    private async Task UpdateItemAsync(MonitorDefinition definition)
    {
        if (Monitors.FirstOrDefault(m => m.Id == definition.Id) is not { } item)
        {
            return;
        }

        var latest = await _runs.LatestAsync(definition.Id, CancellationToken.None);
        item.Update(definition, FolderPath(definition.FolderId), EnvironmentName(definition), latest, MonitorScheduler.NextRun(definition, latest, _time.GetUtcNow()));
        NotifyFailing();
    }

    private void OnEnabledToggled(MonitorItemViewModel item) => _ = ToggleEnabledAsync(item);

    private async Task ToggleEnabledAsync(MonitorItemViewModel item)
    {
        if (!_definitions.TryGetValue(item.Id, out var current))
        {
            return;
        }

        var updated = current with { Enabled = item.Enabled };
        try
        {
            await _monitors.SaveAsync(updated, CancellationToken.None);
            Error = null;
        }
        catch (StorageException ex)
        {
            Error = ex.Message;
            await UpdateItemAsync(current);
            return;
        }

        _definitions[item.Id] = updated;
        if (_saved?.Id == item.Id)
        {
            _saved = _saved with { Enabled = item.Enabled };
        }

        await UpdateItemAsync(updated);
    }

    private async Task ReloadHistoryAsync()
    {
        History.Clear();
        SelectedRun = null;
        if (_selectedMonitor is not { } item)
        {
            return;
        }

        try
        {
            foreach (var run in await _runs.ListAsync(item.Id, HistorySize, CancellationToken.None))
            {
                History.Add(new MonitorRunRowViewModel(run));
            }
        }
        catch (StorageException ex)
        {
            Error = ex.Message;
        }
    }

    private void OnRunCompleted(MonitorRun run)
    {
        if (Monitors.FirstOrDefault(m => m.Id == run.MonitorId) is not { } item || !_definitions.TryGetValue(item.Id, out var definition))
        {
            return;
        }

        item.Update(definition, FolderPath(definition.FolderId), EnvironmentName(definition), run, MonitorScheduler.NextRun(definition, run, _time.GetUtcNow()));
        if (run.Status != MonitorRunStatus.Passed)
        {
            _console.AppendSystem(ConsoleLevel.Warn, Strings.MonitorFailed(definition.Name, run.Message ?? MonitorItemViewModel.StatusText(run.Status)));
        }

        NotifyFailing();

        // The history row is the last effect, so it signals that the console line and the failing count are already visible.
        if (_selectedMonitor?.Id == run.MonitorId)
        {
            History.Insert(0, new MonitorRunRowViewModel(run));
            if (History.Count > HistorySize)
            {
                History.RemoveAt(History.Count - 1);
            }
        }
    }

    private void RefreshRunning()
    {
        var running = _scheduler.Running;
        foreach (var item in Monitors)
        {
            item.IsRunning = item.Id == running;
        }
    }

    private void NotifyFailing()
    {
        OnPropertyChanged(nameof(FailingCount));
        OnPropertyChanged(nameof(FailingText));
    }

    // The scheduler raises its events from worker threads; the UI frameworks need them on their own context (inline in tests).
    private void Post(Action action)
    {
        if (_context is null)
        {
            action();
        }
        else
        {
            _context.Post(_ => action(), null);
        }
    }
}
