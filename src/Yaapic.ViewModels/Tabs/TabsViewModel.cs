using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Storage;
using Yaapic.Domain.Http;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tabs;

/// <summary>The open request tabs: opening, switching, saving and closing with an unsaved-changes prompt.</summary>
public sealed partial class TabsViewModel : ObservableObject
{
    private const string DefaultName = "New Request";

    private readonly IRequestStore _store;
    private readonly IDialogService _dialogs;
    private readonly Func<HttpRequestDefinition, bool, RequestEditorViewModel> _createEditor;

    [ObservableProperty]
    private RequestEditorViewModel? _activeTab;

    [ObservableProperty]
    private string? _lastError;

    /// <summary>Creates the tab list.</summary>
    /// <param name="store">Where requests are saved.</param>
    /// <param name="dialogs">Asks the user about unsaved changes.</param>
    /// <param name="createEditor">Builds an editor from a definition and whether it is new.</param>
    public TabsViewModel(
        IRequestStore store,
        IDialogService dialogs,
        Func<HttpRequestDefinition, bool, RequestEditorViewModel> createEditor)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(createEditor);
        _store = store;
        _dialogs = dialogs;
        _createEditor = createEditor;
        Tabs.CollectionChanged += (_, _) => SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised after a request has been written to the store.</summary>
    public event EventHandler<HttpRequestDefinition>? RequestSaved;

    /// <summary>Raised when tabs are added or removed, or the active tab changes.</summary>
    public event EventHandler? SessionChanged;

    /// <summary>Gets the open tabs, in display order.</summary>
    public ObservableCollection<RequestEditorViewModel> Tabs { get; } = [];

    /// <summary>Opens a new, empty request tab and activates it.</summary>
    public void NewTab()
    {
        var tab = _createEditor(HttpRequestDefinition.Create(DefaultName), true);
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    /// <summary>Activates the tab showing <paramref name="definition"/>, opening one when needed.</summary>
    /// <param name="definition">The saved request to show.</param>
    public void Open(HttpRequestDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var tab = Tabs.FirstOrDefault(t => t.Id == definition.Id);
        if (tab is null)
        {
            tab = _createEditor(definition, false);
            Tabs.Add(tab);
        }

        ActiveTab = tab;
    }

    /// <summary>Saves <paramref name="tab"/> to the store.</summary>
    /// <param name="tab">The tab to save.</param>
    /// <returns><see langword="true"/> when saved; <see langword="false"/> with <see cref="LastError"/> set otherwise.</returns>
    public async Task<bool> SaveAsync(RequestEditorViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        var definition = tab.ToDefinition();
        try
        {
            await _store.SaveAsync(definition, CancellationToken.None);
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return false;
        }

        LastError = null;
        tab.MarkSaved();
        RequestSaved?.Invoke(this, definition);
        return true;
    }

    /// <summary>Closes <paramref name="tab"/>, prompting first when it has unsaved changes.</summary>
    /// <param name="tab">The tab to close.</param>
    /// <returns><see langword="true"/> when the tab was closed.</returns>
    public async Task<bool> CloseAsync(RequestEditorViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (tab.IsDirty && !IsUntouched(tab))
        {
            switch (await _dialogs.ConfirmUnsavedAsync(tab.ToDefinition().Name))
            {
                case UnsavedChoice.Save:
                    if (!await SaveAsync(tab))
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

        Remove(tab);
        return true;
    }

    /// <summary>Closes the tab of a deleted request without asking.</summary>
    /// <param name="id">The request identifier.</param>
    public void CloseWithoutPrompt(Guid id)
    {
        var tab = Tabs.FirstOrDefault(t => t.Id == id);
        if (tab is not null)
        {
            Remove(tab);
        }
    }

    [RelayCommand]
    private Task SaveActiveAsync() => ActiveTab is { } tab ? SaveAsync(tab) : Task.CompletedTask;

    [RelayCommand]
    private Task CloseActiveAsync() => ActiveTab is { } tab ? CloseAsync(tab) : Task.CompletedTask;

    partial void OnActiveTabChanged(RequestEditorViewModel? value) => SessionChanged?.Invoke(this, EventArgs.Empty);

    private static bool IsUntouched(RequestEditorViewModel tab) =>
        tab.IsNew && tab.ToDefinition().IsEquivalentTo(
            new HttpRequestDefinition(tab.Id, DefaultName, HttpMethodKind.Get, "", [], [], RequestBody.None));

    private void Remove(RequestEditorViewModel tab)
    {
        var index = Tabs.IndexOf(tab);
        var wasActive = ActiveTab == tab;
        Tabs.RemoveAt(index);
        if (wasActive)
        {
            ActiveTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
        }
    }
}
