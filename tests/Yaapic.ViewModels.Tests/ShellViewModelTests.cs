using Yaapic.Core.Http;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Workspace;
using Yaapic.TestDoubles;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Shell;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.ViewModels.Tests;

public sealed class ShellViewModelTests
{
    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryEnvironmentStore _environments = new();
    private readonly InMemorySessionStore _session = new();
    private readonly FakeDialogService _dialogs = new();

    private ShellViewModel Create()
    {
        var sender = new RequestSender(new FakeHttpTransport((_, _) => throw new NotSupportedException()));
        var environments = new EnvironmentsViewModel(_environments, _dialogs);
        var tabs = new TabsViewModel(
            _requests,
            _dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, environments, isNew));
        return new ShellViewModel(tabs, new RequestListViewModel(_requests, tabs, _dialogs), environments, _session, _dialogs);
    }

    private async Task<HttpRequestDefinition> SeedAsync(string name)
    {
        var request = HttpRequestDefinition.Create(name);
        await _requests.SaveAsync(request, CancellationToken.None);
        return request;
    }

    [Fact]
    public async Task RestoresTabsInOrderDroppingMissingIdsAndActivatesTheSavedOne()
    {
        var a = await SeedAsync("a");
        var b = await SeedAsync("b");
        await _session.SaveAsync(new SessionState([b.Id, Guid.NewGuid(), a.Id], b.Id, null), CancellationToken.None);
        var saves = _session.SaveCount;
        var shell = Create();

        await shell.InitializeAsync();

        Assert.Equal([b.Id, a.Id], shell.Tabs.Tabs.Select(t => t.Id));
        Assert.Equal(b.Id, shell.Tabs.ActiveTab?.Id);
        Assert.Equal(saves, _session.SaveCount);
    }

    [Fact]
    public async Task ActivatesTheFirstTabWhenTheActiveOneIsNotOpen()
    {
        var a = await SeedAsync("a");
        await _session.SaveAsync(new SessionState([a.Id], Guid.NewGuid(), null), CancellationToken.None);
        var shell = Create();

        await shell.InitializeAsync();

        Assert.Equal(a.Id, shell.Tabs.ActiveTab?.Id);
    }

    [Fact]
    public async Task EmptySessionOpensOneNewTab()
    {
        var shell = Create();

        await shell.InitializeAsync();

        Assert.True(Assert.Single(shell.Tabs.Tabs).IsNew);
    }

    [Fact]
    public async Task RestoresTheActiveEnvironment()
    {
        var environment = EnvironmentDefinition.Create("dev");
        await _environments.SaveAsync(environment, CancellationToken.None);
        await _session.SaveAsync(new SessionState([], null, environment.Id), CancellationToken.None);
        var shell = Create();

        await shell.InitializeAsync();

        Assert.Equal(environment.Id, shell.Environments.ActiveEnvironment?.Id);
    }

    [Fact]
    public async Task SavesTheSessionOnOpenSwitchAndCloseExcludingNeverSavedTabs()
    {
        var a = await SeedAsync("a");
        var b = await SeedAsync("b");
        var shell = Create();
        await shell.InitializeAsync();

        shell.Tabs.Open(a);
        shell.Tabs.Open(b);
        shell.Tabs.Open(a);
        var state = await _session.LoadAsync(CancellationToken.None);
        Assert.Equal([a.Id, b.Id], state.OpenRequestIds);
        Assert.Equal(a.Id, state.ActiveRequestId);

        await shell.Tabs.CloseAsync(shell.Tabs.Tabs.Single(t => t.Id == a.Id));
        state = await _session.LoadAsync(CancellationToken.None);
        Assert.Equal([b.Id], state.OpenRequestIds);
        Assert.Equal(b.Id, state.ActiveRequestId);
    }

    [Fact]
    public async Task SavesTheSessionWhenTheActiveEnvironmentChanges()
    {
        var environment = EnvironmentDefinition.Create("dev");
        await _environments.SaveAsync(environment, CancellationToken.None);
        var shell = Create();
        await shell.InitializeAsync();
        await shell.Environments.LoadAsync(null);

        await shell.Environments.LoadAsync(environment.Id);

        Assert.Equal(environment.Id, (await _session.LoadAsync(CancellationToken.None)).ActiveEnvironmentId);
    }

    [Fact]
    public async Task RequestCloseWithCancelKeepsTheWindowOpen()
    {
        var shell = Create();
        await shell.InitializeAsync();
        shell.Tabs.ActiveTab!.Url = "https://example.test";
        _dialogs.UnsavedAnswer = UnsavedChoice.Cancel;

        Assert.False(await shell.RequestCloseAsync());
        Assert.Single(shell.Tabs.Tabs);
    }

    [Fact]
    public async Task RequestCloseSkipsUntouchedTabsAndSavesTheSession()
    {
        var a = await SeedAsync("a");
        var shell = Create();
        await shell.InitializeAsync();
        shell.Tabs.Open(a);
        var saves = _session.SaveCount;

        Assert.True(await shell.RequestCloseAsync());

        Assert.Empty(_dialogs.UnsavedPrompts);
        Assert.Equal(saves + 1, _session.SaveCount);
        Assert.Equal(2, shell.Tabs.Tabs.Count);
    }

    [Fact]
    public async Task RequestCloseWithSaveWritesTheRequest()
    {
        var shell = Create();
        await shell.InitializeAsync();
        var tab = shell.Tabs.ActiveTab!;
        tab.Url = "https://example.test";
        _dialogs.UnsavedAnswer = UnsavedChoice.Save;

        Assert.True(await shell.RequestCloseAsync());

        Assert.Contains(await _requests.ListAsync(CancellationToken.None), r => r.Id == tab.Id);
        Assert.Equal([tab.Id], (await _session.LoadAsync(CancellationToken.None)).OpenRequestIds);
    }
}
