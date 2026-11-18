using Yaapic.Core.CodeGeneration;
using Yaapic.Core.Http;
using Yaapic.Core.Preferences;
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
    private static readonly string[] Always = ["New request", "New collection", "Manage environments", "Show collections", "Show history", "Toggle request layout (side by side or stacked)", "Toggle console", "Clear console", "Decode JWT", "Client certificates...", "Set up the secret vault...", "Settings", "Change language", "Change accent colour"];

    private static readonly HttpResponseData Ok = new(200, "OK", [], [], TimeSpan.FromMilliseconds(5), "https://x.test");

    private readonly InMemoryHistoryStore _historyStore = new();
    private TaskCompletionSource<SendResult>? _gate;
    private CodeGenerationServices? _codeGeneration;

    private ShellViewModel CreateShell(ShellParts? parts = null)
    {
        parts ??= new ShellParts();
        var requests = new InMemoryRequestStore();
        var dialogs = new FakeDialogService();
        var sender = new RequestSender(new FakeHttpTransport((_, _) => _gate?.Task ?? throw new NotSupportedException()));
        var tabs = new TabsViewModel(
            requests,
            dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, new FakeVariableScopeProvider(), isNew, codeGeneration: _codeGeneration));
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

    private static readonly string[] M4OnOpenRequest = ["Generate code for this request"];
    private static readonly string[] M4OnResponse = ["Compare response with previous", "Pin response as baseline"];

    private ShellViewModel ShellWithRequest()
    {
        _codeGeneration = new CodeGenerationServices(new CodeGeneratorRegistry([]), new InMemoryPreferencesStore(), new FakeDialogService());
        var shell = CreateShell();
        shell.Tabs.NewTab();
        return shell;
    }

    [Fact]
    public void M4_entries_that_need_a_request_or_a_response_follow_availability()
    {
        var empty = CreateShell();
        Assert.DoesNotContain("Generate code for this request", Titles(empty));

        var shell = ShellWithRequest();
        Assert.Contains(M4OnOpenRequest[0], Titles(shell));
        Assert.DoesNotContain(M4OnResponse[0], Titles(shell));
        Assert.DoesNotContain(M4OnResponse[1], Titles(shell));

        shell.Tabs.ActiveTab!.Response.ShowReport(new SendReport(SendResult.Success(Ok), []));

        Assert.All(M4OnResponse, t => Assert.Contains(t, Titles(shell)));
    }

    [Fact]
    public async Task M4_entries_raise_their_event_or_call_their_service_once()
    {
        var shell = ShellWithRequest();
        var tab = shell.Tabs.ActiveTab!;
        tab.Response.ShowReport(new SendReport(SendResult.Success(Ok), []));
        var tokens = new FakeOAuth2TokenService();
        var source = new CommandsPaletteSource(shell, tokens);
        int code = 0, jwt = 0, certs = 0, diff = 0;
        tab.CodeGenerationRequested += (_, _) => code++;
        shell.JwtDecoderRequested += (_, _) => jwt++;
        shell.CertificatesRequested += (_, _) => certs++;
        tab.Response.ShowDiffRequested += (_, _) => diff++;

        await Find(source, "command:generate-code").Execute();
        await Find(source, "command:decode-jwt").Execute();
        await Find(source, "command:client-certificates").Execute();
        await Find(source, "command:compare-response").Execute();
        await Find(source, "command:pin-baseline").Execute();
        await Find(source, "command:clear-oauth-tokens").Execute();

        Assert.Equal([1, 1, 1, 1], [code, jwt, certs, diff]);
        Assert.True(tab.Response.Diff.HasBaseline);
        Assert.Equal(1, tokens.ClearAllCount);
        Assert.All(source.GetItems().Where(i => i.Id.StartsWith("command:decode", StringComparison.Ordinal)), i => Assert.Null(i.Shortcut));
    }

    [Fact]
    public void Clear_tokens_is_offered_only_with_a_token_service()
    {
        var shell = CreateShell();

        Assert.DoesNotContain("Clear OAuth 2.0 tokens", Titles(shell));
        Assert.Contains("Clear OAuth 2.0 tokens", new CommandsPaletteSource(shell, new FakeOAuth2TokenService()).GetItems().Select(i => i.Title));
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
