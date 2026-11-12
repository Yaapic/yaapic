using Yaapic.Core.Http;
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

public sealed class CommandsPaletteSourceTests
{
    private static readonly string[] Always = ["New request", "New collection", "Manage environments", "Show collections", "Show history", "Toggle console", "Clear console", "Set up the secret vault..."];

    private static readonly HttpResponseData Ok = new(200, "OK", [], [], TimeSpan.FromMilliseconds(5), "https://x.test");

    private readonly InMemoryHistoryStore _historyStore = new();
    private TaskCompletionSource<SendResult>? _gate;

    private ShellViewModel CreateShell(ShellParts? parts = null)
    {
        parts ??= new ShellParts();
        var requests = new InMemoryRequestStore();
        var dialogs = new FakeDialogService();
        var sender = new RequestSender(new FakeHttpTransport((_, _) => _gate?.Task ?? throw new NotSupportedException()));
        var tabs = new TabsViewModel(
            requests,
            dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, new FakeVariableScopeProvider(), isNew));
        return new ShellViewModel(
            tabs,
            new RequestListViewModel(requests, tabs, dialogs),
            new CollectionTreeViewModel(new InMemoryCollectionStore(requests), requests, tabs, dialogs),
            new HistoryViewModel(_historyStore, TimeProvider.System, tabs, dialogs),
            new EnvironmentsViewModel(new InMemoryEnvironmentStore(), dialogs),
            new GlobalsViewModel(new InMemoryGlobalsStore()),
            new InMemorySessionStore(),
            dialogs,
            parts.Console,
            parts.VaultViewModel,
            parts.Engines, parts.Updates);
    }

    private static string[] Titles(ShellViewModel shell) =>
        [.. new CommandsPaletteSource(shell).GetItems().Select(i => i.Title)];

    private static string[] Expected(params string[] extra) => [.. Always, .. extra];

    private static void AssertSameTitles(string[] expected, string[] actual) =>
        Assert.Equal([.. expected.Order()], [.. actual.Order()]);

    [Fact]
    public void EmptyShellListsOnlyTheAlwaysAvailableCommands() =>
        AssertSameTitles(Expected(), Titles(CreateShell()));

    [Fact]
    public void OneTabAddsSaveAndClose()
    {
        var shell = CreateShell();
        shell.Tabs.NewTab();

        AssertSameTitles(Expected("Save request", "Close tab"), Titles(shell));
    }

    [Fact]
    public void SendIsListedOnlyWhenTheTabCanExecute()
    {
        var shell = CreateShell();
        shell.Tabs.NewTab();
        shell.Tabs.ActiveTab!.Url = "https://api.test";

        AssertSameTitles(Expected("Save request", "Close tab", "Send request"), Titles(shell));
    }

    [Fact]
    public void TwoTabsAddNavigation()
    {
        var shell = CreateShell();
        shell.Tabs.NewTab();
        shell.Tabs.NewTab();

        AssertSameTitles(Expected("Save request", "Close tab", "Next tab", "Previous tab"), Titles(shell));
    }

    [Fact]
    public async Task SendingListsCancelInsteadOfSend()
    {
        _gate = new TaskCompletionSource<SendResult>();
        var shell = CreateShell();
        shell.Tabs.NewTab();
        var tab = shell.Tabs.ActiveTab!;
        tab.Url = "https://api.test";
        var send = tab.SendCommand.ExecuteAsync(null);

        AssertSameTitles(Expected("Save request", "Close tab", "Cancel request"), Titles(shell));

        _gate.SetResult(SendResult.Success(Ok));
        await send;
    }

    [Fact]
    public async Task HistoryEntriesAddClearHistory()
    {
        var shell = CreateShell();
        await shell.History.RecordAsync(
            HttpRequestDefinition.Create("r"),
            new SendReport(SendResult.Success(Ok), []));

        Assert.Contains("Clear history", Titles(shell));
    }

    [Fact]
    public async Task ExecutingCommandsRunsTheirActions()
    {
        var shell = CreateShell();
        var raised = false;
        shell.EnvironmentsRequested += (_, _) => raised = true;
        var source = new CommandsPaletteSource(shell);

        await Find(source, "command:new-request").Execute();
        await Find(source, "command:show-history").Execute();
        await Find(source, "command:manage-environments").Execute();

        Assert.Single(shell.Tabs.Tabs);
        Assert.Equal(SidebarMode.History, shell.Mode);
        Assert.True(raised);
    }

    private static PaletteItem Find(CommandsPaletteSource source, string id) => source.GetItems().Single(i => i.Id == id);

    [Fact]
    public void NextAndPreviousWrapAroundAndIgnoreFewerThanTwoTabs()
    {
        var shell = CreateShell();
        shell.Tabs.NextTabCommand.Execute(null);
        Assert.Null(shell.Tabs.ActiveTab);

        shell.Tabs.NewTab();
        var first = shell.Tabs.ActiveTab;
        shell.Tabs.PreviousTabCommand.Execute(null);
        Assert.Same(first, shell.Tabs.ActiveTab);

        shell.Tabs.NewTab();
        var second = shell.Tabs.ActiveTab;
        shell.Tabs.NextTabCommand.Execute(null);
        Assert.Same(first, shell.Tabs.ActiveTab);
        shell.Tabs.PreviousTabCommand.Execute(null);
        Assert.Same(second, shell.Tabs.ActiveTab);
    }

    [Theory]
    [InlineData(Yaapic.Core.Secrets.VaultState.NotCreated, "Set up the secret vault...")]
    [InlineData(Yaapic.Core.Secrets.VaultState.Locked, "Unlock the vault...")]
    [InlineData(Yaapic.Core.Secrets.VaultState.Unlocked, "Lock the vault")]
    public async Task ExactlyOneVaultCommandMatchesTheVaultState(Yaapic.Core.Secrets.VaultState state, string title)
    {
        var parts = new ShellParts();
        parts.Vault.Arrange(state);
        await parts.VaultViewModel.InitializeAsync();
        var shell = CreateShell(parts);

        var vaultTitles = Titles(shell).Where(t => t.Contains("vault", StringComparison.Ordinal)).ToArray();

        Assert.Equal([title], vaultTitles);
    }

    [Fact]
    public async Task ConsoleAndVaultCommandsRunTheirActions()
    {
        var parts = new ShellParts();
        var shell = CreateShell(parts);
        var setup = false;
        parts.VaultViewModel.SetupRequested += (_, _) => setup = true;
        var source = new CommandsPaletteSource(shell);

        await Find(source, "command:toggle-console").Execute();
        Assert.True(shell.IsConsoleOpen);
        await Find(source, "command:clear-console").Execute();
        Assert.True(shell.IsConsoleOpen);
        await Find(source, "command:vault-setup").Execute();
        Assert.True(setup);
        Assert.Equal("Ctrl+`", Find(source, "command:toggle-console").Shortcut);
    }

    [Fact]
    public void Update_commands_follow_support_and_state()
    {
        var parts = new ShellParts();
        var shell = CreateShell(parts);
        Assert.DoesNotContain("Check for updates", Titles(shell));

        var supported = new ShellParts();
        supported.UpdateService.IsSupported = true;
        var withUpdates = CreateShell(supported);

        Assert.Contains("Check for updates", Titles(withUpdates));
        Assert.DoesNotContain("Restart to update", Titles(withUpdates));
    }
}
