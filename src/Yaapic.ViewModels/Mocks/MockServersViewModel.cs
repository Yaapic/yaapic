using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Mocks;
using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Mocks;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;

namespace Yaapic.ViewModels.Mocks;

/// <summary>The Mock servers window: server list, editor, start and stop, request log and generation from a folder (ADR 0020).</summary>
public sealed partial class MockServersViewModel : ObservableObject
{
    private const int LogCapacity = 500;

    private readonly MockServerService _service;
    private readonly IDialogService _dialogs;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly object _inline = new();
    private MockServerDefinition? _saved;
    private MockServerItemViewModel? _selectedServer;
    private bool _loading;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private int _port = 3000;

    [ObservableProperty]
    private bool _cors = true;

    [ObservableProperty]
    private MockRouteViewModel? _selectedRoute;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isDirty;

    [ObservableProperty]
    private IReadOnlyList<string> _validationErrors = [];

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private string? _generationMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedLogHeaders))]
    [NotifyPropertyChangedFor(nameof(SelectedLogBody))]
    private MockLogRowViewModel? _selectedLog;

    /// <summary>Initializes a new instance of the <see cref="MockServersViewModel"/> class.</summary>
    /// <param name="service">The mock server service.</param>
    /// <param name="dialogs">The dialog service.</param>
    public MockServersViewModel(MockServerService service, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(dialogs);
        _service = service;
        _dialogs = dialogs;
        Routes.CollectionChanged += (_, _) => Touch();
        service.ServersChanged += (_, _) => Post(RefreshServers);
        service.RunningChanged += (_, _) => Post(RefreshRunning);
        service.RequestLogged += (_, e) => Post(() => OnRequestLogged(e));
    }

    /// <summary>Gets the servers.</summary>
    public ObservableCollection<MockServerItemViewModel> Servers { get; } = [];

    /// <summary>Gets the selected server; change it with <see cref="SelectServerAsync"/>, which asks about unsaved edits.</summary>
    public MockServerItemViewModel? SelectedServer => _selectedServer;

    /// <summary>Gets a value indicating whether the selected server runs.</summary>
    public bool SelectedIsRunning => _selectedServer?.IsRunning ?? false;

    /// <summary>Gets the routes of the selected server.</summary>
    public ObservableCollection<MockRouteViewModel> Routes { get; } = [];

    /// <summary>Gets the requests of the selected server, newest first.</summary>
    public ObservableCollection<MockLogRowViewModel> Log { get; } = [];

    /// <summary>Gets the request headers of <see cref="SelectedLog"/>.</summary>
    public IReadOnlyList<KeyValueEntry> SelectedLogHeaders => SelectedLog?.Entry.RequestHeaders ?? [];

    /// <summary>Gets the request body preview of <see cref="SelectedLog"/>.</summary>
    public string SelectedLogBody => SelectedLog?.Entry.RequestBodyPreview ?? "";

    /// <summary>Gets the number of running servers.</summary>
    public int RunningCount => _service.RunningCount;

    /// <summary>Gets the status bar text; empty when no server runs.</summary>
    public string RunningText => RunningCount == 0 ? "" : Strings.MockStatusBar(RunningCount);

    /// <summary>Loads the servers from the store and selects the first one.</summary>
    /// <returns>A task that completes when the list is shown.</returns>
    public async Task LoadAsync()
    {
        try
        {
            await _service.LoadAsync(CancellationToken.None);
            Error = null;
        }
        catch (StorageException ex)
        {
            Error = ex.Message;
            return;
        }

        RefreshServers();
        if (_selectedServer is null && Servers.Count > 0)
        {
            await SelectServerAsync(Servers[0]);
        }
    }

    /// <summary>Selects a server; with unsaved edits, asks first and keeps the selection when canceled.</summary>
    /// <param name="server">The server to select, or <see langword="null"/> for none.</param>
    /// <returns><see langword="true"/> when <paramref name="server"/> is now selected.</returns>
    public async Task<bool> SelectServerAsync(MockServerItemViewModel? server)
    {
        if (server == _selectedServer)
        {
            return true;
        }

        if (IsDirty && _selectedServer is not null)
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

        _selectedServer = server;
        OnPropertyChanged(nameof(SelectedServer));
        OnPropertyChanged(nameof(SelectedIsRunning));
        LoadEditor(server is null ? null : _service.Servers.FirstOrDefault(s => s.Id == server.Id));
        ReloadLog();
        return true;
    }

    /// <summary>Creates a mock server from the requests of a folder, saves it and selects it.</summary>
    /// <param name="folder">The folder.</param>
    /// <returns>A task that completes when the server is created.</returns>
    public async Task GenerateFromFolderAsync(CollectionNode folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var generation = MockGenerator.FromFolder(folder);
        if (await StoreAndSelectAsync(generation.Server))
        {
            GenerationMessage = Strings.MockGenerated(generation.Server.Routes.Count, generation.Skipped);
        }
    }

    private bool CanSave() => IsDirty;

    [RelayCommand]
    private async Task NewServerAsync() => await StoreAndSelectAsync(MockServerDefinition.Create(Strings.MockNewName));

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveEditedAsync();

    [RelayCommand]
    private void Revert() => LoadEditor(_saved);

    [RelayCommand]
    private async Task DeleteServerAsync()
    {
        if (_selectedServer is not { } item || !await _dialogs.ConfirmDeleteAsync(item.Name))
        {
            return;
        }

        try
        {
            await _service.DeleteAsync(item.Id, CancellationToken.None);
            Error = null;
        }
        catch (StorageException ex)
        {
            Error = ex.Message;
            return;
        }

        RefreshServers();
        RefreshRunning();
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (_selectedServer is not { } item)
        {
            return;
        }

        try
        {
            await _service.StartAsync(item.Id, CancellationToken.None);
            Error = null;
        }
        catch (Exception ex) when (ex is MockPortInUseException or InvalidOperationException)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (_selectedServer is { } item)
        {
            await _service.StopAsync(item.Id);
        }
    }

    [RelayCommand]
    private async Task CopyUrlAsync()
    {
        if (_selectedServer is { } item)
        {
            await _dialogs.SetClipboardTextAsync(item.BaseUrl);
        }
    }

    [RelayCommand]
    private void AddRoute() => SelectedRoute = Insert(Routes.Count, MockRoute.Create(Strings.MockNewRoute));

    [RelayCommand]
    private void DuplicateRoute()
    {
        if (SelectedRoute is { } route)
        {
            var copy = route.ToRoute();
            SelectedRoute = Insert(Routes.IndexOf(route) + 1, copy with { Id = Guid.NewGuid(), Name = Strings.CommonCopyName(copy.Name) });
        }
    }

    [RelayCommand]
    private void DeleteRoute()
    {
        if (SelectedRoute is not { } route)
        {
            return;
        }

        var index = Routes.IndexOf(route);
        Routes.Remove(route);
        SelectedRoute = Routes.Count == 0 ? null : Routes[Math.Min(index, Routes.Count - 1)];
    }

    [RelayCommand]
    private void MoveRouteUp() => Move(-1);

    [RelayCommand]
    private void MoveRouteDown() => Move(1);

    [RelayCommand]
    private void ClearLog()
    {
        if (_selectedServer is { } item)
        {
            _service.ClearLog(item.Id);
            ReloadLog();
        }
    }

    partial void OnNameChanged(string value) => Touch();

    partial void OnPortChanged(int value) => Touch();

    partial void OnCorsChanged(bool value) => Touch();

    private void Move(int delta)
    {
        if (SelectedRoute is not { } route)
        {
            return;
        }

        var from = Routes.IndexOf(route);
        var to = from + delta;
        if (to >= 0 && to < Routes.Count)
        {
            Routes.Move(from, to);
        }
    }

    private MockRouteViewModel Insert(int index, MockRoute route)
    {
        var vm = new MockRouteViewModel(route);
        vm.Changed += (_, _) => Touch();
        Routes.Insert(index, vm);
        return vm;
    }

    private MockServerDefinition BuildDefinition() =>
        new(_saved?.Id ?? Guid.Empty, Name, Port, Cors, [.. Routes.Select(r => r.ToRoute())]);

    private void Touch()
    {
        if (!_loading)
        {
            IsDirty = _saved is not null && !BuildDefinition().IsEquivalentTo(_saved);
        }
    }

    private void LoadEditor(MockServerDefinition? server)
    {
        _loading = true;
        try
        {
            _saved = server;
            Name = server?.Name ?? "";
            Port = server?.Port ?? 3000;
            Cors = server?.Cors ?? true;
            Routes.Clear();
            foreach (var route in server?.Routes ?? [])
            {
                Insert(Routes.Count, route);
            }

            SelectedRoute = Routes.Count > 0 ? Routes[0] : null;
            ValidationErrors = [];
            IsDirty = false;
        }
        finally
        {
            _loading = false;
        }
    }

    // Stores a definition that is not the edited one (new or generated) and selects it.
    private async Task<bool> StoreAndSelectAsync(MockServerDefinition server)
    {
        try
        {
            var issues = await _service.SaveAsync(server, CancellationToken.None);
            if (issues.Count > 0)
            {
                ValidationErrors = [.. issues.Select(IssueLine)];
                return false;
            }

            Error = null;
        }
        catch (StorageException ex)
        {
            Error = ex.Message;
            return false;
        }

        RefreshServers();
        return await SelectServerAsync(Servers.FirstOrDefault(s => s.Id == server.Id));
    }

    private async Task<bool> SaveEditedAsync()
    {
        if (_saved is null)
        {
            return false;
        }

        var definition = BuildDefinition();
        try
        {
            var issues = await _service.SaveAsync(definition, CancellationToken.None);
            if (issues.Count > 0)
            {
                ValidationErrors = [.. issues.Select(IssueLine)];
                foreach (var route in Routes)
                {
                    route.IssueText = issues.FirstOrDefault(i => i.RouteId == route.Id) is { } first ? IssueLine(first) : "";
                }

                return false;
            }

            Error = null;
        }
        catch (StorageException ex)
        {
            Error = ex.Message;
            return false;
        }
        catch (MockPortInUseException ex)
        {
            // The definition is stored; only the restart on the new port failed.
            Error = ex.Message;
        }

        _saved = definition;
        ValidationErrors = [];
        IsDirty = false;
        RefreshServers();
        RefreshRunning();
        return true;
    }

    private static string IssueLine(MockValidationIssue issue) => issue.Issue switch
    {
        MockIssue.NameEmpty => Strings.MockIssueNameEmpty,
        MockIssue.PortOutOfRange => Strings.MockIssuePortOutOfRange,
        MockIssue.RouteMethodInvalid => Strings.MockIssueRouteMethodInvalid,
        MockIssue.RoutePathInvalid => Strings.MockIssueRoutePathInvalid,
        MockIssue.RouteStatusOutOfRange => Strings.MockIssueRouteStatusOutOfRange,
        MockIssue.RouteDelayOutOfRange => Strings.MockIssueRouteDelayOutOfRange,
        _ => Strings.MockIssueRouteHeaderNameInvalid,
    };

    private void RefreshServers()
    {
        var existing = Servers.ToDictionary(s => s.Id);
        Servers.Clear();
        foreach (var d in _service.Servers)
        {
            if (existing.TryGetValue(d.Id, out var item))
            {
                item.Update(d);
            }
            else
            {
                item = new MockServerItemViewModel(d, _service.IsRunning(d.Id));
            }

            Servers.Add(item);
        }

        if (_selectedServer is not null && Servers.All(s => s.Id != _selectedServer.Id))
        {
            _selectedServer = null;
            OnPropertyChanged(nameof(SelectedServer));
            OnPropertyChanged(nameof(SelectedIsRunning));
            LoadEditor(null);
            ReloadLog();
        }
    }

    private void RefreshRunning()
    {
        foreach (var s in Servers)
        {
            s.IsRunning = _service.IsRunning(s.Id);
        }

        OnPropertyChanged(nameof(SelectedIsRunning));
        OnPropertyChanged(nameof(RunningCount));
        OnPropertyChanged(nameof(RunningText));
    }

    private void ReloadLog()
    {
        Log.Clear();
        SelectedLog = null;
        if (_selectedServer is { } item)
        {
            foreach (var entry in _service.GetLog(item.Id))
            {
                Log.Add(new MockLogRowViewModel(entry));
            }
        }
    }

    private void OnRequestLogged(MockLogged logged)
    {
        if (_selectedServer?.Id != logged.ServerId)
        {
            return;
        }

        Log.Insert(0, new MockLogRowViewModel(logged.Entry));
        if (Log.Count > LogCapacity)
        {
            Log.RemoveAt(Log.Count - 1);
        }
    }

    // The service raises its events from worker threads; the UI frameworks need them on their own context. Without one (tests, headless
    // hosts) they run inline, one at a time: concurrent connections would otherwise corrupt the log list, and the exception would turn
    // the answer of the mock into a 500.
    private void Post(Action action)
    {
        if (_context is null)
        {
            lock (_inline)
            {
                action();
            }
        }
        else
        {
            _context.Post(_ => action(), null);
        }
    }
}
