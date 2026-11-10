using Yaapic.Core.Http;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Workspace;
using Yaapic.TestDoubles;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.History;
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
    private readonly InMemoryCollectionStore _collections;

    private ShellParts _parts = new();

    public ShellViewModelTests() => _collections = new InMemoryCollectionStore(_requests);

    private ShellViewModel Create()
    {
        var sender = new RequestSender(new FakeHttpTransport((_, _) => throw new NotSupportedException()));
        var environments = new EnvironmentsViewModel(_environments, _dialogs);
        var tabs = new TabsViewModel(
            _requests,
            _dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, new FakeVariableScopeProvider(), isNew));
        return new ShellViewModel(
            tabs,
            new RequestListViewModel(_requests, tabs, _dialogs),
            new CollectionTreeViewModel(_collections, _requests, tabs, _dialogs),
            new HistoryViewModel(new InMemoryHistoryStore(), TimeProvider.System, tabs, _dialogs),
            environments, new GlobalsViewModel(new InMemoryGlobalsStore()), _session, _dialogs,
            _parts.Console, _parts.VaultViewModel, _parts.Engines);
    }

    [Fact]
    public void ModeSwitchesBetweenCollectionsAndHistory()
    {
        var shell = Create();
        Assert.Equal(SidebarMode.Collections, shell.Mode);

        shell.ShowHistoryCommand.Execute(null);
        Assert.Equal(SidebarMode.History, shell.Mode);

        shell.ShowCollectionsCommand.Execute(null);
        Assert.Equal(SidebarMode.Collections, shell.Mode);
    }

    private async Task<HttpRequestDefinition> SeedAsync(string name)
    {
        var request = HttpRequestDefinition.Create(name);
        await _requests.SaveAsync(request, CancellationToken.None);
        return request;
    }

    [Fact]
    public async Task InitializationRefreshesTheCollectionTree()
    {
        var folder = FolderDefinition.Create("api");
        await _collections.SaveFolderAsync(folder, null, CancellationToken.None);
        var request = await SeedAsync("nested");
        await _collections.MoveAsync(request.Id, folder.Id, CancellationToken.None);
        var shell = Create();

        await shell.InitializeAsync();

        var root = Assert.Single(shell.Collections.Roots);
        Assert.Equal(folder.Id, root.Id);
        Assert.Contains(root.Children, c => c.Id == request.Id);
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

    [Fact]
    public void ConsoleStartsClosedAndToggles()
    {
        var shell = Create();
        Assert.False(shell.IsConsoleOpen);

        shell.ToggleConsoleCommand.Execute(null);
        Assert.True(shell.IsConsoleOpen);

        shell.ToggleConsoleCommand.Execute(null);
        Assert.False(shell.IsConsoleOpen);

        shell.OpenConsoleCommand.Execute(null);
        Assert.True(shell.IsConsoleOpen);
    }

    [Fact]
    public void ClearConsoleEmptiesTheConsoleAndKeepsItOpen()
    {
        var shell = Create();
        shell.Console.AppendNetwork([new Yaapic.Domain.Scripting.ConsoleEntry(DateTimeOffset.UnixEpoch, Yaapic.Domain.Scripting.ConsoleLevel.Log, Yaapic.Domain.Scripting.ConsoleSource.Network, "GET /")]);
        Assert.False(shell.Console.IsEmpty);

        shell.ClearConsoleCommand.Execute(null);

        Assert.True(shell.Console.IsEmpty);
        Assert.True(shell.IsConsoleOpen);
    }

    [Theory]
    [InlineData(10, 120)]
    [InlineData(300, 300)]
    [InlineData(5000, 600)]
    public void ConsoleHeightIsClamped(double value, double expected)
    {
        var shell = Create();
        Assert.Equal(250, shell.ConsoleHeight);

        shell.ConsoleHeight = value;

        Assert.Equal(expected, shell.ConsoleHeight);
    }

    [Fact]
    public async Task UnavailableEnginesAreReportedByInitialize()
    {
        var js = new FakeScriptEngine(_ => throw new NotSupportedException()) { UnavailableReason = "library missing" };
        var lua = new FakeScriptEngine(_ => throw new NotSupportedException()) { Language = Yaapic.Domain.Scripting.ScriptLanguage.Lua };
        var registry = new Yaapic.Core.Scripting.ScriptEngineRegistry();
        registry.Add(js);
        registry.Add(lua);
        _parts = new ShellParts(registry);
        var shell = Create();

        await shell.InitializeAsync();

        Assert.Equal(["JavaScript engine unavailable: library missing"], shell.ScriptEngineWarnings);
        Assert.True(shell.HasScriptEngineWarnings);
    }

    [Fact]
    public async Task AvailableEnginesLeaveNoWarning()
    {
        var shell = Create();
        _parts.Engines.Add(new FakeScriptEngine(_ => throw new NotSupportedException()));
        _parts.Engines.Add(new FakeScriptEngine(_ => throw new NotSupportedException()) { Language = Yaapic.Domain.Scripting.ScriptLanguage.Lua });

        await shell.InitializeAsync();

        Assert.Empty(shell.ScriptEngineWarnings);
        Assert.False(shell.HasScriptEngineWarnings);
    }

    [Fact]
    public async Task InitializeReadsTheVaultBeforeTheEnvironmentsLoad()
    {
        var shell = Create();

        await shell.InitializeAsync();

        Assert.Equal(["InitializeAsync"], _parts.Vault.Calls);
    }
}
