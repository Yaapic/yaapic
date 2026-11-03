using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.ViewModels.Collections;

/// <summary>The sidebar tree of collections, folders and requests: load, expand, filter and open.</summary>
public sealed partial class CollectionTreeViewModel : ObservableObject
{
    private readonly ICollectionStore _collections;
    private readonly TabsViewModel _tabs;
    private readonly HashSet<Guid> _expanded = [];
    private IReadOnlyList<CollectionNode> _tree = [];
    private bool _applying;

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string? _lastError;

    /// <summary>Creates the tree.</summary>
    /// <param name="collections">Where the tree is loaded from.</param>
    /// <param name="requests">Where requests are saved; used by the request commands.</param>
    /// <param name="tabs">The open tabs; a save there refreshes this tree and requests open in it.</param>
    /// <param name="dialogs">Asks the user to confirm deletions; used by the folder and request commands.</param>
    public CollectionTreeViewModel(ICollectionStore collections, IRequestStore requests, TabsViewModel tabs, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(collections);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(dialogs);
        _collections = collections;
        Requests = requests;
        _tabs = tabs;
        Dialogs = dialogs;
        tabs.RequestSaved += (_, _) => _ = RefreshAsync();
    }

    /// <summary>Gets the nodes matching <see cref="FilterText"/>, in store order.</summary>
    public ObservableCollection<CollectionNodeViewModel> Roots { get; } = [];

    internal IRequestStore Requests { get; }

    internal IDialogService Dialogs { get; }

    /// <summary>Reloads the tree from the store, keeping the expansion of folders that still exist, and applies the filter.</summary>
    /// <returns>A task that completes when the tree is up to date.</returns>
    public async Task RefreshAsync()
    {
        try
        {
            _tree = await _collections.LoadTreeAsync(CancellationToken.None);
            LastError = null;
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        IsEmpty = _tree.Count == 0;
        _expanded.IntersectWith(CollectionNode.Flatten(_tree).Where(static n => n.Kind == CollectionNodeKind.Folder).Select(static n => n.Id));
        ApplyFilter();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void Open(CollectionNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.IsFolder)
            node.IsExpanded = !node.IsExpanded;
        else
            _tabs.Open(node.Node.Request!);
    }

    [RelayCommand]
    private async Task NewCollectionAsync()
    {
        if (await PromptNameAsync("New collection", "New Collection") is not { } name) return;
        await RunAsync(() => _collections.SaveFolderAsync(FolderDefinition.Create(name), null, CancellationToken.None));
    }

    [RelayCommand]
    private async Task NewFolderAsync(CollectionNodeViewModel parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (!parent.IsFolder || await PromptNameAsync("New folder", "New Folder") is not { } name) return;
        await RunAsync(() => _collections.SaveFolderAsync(FolderDefinition.Create(name), parent.Id, CancellationToken.None),
            () => _expanded.Add(parent.Id));
    }

    [RelayCommand]
    private async Task RenameFolderAsync(CollectionNodeViewModel folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (!folder.IsFolder || await PromptNameAsync("Rename", folder.Name) is not { } name || name == folder.Name) return;
        await RunAsync(() => _collections.SaveFolderAsync(folder.Node.Folder! with { Name = name }, null, CancellationToken.None));
    }

    [RelayCommand]
    private async Task DeleteFolderAsync(CollectionNodeViewModel folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (!folder.IsFolder) return;
        var requests = folder.Node.Descendants().Where(static n => n.Kind == CollectionNodeKind.Request).Select(static n => n.Id).ToList();
        var label = requests.Count == 0 ? folder.Name : $"{folder.Name} and its {requests.Count} requests";
        if (!await Dialogs.ConfirmDeleteAsync(label)) return;
        foreach (var id in requests)
            _tabs.CloseWithoutPrompt(id);
        await RunAsync(() => _collections.DeleteFolderAsync(folder.Id, CancellationToken.None));
    }

    // A blank answer counts as a cancel.
    private async Task<string?> PromptNameAsync(string title, string initial) =>
        (await Dialogs.PromptTextAsync(title, "Name", initial))?.Trim() is { Length: > 0 } name ? name : null;

    private async Task RunAsync(Func<Task> action, Action? onSuccess = null)
    {
        try
        {
            await action();
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        onSuccess?.Invoke();
        await RefreshAsync();
    }

    private void ApplyFilter()
    {
        var filter = FilterText.Trim();
        _applying = true;
        try
        {
            Roots.Clear();
            foreach (var node in _tree)
                if (Build(node, null, filter, false) is { } vm)
                    Roots.Add(vm);
        }
        finally
        {
            _applying = false;
        }
    }

    private CollectionNodeViewModel? Build(CollectionNode node, CollectionNodeViewModel? parent, string filter, bool showAll)
    {
        var self = filter.Length == 0 || node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (node.Request is { } r && r.Url.Contains(filter, StringComparison.OrdinalIgnoreCase));
        var vm = new CollectionNodeViewModel(node, parent);
        var childrenShown = showAll || (self && node.Kind == CollectionNodeKind.Folder);
        foreach (var child in node.Children)
            if (Build(child, vm, filter, childrenShown) is { } c)
                vm.Children.Add(c);

        if (!showAll && !self && vm.Children.Count == 0)
            return null;

        vm.IsExpanded = node.Kind == CollectionNodeKind.Folder && (filter.Length == 0 ? _expanded.Contains(node.Id) : vm.Children.Count > 0);
        vm.ExpansionChanged = OnExpansionChanged;
        return vm;
    }

    private void OnExpansionChanged(CollectionNodeViewModel node)
    {
        if (_applying) return;
        if (node.IsExpanded) _expanded.Add(node.Id);
        else _expanded.Remove(node.Id);
    }
}
