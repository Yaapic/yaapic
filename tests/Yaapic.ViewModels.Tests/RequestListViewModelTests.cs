using Yaapic.Core.Http;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.ViewModels.Tests;

public sealed class RequestListViewModelTests
{
    private sealed class NoEnvironment : IActiveEnvironmentProvider
    {
        public EnvironmentDefinition? Current => null;
    }

    private readonly InMemoryRequestStore _store = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly TabsViewModel _tabs;
    private readonly RequestListViewModel _list;

    public RequestListViewModelTests()
    {
        var sender = new RequestSender(new FakeHttpTransport((_, _) => throw new NotSupportedException()));
        _tabs = new TabsViewModel(
            _store,
            _dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, new NoEnvironment(), isNew));
        _list = new RequestListViewModel(_store, _tabs, _dialogs);
    }

    private async Task<HttpRequestDefinition> SeedAsync(string name, string url = "")
    {
        var request = HttpRequestDefinition.Create(name) with { Url = url };
        await _store.SaveAsync(request, CancellationToken.None);
        return request;
    }

    [Fact]
    public async Task RefreshListsStoreContent()
    {
        await SeedAsync("b");
        await SeedAsync("a");

        await _list.RefreshAsync();

        Assert.Equal(["a", "b"], _list.Items.Select(i => i.Name));
        Assert.All(_list.Items, i => Assert.Equal("GET", i.MethodText));
    }

    [Fact]
    public async Task FilterMatchesNameAndUrlIgnoringCase()
    {
        await SeedAsync("Users", "https://x/one");
        await SeedAsync("Orders", "https://x/USERS-api");
        await SeedAsync("Other", "https://x/other");
        await _list.RefreshAsync();

        _list.FilterText = "  users ";

        Assert.Equal(["Orders", "Users"], _list.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task ClearingTheFilterShowsEverything()
    {
        await SeedAsync("a");
        await SeedAsync("b");
        await _list.RefreshAsync();
        _list.FilterText = "a";
        Assert.Single(_list.Items);

        _list.FilterText = "";

        Assert.Equal(2, _list.Items.Count);
    }

    [Fact]
    public async Task SavingATabRefreshesTheList()
    {
        _tabs.NewTab();
        await _tabs.SaveAsync(_tabs.ActiveTab!);

        Assert.Single(_list.Items);
    }

    [Fact]
    public async Task OpenActivatesATab()
    {
        var saved = await SeedAsync("a");
        await _list.RefreshAsync();

        _list.OpenCommand.Execute(_list.Items[0]);

        Assert.Equal(saved.Id, _tabs.ActiveTab!.Id);
    }

    [Fact]
    public async Task ConfirmedDeleteRemovesFromStoreAndClosesTab()
    {
        await SeedAsync("a");
        await _list.RefreshAsync();
        _list.OpenCommand.Execute(_list.Items[0]);
        _dialogs.DeleteAnswer = true;

        await _list.DeleteCommand.ExecuteAsync(_list.Items[0]);

        Assert.Equal(["a"], _dialogs.DeletePrompts);
        Assert.Empty(_list.Items);
        Assert.Empty(await _store.ListAsync(CancellationToken.None));
        Assert.Empty(_tabs.Tabs);
    }

    [Fact]
    public async Task RefusedDeleteChangesNothing()
    {
        await SeedAsync("a");
        await _list.RefreshAsync();
        _list.OpenCommand.Execute(_list.Items[0]);

        await _list.DeleteCommand.ExecuteAsync(_list.Items[0]);

        Assert.Single(_list.Items);
        Assert.Single(await _store.ListAsync(CancellationToken.None));
        Assert.Single(_tabs.Tabs);
    }

    [Fact]
    public void NewRequestOpensANewTab()
    {
        _list.NewRequestCommand.Execute(null);

        Assert.True(_tabs.ActiveTab!.IsNew);
    }
}
