using Yaapic.Core.Http;
using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.ViewModels.Tests;

public sealed class CollectionTreeViewModelTests
{
    private sealed class NoEnvironment : IActiveEnvironmentProvider
    {
        public EnvironmentDefinition? Current => null;
    }

    private sealed class FailingStore : ICollectionStore
    {
        public Task<IReadOnlyList<CollectionNode>> LoadTreeAsync(CancellationToken ct) => throw new StorageException("boom");

        public Task SaveFolderAsync(FolderDefinition folder, Guid? parentId, CancellationToken ct) => throw new NotSupportedException();

        public Task DeleteFolderAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();

        public Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct) => throw new NotSupportedException();
    }

    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryCollectionStore _store;
    private readonly FakeDialogService _dialogs = new();
    private readonly TabsViewModel _tabs;
    private readonly CollectionTreeViewModel _tree;

    public CollectionTreeViewModelTests()
    {
        _store = new InMemoryCollectionStore(_requests);
        var sender = new RequestSender(new FakeHttpTransport((_, _) => throw new NotSupportedException()));
        _tabs = new TabsViewModel(
            _requests,
            _dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, new NoEnvironment(), isNew));
        _tree = new CollectionTreeViewModel(_store, _requests, _tabs, _dialogs);
    }

    private async Task<FolderDefinition> FolderAsync(string name, Guid? parent = null)
    {
        var folder = FolderDefinition.Create(name);
        await _store.SaveFolderAsync(folder, parent, CancellationToken.None);
        return folder;
    }

    private async Task<HttpRequestDefinition> RequestAsync(string name, string url = "", Guid? parent = null)
    {
        var request = HttpRequestDefinition.Create(name) with { Url = url };
        await _requests.SaveAsync(request, CancellationToken.None);
        if (parent is not null) await _store.MoveAsync(request.Id, parent, CancellationToken.None);
        return request;
    }

    [Fact]
    public async Task RefreshBuildsTheTreeInStoreOrderWithParents()
    {
        var f = await FolderAsync("F");
        var sub = await FolderAsync("Sub", f.Id);
        await RequestAsync("b", parent: f.Id);
        await RequestAsync("a");

        await _tree.RefreshAsync();

        Assert.Equal(["F", "a"], _tree.Roots.Select(n => n.Name));
        var root = _tree.Roots[0];
        Assert.Equal(["Sub", "b"], root.Children.Select(n => n.Name));
        Assert.All(root.Children, c => Assert.Same(root, c.Parent));
        Assert.Equal(sub.Id, root.Children[0].Id);
        Assert.Equal("GET", _tree.Roots[1].MethodText);
        Assert.False(_tree.IsEmpty);
    }

    [Fact]
    public async Task OnlyTopLevelFoldersAreCollections()
    {
        var f = await FolderAsync("F");
        await FolderAsync("Sub", f.Id);
        await RequestAsync("a");

        await _tree.RefreshAsync();

        Assert.True(_tree.Roots[0].IsCollection);
        Assert.False(_tree.Roots[0].Children[0].IsCollection);
        Assert.False(_tree.Roots[1].IsCollection);
        Assert.False(_tree.Roots[1].IsFolder);
    }

    [Fact]
    public async Task EmptyStoreIsEmpty()
    {
        await _tree.RefreshAsync();

        Assert.True(_tree.IsEmpty);
        Assert.Empty(_tree.Roots);
    }

    [Fact]
    public async Task ExpansionSurvivesRefreshAndRemovedFoldersAreForgotten()
    {
        var f = await FolderAsync("F");
        var g = await FolderAsync("G");
        await _tree.RefreshAsync();
        _tree.Roots[0].IsExpanded = true;
        _tree.Roots[1].IsExpanded = true;

        await _tree.RefreshAsync();
        Assert.All(_tree.Roots, n => Assert.True(n.IsExpanded));

        await _store.DeleteFolderAsync(g.Id, CancellationToken.None);
        await _tree.RefreshAsync();
        await _store.SaveFolderAsync(g, null, CancellationToken.None);
        await _tree.RefreshAsync();

        Assert.True(_tree.Roots.Single(n => n.Id == f.Id).IsExpanded);
        Assert.False(_tree.Roots.Single(n => n.Id == g.Id).IsExpanded);
    }

    [Fact]
    public async Task FilterMatchesRequestNameUrlAndFolderNameIgnoringCase()
    {
        var f = await FolderAsync("Billing");
        await RequestAsync("inside", parent: f.Id);
        await RequestAsync("Users", "https://x/one");
        await RequestAsync("Orders", "https://x/USERS-api");
        await RequestAsync("Other", "https://x/other");
        await _tree.RefreshAsync();

        _tree.FilterText = "  users ";
        Assert.Equal(["Orders", "Users"], _tree.Roots.Select(n => n.Name));

        _tree.FilterText = "BILL";
        Assert.Equal(["Billing"], _tree.Roots.Select(n => n.Name));
        Assert.Equal(["inside"], _tree.Roots[0].Children.Select(n => n.Name));
    }

    [Fact]
    public async Task NestedMatchShowsAncestorsExpandedWithoutTouchingRememberedExpansion()
    {
        var f = await FolderAsync("F");
        var sub = await FolderAsync("Sub", f.Id);
        await RequestAsync("target", parent: sub.Id);
        await RequestAsync("noise", parent: sub.Id);
        await _tree.RefreshAsync();

        _tree.FilterText = "target";

        var root = _tree.Roots.Single();
        Assert.True(root.IsExpanded);
        Assert.True(root.Children[0].IsExpanded);
        Assert.Equal(["target"], root.Children[0].Children.Select(n => n.Name));

        _tree.FilterText = "";

        Assert.False(_tree.Roots[0].IsExpanded);
        Assert.Equal(2, _tree.Roots[0].Children[0].Children.Count);
    }

    [Fact]
    public async Task SavingATabRefreshesTheTree()
    {
        _tabs.NewTab();
        await _tabs.SaveAsync(_tabs.ActiveTab!);

        Assert.Single(_tree.Roots);
    }

    [Fact]
    public async Task OpeningARequestActivatesItsTabAndOpeningAFolderToggles()
    {
        var f = await FolderAsync("F");
        var saved = await RequestAsync("a");
        await _tree.RefreshAsync();

        _tree.OpenCommand.Execute(_tree.Roots.Single(n => n.Id == saved.Id));
        Assert.Equal(saved.Id, _tabs.ActiveTab!.Id);

        var folder = _tree.Roots.Single(n => n.Id == f.Id);
        _tree.OpenCommand.Execute(folder);
        Assert.True(folder.IsExpanded);
        _tree.OpenCommand.Execute(folder);
        Assert.False(folder.IsExpanded);
    }

    [Fact]
    public async Task FailingStoreSetsLastErrorAndKeepsTheTree()
    {
        await FolderAsync("F");
        await _tree.RefreshAsync();
        var failing = new CollectionTreeViewModel(new FailingStore(), _requests, _tabs, _dialogs);

        await failing.RefreshAsync();

        Assert.Equal("boom", failing.LastError);
        Assert.Single(_tree.Roots);
    }
}
