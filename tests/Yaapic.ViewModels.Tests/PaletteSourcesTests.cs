using Yaapic.Core.Http;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.History;
using Yaapic.ViewModels.Palette;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Shell;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.ViewModels.Tests;

public sealed class PaletteSourcesTests
{
    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryEnvironmentStore _environmentStore = new();
    private readonly InMemoryCollectionStore _collectionStore;
    private readonly FakeDialogService _dialogs = new();
    private readonly TabsViewModel _tabs;
    private readonly CollectionTreeViewModel _collections;
    private readonly EnvironmentsViewModel _environments;
    private readonly ShellViewModel _shell;
    private readonly ShellParts _parts = new();

    public PaletteSourcesTests()
    {
        _collectionStore = new InMemoryCollectionStore(_requests);
        var sender = new RequestSender(new FakeHttpTransport((_, _) => throw new NotSupportedException()));
        _tabs = new TabsViewModel(
            _requests,
            _dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, new FakeVariableScopeProvider(), isNew));
        _collections = new CollectionTreeViewModel(_collectionStore, _requests, _tabs, _dialogs);
        _environments = new EnvironmentsViewModel(_environmentStore, _dialogs);
        _shell = new ShellViewModel(
            _tabs,
            new RequestListViewModel(_requests, _tabs, _dialogs),
            _collections,
            new HistoryViewModel(new InMemoryHistoryStore(), TimeProvider.System, _tabs, _dialogs),
            _environments,
            new GlobalsViewModel(new InMemoryGlobalsStore()),
            new InMemorySessionStore(),
            _dialogs,
            _parts.Console,
            _parts.VaultViewModel,
            _parts.Engines, _parts.Updates);
        _shell.AttachPalette(new CommandPaletteViewModel(
            [new CommandsPaletteSource(_shell), new RequestsPaletteSource(_collections), new EnvironmentsPaletteSource(_environments), new TabsPaletteSource(_tabs)]));
    }

    private async Task<(HttpRequestDefinition Nested, HttpRequestDefinition Loose)> SeedTreeAsync()
    {
        var folder = FolderDefinition.Create("Shop");
        await _collectionStore.SaveFolderAsync(folder, null, CancellationToken.None);
        var nested = HttpRequestDefinition.Create("List orders") with { Url = "https://api.test/orders" };
        var loose = HttpRequestDefinition.Create("Ping") with { Url = "https://api.test/ping" };
        await _requests.SaveAsync(nested, CancellationToken.None);
        await _requests.SaveAsync(loose, CancellationToken.None);
        await _collectionStore.MoveAsync(nested.Id, folder.Id, CancellationToken.None);
        await _collections.RefreshAsync();
        return (nested, loose);
    }

    [Fact]
    public async Task RequestsSourceListsNestedRequestsWithPathSubtitles()
    {
        var (nested, loose) = await SeedTreeAsync();

        var items = new RequestsPaletteSource(_collections).GetItems();

        var n = Assert.Single(items, i => i.Id == $"request:{nested.Id}");
        Assert.Equal("List orders", n.Title);
        Assert.Equal("GET  https://api.test/orders  ·  Shop", n.Subtitle);
        Assert.Equal("Request", n.Category);
        Assert.Equal("GET  https://api.test/ping", Assert.Single(items, i => i.Id == $"request:{loose.Id}").Subtitle);
    }

    [Fact]
    public async Task ExecutingARequestItemOpensItInATab()
    {
        var (nested, _) = await SeedTreeAsync();
        var item = new RequestsPaletteSource(_collections).GetItems().Single(i => i.Id == $"request:{nested.Id}");

        await item.Execute();

        Assert.Equal(nested.Id, _tabs.ActiveTab!.Id);
    }

    [Fact]
    public async Task OpenRequestWithAnUnknownIdIsANoOp()
    {
        await SeedTreeAsync();

        _collections.OpenRequest(Guid.NewGuid());

        Assert.Empty(_tabs.Tabs);
    }

    [Fact]
    public async Task EnvironmentsSourceSwitchesAndClears()
    {
        var dev = EnvironmentDefinition.Create("Dev");
        await _environmentStore.SaveAsync(dev, CancellationToken.None);
        await _environments.LoadAsync(null);
        var source = new EnvironmentsPaletteSource(_environments);

        var items = source.GetItems();
        Assert.Equal(["No environment", "Use environment: Dev"], items.Select(i => i.Title));
        await items[1].Execute();
        Assert.Equal(dev.Id, _environments.ActiveEnvironment?.Id);

        items = source.GetItems();
        Assert.Equal("Active", items[1].Subtitle);
        Assert.Null(items[0].Subtitle);
        await items[0].Execute();
        Assert.Null(_environments.ActiveEnvironment);
    }

    [Fact]
    public async Task TabsSourceActivatesTheChosenTab()
    {
        _tabs.NewTab();
        var first = _tabs.ActiveTab!;
        first.Url = "https://api.test/a";
        _tabs.NewTab();

        var items = new TabsPaletteSource(_tabs).GetItems();

        Assert.Equal(2, items.Count);
        Assert.Equal("Go to tab: New Request", items[0].Title);
        Assert.Equal("GET  https://api.test/a", items[0].Subtitle);
        await items[0].Execute();
        Assert.Same(first, _tabs.ActiveTab);
    }

    [Fact]
    public async Task ShellOpensThePaletteWithAQueryAndExecutesTheFirstResult()
    {
        _shell.OpenPaletteCommand.Execute(null);
        Assert.True(_shell.Palette.IsOpen);
        _shell.Palette.Query = "new coll";
        Assert.Equal("command:new-collection", _shell.Palette.Results[0].Item.Id);

        _shell.Palette.Query = "show hist";
        await _shell.Palette.ExecuteSelectedCommand.ExecuteAsync(null);

        Assert.Equal(SidebarMode.History, _shell.Mode);
        Assert.False(_shell.Palette.IsOpen);
    }

    [Fact]
    public void PaletteCannotBeReadBeforeOrAttachedTwice()
    {
        var shell = new ShellViewModel(
            _tabs, new RequestListViewModel(_requests, _tabs, _dialogs), _collections,
            new HistoryViewModel(new InMemoryHistoryStore(), TimeProvider.System, _tabs, _dialogs),
            _environments, new GlobalsViewModel(new InMemoryGlobalsStore()), new InMemorySessionStore(), _dialogs,
            _parts.Console, _parts.VaultViewModel, _parts.Engines, _parts.Updates);
        Assert.Throws<InvalidOperationException>(() => shell.Palette);

        shell.AttachPalette(new CommandPaletteViewModel([]));

        Assert.Throws<InvalidOperationException>(() => shell.AttachPalette(new CommandPaletteViewModel([])));
    }
}
