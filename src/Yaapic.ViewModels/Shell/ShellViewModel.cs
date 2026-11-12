using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Scripting;
using Yaapic.Core.Storage;
using Yaapic.Domain.Scripting;
using Yaapic.Domain.Workspace;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.History;
using Yaapic.ViewModels.Palette;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Tabs;
using Yaapic.ViewModels.Updates;
using Yaapic.ViewModels.Vault;

namespace Yaapic.ViewModels.Shell;

/// <summary>The root view model of the application window: owns the sidebar, tabs and environments and restores the session.</summary>
public sealed partial class ShellViewModel : ObservableObject, IVaultReloader
{
    /// <summary>The default height of the console panel.</summary>
    public const double DefaultConsoleHeight = 250;

    /// <summary>The smallest height of the console panel.</summary>
    public const double MinConsoleHeight = 120;

    /// <summary>The largest height of the console panel.</summary>
    public const double MaxConsoleHeight = 600;

    private readonly ISessionStore _session;
    private readonly IDialogService _dialogs;
    private readonly ScriptEngineRegistry _engines;
    private CommandPaletteViewModel? _palette;
    private bool _initialized;
    private bool _saving;
    private SessionState? _pending;
    private Task _drain = Task.CompletedTask;

    [ObservableProperty]
    private SidebarMode _mode = SidebarMode.Collections;

    [ObservableProperty]
    private bool _isConsoleOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScriptEngineWarnings))]
    private IReadOnlyList<string> _scriptEngineWarnings = [];

    private double _consoleHeight = DefaultConsoleHeight;
    private bool _vaultInitializing;

    /// <summary>Creates the shell.</summary>
    /// <param name="tabs">The open tabs.</param>
    /// <param name="requests">The sidebar request list.</param>
    /// <param name="collections">The collection tree.</param>
    /// <param name="history">The request history.</param>
    /// <param name="environments">The environment selection and editing.</param>
    /// <param name="globals">The global variables editing.</param>
    /// <param name="session">Where the session is saved.</param>
    /// <param name="dialogs">Asks the user about unsaved changes when the window closes.</param>
    /// <param name="console">The application-wide console.</param>
    /// <param name="vault">The secret vault view model.</param>
    /// <param name="engines">The script engines, checked for availability at start-up.</param>
    /// <param name="updates">The update banner and settings.</param>
    /// <param name="layout">The request / response layout shared with the tabs; <see langword="null"/> creates one.</param>
    public ShellViewModel(
        TabsViewModel tabs,
        RequestListViewModel requests,
        CollectionTreeViewModel collections,
        HistoryViewModel history,
        EnvironmentsViewModel environments,
        GlobalsViewModel globals,
        ISessionStore session,
        IDialogService dialogs,
        ConsoleViewModel console,
        VaultViewModel vault,
        ScriptEngineRegistry engines,
        UpdateViewModel updates,
        PaneLayoutViewModel? layout = null)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(collections);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(updates);
        Tabs = tabs;
        Requests = requests;
        Collections = collections;
        History = history;
        Environments = environments;
        Globals = globals;
        Console = console;
        Vault = vault;
        Updates = updates;
        Layout = layout ?? new PaneLayoutViewModel();
        Layout.PropertyChanged += (_, _) => QueueSave();
        _session = session;
        _dialogs = dialogs;
        _engines = engines;
        tabs.SessionChanged += (_, _) => QueueSave();
        tabs.RequestSaved += (_, _) => QueueSave();
        environments.ActiveEnvironmentChanged += (_, _) => QueueSave();
    }

    /// <summary>Gets the open tabs.</summary>
    public TabsViewModel Tabs { get; }

    /// <summary>Gets the sidebar request list.</summary>
    public RequestListViewModel Requests { get; }

    /// <summary>Gets the collection tree.</summary>
    public CollectionTreeViewModel Collections { get; }

    /// <summary>Gets the request history.</summary>
    public HistoryViewModel History { get; }

    /// <summary>Gets the environments view model.</summary>
    public EnvironmentsViewModel Environments { get; }

    /// <summary>Gets the global variables view model.</summary>
    public GlobalsViewModel Globals { get; }

    /// <summary>Gets the application-wide console.</summary>
    public ConsoleViewModel Console { get; }

    /// <summary>Gets the secret vault view model.</summary>
    public VaultViewModel Vault { get; }

    /// <summary>Gets the request / response panel layout.</summary>
    public PaneLayoutViewModel Layout { get; }

    /// <summary>Gets the update banner and settings.</summary>
    public UpdateViewModel Updates { get; }

    /// <summary>Gets or sets the height of the console panel, clamped to <see cref="MinConsoleHeight"/>..<see cref="MaxConsoleHeight"/>.</summary>
    public double ConsoleHeight
    {
        get => _consoleHeight;
        set => SetProperty(ref _consoleHeight, Math.Clamp(value, MinConsoleHeight, MaxConsoleHeight));
    }

    /// <summary>Gets a value indicating whether a script engine is unavailable.</summary>
    public bool HasScriptEngineWarnings => ScriptEngineWarnings.Count > 0;

    /// <summary>Gets the command palette.</summary>
    /// <exception cref="InvalidOperationException">Read before <see cref="AttachPalette"/>.</exception>
    public CommandPaletteViewModel Palette =>
        _palette ?? throw new InvalidOperationException("The palette has not been attached.");

    /// <summary>Attaches the palette; called once by the composition root, because the palette sources need the shell.</summary>
    /// <param name="palette">The palette to attach.</param>
    /// <exception cref="InvalidOperationException">A palette is already attached.</exception>
    public void AttachPalette(CommandPaletteViewModel palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (_palette is not null)
        {
            throw new InvalidOperationException("The palette is already attached.");
        }

        _palette = palette;
    }

    /// <summary>Raised when the user asks to manage environments; the views open their environments dialog.</summary>
    public event EventHandler? EnvironmentsRequested;

    /// <summary>Asks the views to open the environments dialog.</summary>
    public void RequestEnvironments() => EnvironmentsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Loads the stores and restores the saved session; opens one new tab when none is restored.</summary>
    /// <returns>A task that completes when the shell is ready.</returns>
    public async Task InitializeAsync()
    {
        ScriptEngineWarnings =
        [
            .. Enum.GetValues<ScriptLanguage>()
                .Select(l => _engines.IsAvailable(l, out var reason) ? null : Strings.ShellEngineUnavailable(l, reason))
                .OfType<string>(),
        ];
        _vaultInitializing = true;
        try
        {
            await Vault.InitializeAsync();
        }
        finally
        {
            _vaultInitializing = false;
        }

        var state = await _session.LoadAsync(CancellationToken.None);
        Layout.IsHorizontal = state.Layout == PaneLayout.Horizontal;
        await Environments.LoadAsync(state.ActiveEnvironmentId);
        await Globals.LoadAsync();
        await Requests.RefreshAsync();
        await Collections.RefreshAsync();
        await History.RefreshAsync();

        foreach (var id in state.OpenRequestIds)
        {
            var item = Requests.Items.FirstOrDefault(i => i.Id == id);
            if (item is not null)
            {
                Requests.OpenCommand.Execute(item);
            }
        }

        Tabs.ActiveTab = Tabs.Tabs.FirstOrDefault(t => t.Id == state.ActiveRequestId) ?? Tabs.Tabs.FirstOrDefault();
        if (Tabs.ActiveTab is null)
        {
            Tabs.NewTab();
        }

        _initialized = true;

        // Never awaited: the check must not delay start-up, and it cannot throw.
        _ = Updates.InitializeAsync();
    }

    /// <summary>Asks about unsaved tabs before the window closes; tabs stay open so the session keeps them.</summary>
    /// <returns><see langword="true"/> when the window may close.</returns>
    public async Task<bool> RequestCloseAsync()
    {
        foreach (var tab in Tabs.Tabs.ToList())
        {
            if (!tab.IsDirty || TabsViewModel.IsUntouched(tab))
            {
                continue;
            }

            Tabs.ActiveTab = tab;
            switch (await _dialogs.ConfirmUnsavedAsync(tab.ToDefinition().Name))
            {
                case UnsavedChoice.Save:
                    if (!await Tabs.SaveAsync(tab))
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

        QueueSave();
        await _drain;
        Updates.Dispose();
        return true;
    }

    /// <summary>Reloads the environments, globals and collection tree after the vault state changed.</summary>
    /// <returns>A task that completes when the reload is done.</returns>
    public async Task ReloadAsync()
    {
        if (_vaultInitializing)
        {
            return; // InitializeAsync loads everything right after the vault.
        }

        await Environments.LoadAsync(Environments.ActiveEnvironment?.Id);
        await Globals.LoadAsync();
        await Collections.RefreshAsync();
    }

    [RelayCommand]
    private void ToggleConsole() => IsConsoleOpen = !IsConsoleOpen;

    [RelayCommand]
    private void OpenConsole() => IsConsoleOpen = true;

    [RelayCommand]
    private void ClearConsole()
    {
        Console.ClearCommand.Execute(null);
        IsConsoleOpen = true;
    }

    [RelayCommand]
    private void OpenPalette() => Palette.OpenCommand.Execute(null);

    [RelayCommand]
    private void ShowCollections() => Mode = SidebarMode.Collections;

    [RelayCommand]
    private void ShowHistory() => Mode = SidebarMode.History;

    private void QueueSave()
    {
        if (!_initialized)
        {
            return;
        }

        _pending = new SessionState(
            [.. Tabs.Tabs.Where(t => !t.IsNew).Select(t => t.Id)],
            Tabs.ActiveTab is { IsNew: false } active ? active.Id : null,
            Environments.ActiveEnvironment?.Id,
            Layout.IsHorizontal ? PaneLayout.Horizontal : PaneLayout.Vertical);
        if (!_saving)
        {
            _drain = DrainAsync();
        }
    }

    // Serialized: one save at a time, the latest pending state wins.
    private async Task DrainAsync()
    {
        _saving = true;
        try
        {
            while (_pending is { } state)
            {
                _pending = null;
                try
                {
                    await _session.SaveAsync(state, CancellationToken.None);
                }
                catch (StorageException)
                {
                    // Session state is not user data: a failed save is ignored.
                }
            }
        }
        finally
        {
            _saving = false;
        }
    }
}
