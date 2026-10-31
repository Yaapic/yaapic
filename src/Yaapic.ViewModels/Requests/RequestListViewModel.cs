using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Storage;
using Yaapic.Domain.Http;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.ViewModels.Requests;

/// <summary>The sidebar list of saved requests: filter, open, delete and create.</summary>
public sealed partial class RequestListViewModel : ObservableObject
{
    private readonly IRequestStore _store;
    private readonly TabsViewModel _tabs;
    private readonly IDialogService _dialogs;
    private IReadOnlyList<HttpRequestDefinition> _all = [];

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private string? _lastError;

    /// <summary>Creates the list.</summary>
    /// <param name="store">Where requests are saved.</param>
    /// <param name="tabs">The open tabs; a save there refreshes this list.</param>
    /// <param name="dialogs">Asks the user to confirm deletions.</param>
    public RequestListViewModel(IRequestStore store, TabsViewModel tabs, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(dialogs);
        _store = store;
        _tabs = tabs;
        _dialogs = dialogs;
        tabs.RequestSaved += (_, _) => _ = RefreshAsync();
    }

    /// <summary>Gets the requests matching <see cref="FilterText"/>, in store order.</summary>
    public ObservableCollection<RequestListItem> Items { get; } = [];

    /// <summary>Reloads the saved requests from the store and applies the filter.</summary>
    /// <returns>A task that completes when the list is up to date.</returns>
    public async Task RefreshAsync()
    {
        try
        {
            _all = await _store.ListAsync(CancellationToken.None);
            LastError = null;
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        ApplyFilter();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void Open(RequestListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var definition = _all.FirstOrDefault(r => r.Id == item.Id);
        if (definition is not null)
        {
            _tabs.Open(definition);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(RequestListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!await _dialogs.ConfirmDeleteAsync(item.Name))
        {
            return;
        }

        try
        {
            await _store.DeleteAsync(item.Id, CancellationToken.None);
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        _tabs.CloseWithoutPrompt(item.Id);
        await RefreshAsync();
    }

    [RelayCommand]
    private void NewRequest() => _tabs.NewTab();

    private void ApplyFilter()
    {
        var filter = FilterText.Trim();
        Items.Clear();
        foreach (var r in _all)
        {
            if (filter.Length == 0
                || r.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || r.Url.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                Items.Add(new RequestListItem(r.Id, r.Name, r.Method, r.Url));
            }
        }
    }
}
