using Yaapic.Core.Http;
using Yaapic.Core.Import;
using Yaapic.Core.LoadTesting;
using Yaapic.Core.Mocks;
using Yaapic.Core.Monitors;
using Yaapic.Core.Runner;
using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Mocks;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.History;
using Yaapic.ViewModels.LoadTesting;
using Yaapic.ViewModels.Mocks;
using Yaapic.ViewModels.Monitors;
using Yaapic.ViewModels.Palette;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Runner;
using Yaapic.ViewModels.Shell;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.ViewModels.Tests;

public sealed class AutomationShellTests : IAsyncDisposable
{
    private sealed class NoReader : IImportInputReader
    {
        public Task<ImportInput> ReadFileAsync(string path, CancellationToken ct) => throw new NotSupportedException();

        public Task<ImportInput> ReadFolderAsync(string path, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeScopes : IRunScopeSource
    {
        public VariableScopes GetScopes(Guid requestId) => new(null, [], []);

        public IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId) => [];

        public Task<IReadOnlyList<string>> PersistAsync(Guid requestId, IReadOnlyList<VariableChange> changes, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class Recorder(List<string> log, string name) : IDisposable, IAsyncDisposable
    {
        public void Dispose() => log.Add(name + ":sync");

        public ValueTask DisposeAsync()
        {
            log.Add(name + ":async");
            return ValueTask.CompletedTask;
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryCollectionStore _collections;
    private readonly InMemoryMockServerStore _mockStore = new();
    private readonly InMemoryEnvironmentStore _environments = new();
    private readonly InMemoryMonitorStore _monitorStore = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly ShellParts _parts = new();
    private readonly MonitorScheduler _scheduler;
    private readonly RunnerViewModel _runner;
    private readonly LoadTestViewModel _load;
    private readonly MockServersViewModel _mocks;
    private readonly MonitorsViewModel _monitors;
    private readonly ShellViewModel _shell;

    public AutomationShellTests()
    {
        _collections = new InMemoryCollectionStore(_requests);
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var transport = new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Success(new HttpResponseData(200, "OK", [], [], TimeSpan.Zero, "http://a/"))));
        ScriptEngineRegistry registry = new();
        registry.Add(new FakeScriptEngine(i => ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language)));
        var provider = new FakeVariableScopeProvider();
        var scopes = new VariableScopeRunSource(provider);
        var executor = new RunStepExecutor(new ScriptedRequestSender(new RequestSender(transport), registry, time), new FakeScriptChainProvider(), scopes);
        _runner = new RunnerViewModel(new RunnerSetupViewModel(new NoReader(), _dialogs), new CollectionRunner(executor, time), new ConsoleViewModel(), _dialogs);
        _load = new LoadTestViewModel(new RunnerSetupViewModel(new NoReader(), _dialogs), new LoadTestRunner(executor, scopes, time), provider, _dialogs);
        _mocks = new MockServersViewModel(new MockServerService(_mockStore, new FakeMockHttpHost(), time), _dialogs);
        var fake = new FakeScopes();
        _scheduler = new MonitorScheduler(_monitorStore, new InMemoryMonitorRunStore(), _collections, _environments, fake,
            new RunStepExecutor(new ScriptedRequestSender(new RequestSender(transport), registry, time), new FakeScriptChainProvider(), fake), time);
        _monitors = new MonitorsViewModel(_scheduler, _monitorStore, new InMemoryMonitorRunStore(), _collections, _environments, new ConsoleViewModel(), _dialogs, time);
        var sender = new RequestSender(transport);
        var tabs = new TabsViewModel(
            _requests,
            _dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, new FakeVariableScopeProvider(), isNew));
        _shell = new ShellViewModel(
            tabs,
            new RequestListViewModel(_requests, tabs, _dialogs),
            new CollectionTreeViewModel(_collections, _requests, tabs, _dialogs),
            new HistoryViewModel(new InMemoryHistoryStore(), TimeProvider.System, tabs, _dialogs),
            new EnvironmentsViewModel(_environments, _dialogs),
            new GlobalsViewModel(new InMemoryGlobalsStore()),
            new InMemorySessionStore(),
            _dialogs,
            _parts.Console, _parts.VaultViewModel, _parts.Engines, _parts.Updates);
    }

    public ValueTask DisposeAsync() => _scheduler.DisposeAsync();

    private void Attach() => _shell.AttachAutomation(_runner, _load, _mocks, _monitors);

    private string[] Slugs() => [.. new CommandsPaletteSource(_shell).GetItems().Select(i => i.Id)];

    private async Task<(FolderDefinition Top, CollectionNodeViewModel Root, HttpRequestDefinition Request)> TreeAsync()
    {
        var top = FolderDefinition.Create("Top");
        await _collections.SaveFolderAsync(top, null, Ct);
        await _collections.SaveFolderAsync(FolderDefinition.Create("Nested"), top.Id, Ct);
        var request = HttpRequestDefinition.Create("R") with { Url = "http://h/r" };
        await _requests.SaveAsync(request, Ct);
        await _collections.MoveAsync(request.Id, top.Id, Ct);
        await _shell.Collections.RefreshAsync();
        var root = _shell.Collections.Roots.Single();
        root.IsExpanded = true;
        return (top, root, request);
    }

    [Fact]
    public void Attaching_twice_throws()
    {
        Attach();

        Assert.Throws<InvalidOperationException>(Attach);
        Assert.Same(_runner, _shell.Runner);
        Assert.Same(_load, _shell.LoadTest);
        Assert.Same(_mocks, _shell.Mocks);
        Assert.Same(_monitors, _shell.Monitors);
    }

    [Fact]
    public async Task Without_the_tools_commands_are_disabled_events_silent_and_palette_entries_absent()
    {
        var (_, root, _) = await TreeAsync();
        var raised = 0;
        _shell.RunnerRequested += (_, _) => raised++;
        _shell.LoadTestRequested += (_, _) => raised++;
        _shell.MockServersRequested += (_, _) => raised++;
        _shell.MonitorsRequested += (_, _) => raised++;

        _shell.RequestRunner();
        _shell.RequestLoadTest();
        _shell.RequestMockServers();
        _shell.RequestMonitors();

        Assert.False(_shell.CanAutomate);
        Assert.Equal(0, raised);
        Assert.False(_shell.RunFolderCommand.CanExecute(root));
        Assert.False(_shell.LoadTestFolderCommand.CanExecute(root));
        Assert.False(_shell.MockFromFolderCommand.CanExecute(root));
        Assert.False(_shell.MonitorFolderCommand.CanExecute(root));
        Assert.DoesNotContain("command:run-collection", Slugs());
        Assert.DoesNotContain("command:mock-servers", Slugs());
        Assert.Equal("", _shell.MocksStatusText);
        Assert.Equal("", _shell.MonitorsStatusText);
    }

    [Fact]
    public async Task Folder_commands_open_or_create_and_raise_their_event_and_skip_requests()
    {
        Attach();
        var (top, root, _) = await TreeAsync();
        var nested = root.Children.Single(c => c.IsFolder);
        var request = root.Children.Single(c => !c.IsFolder);
        var raised = new List<string>();
        _shell.RunnerRequested += (_, _) => raised.Add("runner");
        _shell.LoadTestRequested += (_, _) => raised.Add("load");
        _shell.MockServersRequested += (_, _) => raised.Add("mocks");
        _shell.MonitorsRequested += (_, _) => raised.Add("monitors");

        _shell.RunFolderCommand.Execute(nested);
        _shell.LoadTestFolderCommand.Execute(root);
        await _shell.MockFromFolderCommand.ExecuteAsync(root);
        await _shell.MonitorFolderCommand.ExecuteAsync(root);

        Assert.Equal(["runner", "load", "mocks", "monitors"], raised);
        Assert.Single(await _mockStore.ListAsync(Ct));
        Assert.Equal(top.Id, (await _monitorStore.ListAsync(Ct)).Single().FolderId);
        Assert.False(_shell.RunFolderCommand.CanExecute(request));
        Assert.False(_shell.LoadTestFolderCommand.CanExecute(request));
        Assert.False(_shell.MockFromFolderCommand.CanExecute(request));
        Assert.False(_shell.MonitorFolderCommand.CanExecute(request));
        Assert.True(_shell.RunFolderCommand.CanExecute(nested));
    }

    [Fact]
    public async Task The_palette_lists_the_tools_and_opens_them_with_and_without_an_active_collection_request()
    {
        Attach();
        var (_, _, request) = await TreeAsync();
        var raised = new List<string>();
        _shell.RunnerRequested += (_, _) => raised.Add("runner");
        _shell.LoadTestRequested += (_, _) => raised.Add("load");
        _shell.MockServersRequested += (_, _) => raised.Add("mocks");
        _shell.MonitorsRequested += (_, _) => raised.Add("monitors");

        await RunAllAsync();
        Assert.Empty(_runner.Setup.Items);

        _shell.Tabs.Open(request);
        await RunAllAsync();

        Assert.Equal(["runner", "load", "mocks", "monitors", "runner", "load", "mocks", "monitors"], raised);
        Assert.Single(_runner.Setup.Items);
        Assert.Single(_load.Requests.Items);
        Assert.Equal(
            [Strings.PaletteRunCollection, Strings.PaletteLoadTest, Strings.PaletteMockServers, Strings.PaletteMonitors],
            new CommandsPaletteSource(_shell).GetItems().Where(i => i.Id is "command:run-collection" or "command:load-test" or "command:mock-servers" or "command:monitors").Select(i => i.Title));
        return;

        async Task RunAllAsync()
        {
            var items = new CommandsPaletteSource(_shell).GetItems();
            foreach (var slug in new[] { "run-collection", "load-test", "mock-servers", "monitors" })
            {
                await items.Single(i => i.Id == "command:" + slug).Execute();
            }
        }
    }

    [Fact]
    public async Task Status_texts_follow_the_tool_view_models()
    {
        Attach();
        var changed = new List<string?>();
        _shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Equal(Strings.RunnerStatusBar, _shell.RunnerStatusText);
        Assert.Equal("", _shell.MocksStatusText);

        await _mocks.LoadAsync();
        await _mocks.NewServerCommand.ExecuteAsync(null);
        await _mocks.StartCommand.ExecuteAsync(null);

        Assert.Equal(_mocks.RunningText, _shell.MocksStatusText);
        Assert.NotEqual("", _shell.MocksStatusText);
        Assert.Contains(nameof(ShellViewModel.MocksStatusText), changed);
    }

    [Fact]
    public async Task Owned_async_resources_are_disposed_on_close_after_the_sync_ones()
    {
        List<string> log = [];
        _shell.OwnAsync(new Recorder(log, "a"));
        _shell.Own(new Recorder(log, "s"));

        Assert.True(await _shell.RequestCloseAsync());

        Assert.Equal(["s:sync", "a:async"], log);
    }
}
