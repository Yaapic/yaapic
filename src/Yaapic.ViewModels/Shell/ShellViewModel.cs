using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Core.Storage;
using Yaapic.Domain.Workspace;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.ViewModels.Shell;

/// <summary>The root view model of the application window: owns the sidebar, tabs and environments and restores the session.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly ISessionStore _session;
    private readonly IDialogService _dialogs;
    private bool _initialized;
    private bool _saving;
    private SessionState? _pending;
    private Task _drain = Task.CompletedTask;

    /// <summary>Creates the shell.</summary>
    /// <param name="tabs">The open tabs.</param>
    /// <param name="requests">The sidebar request list.</param>
    /// <param name="environments">The environment selection and editing.</param>
    /// <param name="session">Where the session is saved.</param>
    /// <param name="dialogs">Asks the user about unsaved changes when the window closes.</param>
    public ShellViewModel(
        TabsViewModel tabs,
        RequestListViewModel requests,
        EnvironmentsViewModel environments,
        ISessionStore session,
        IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dialogs);
        Tabs = tabs;
        Requests = requests;
        Environments = environments;
        _session = session;
        _dialogs = dialogs;
        tabs.SessionChanged += (_, _) => QueueSave();
        tabs.RequestSaved += (_, _) => QueueSave();
        environments.ActiveEnvironmentChanged += (_, _) => QueueSave();
    }

    /// <summary>Gets the open tabs.</summary>
    public TabsViewModel Tabs { get; }

    /// <summary>Gets the sidebar request list.</summary>
    public RequestListViewModel Requests { get; }

    /// <summary>Gets the environments view model.</summary>
    public EnvironmentsViewModel Environments { get; }

    /// <summary>Loads the stores and restores the saved session; opens one new tab when none is restored.</summary>
    /// <returns>A task that completes when the shell is ready.</returns>
    public async Task InitializeAsync()
    {
        var state = await _session.LoadAsync(CancellationToken.None);
        await Environments.LoadAsync(state.ActiveEnvironmentId);
        await Requests.RefreshAsync();

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
        return true;
    }

    private void QueueSave()
    {
        if (!_initialized)
        {
            return;
        }

        _pending = new SessionState(
            [.. Tabs.Tabs.Where(t => !t.IsNew).Select(t => t.Id)],
            Tabs.ActiveTab is { IsNew: false } active ? active.Id : null,
            Environments.ActiveEnvironment?.Id);
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
