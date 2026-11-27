using Yaapic.Core.Http;
using Yaapic.Core.Monitors;
using Yaapic.Core.Runner;
using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Monitors;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Monitors;

namespace Yaapic.ViewModels.Tests.Monitors;

public sealed class MonitorsViewModelTests : IAsyncDisposable
{
    private sealed class FakeScopes : IRunScopeSource
    {
        public VariableScopes GetScopes(Guid requestId) => new(null, [], []);

        public IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId) => [];

        public Task<IReadOnlyList<string>> PersistAsync(Guid requestId, IReadOnlyList<VariableChange> changes, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private readonly InMemoryMonitorStore _monitors = new();
    private readonly InMemoryMonitorRunStore _runs = new();
    private readonly InMemoryEnvironmentStore _environments = new();
    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryCollectionStore _collections;
    private readonly FakeDialogService _dialogs = new();
    private readonly ConsoleViewModel _console = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly MonitorScheduler _scheduler;
    private readonly MonitorsViewModel _vm;
    private bool _failing;

    public MonitorsViewModelTests()
    {
        _collections = new InMemoryCollectionStore(_requests);
        var transport = new FakeHttpTransport((_, _) => Task.FromResult(_failing
            ? SendResult.Fail(SendFailureKind.ConnectionFailed, "boom")
            : SendResult.Success(new HttpResponseData(200, "OK", [], [], TimeSpan.Zero, "http://a/"))));
        ScriptEngineRegistry registry = new();
        registry.Add(new FakeScriptEngine(i => ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language)));
        var scopes = new FakeScopes();
        var executor = new RunStepExecutor(new ScriptedRequestSender(new RequestSender(transport), registry, _time), new FakeScriptChainProvider(), scopes);
        _scheduler = new MonitorScheduler(_monitors, _runs, _collections, _environments, scopes, executor, _time);
        _vm = new MonitorsViewModel(_scheduler, _monitors, _runs, _collections, _environments, _console, _dialogs, _time);
    }

    public ValueTask DisposeAsync() => _scheduler.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<FolderDefinition> FolderAsync(string name, Guid? parent = null, bool withRequest = true)
    {
        var folder = FolderDefinition.Create(name);
        await _collections.SaveFolderAsync(folder, parent, Ct);
        if (withRequest)
        {
            var request = HttpRequestDefinition.Create(name + " request") with { Url = "http://h/x" };
            await _requests.SaveAsync(request, Ct);
            await _collections.MoveAsync(request.Id, folder.Id, Ct);
        }

        return folder;
    }

    private async Task<MonitorDefinition> MonitorAsync(string name, Guid folderId, Func<MonitorDefinition, MonitorDefinition>? edit = null)
    {
        var monitor = MonitorDefinition.Create(name, folderId);
        monitor = edit?.Invoke(monitor) ?? monitor;
        await _monitors.SaveAsync(monitor, Ct);
        return monitor;
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(5, timeout.Token);
        }
    }

    private async Task RunNowAndWaitAsync(int historyCount)
    {
        _vm.RunNowCommand.Execute(null);
        await WaitAsync(() => _vm.History.Count >= historyCount);
    }

    [Fact]
    public async Task Load_lists_monitors_with_folder_and_environment_names_and_selects_the_first()
    {
        var folder = await FolderAsync("api");
        var environment = new EnvironmentDefinition(Guid.NewGuid(), "prod", []);
        await _environments.SaveAsync(environment, Ct);
        await MonitorAsync("a", folder.Id, m => m with { EnvironmentId = environment.Id, IntervalMinutes = 5 });
        await MonitorAsync("b", Guid.NewGuid());

        await _vm.LoadAsync();

        Assert.Equal(["a", "b"], _vm.Monitors.Select(m => m.Name));
        Assert.Equal("a", _vm.SelectedMonitor?.Name);
        var a = _vm.Monitors[0];
        Assert.Equal("api", a.FolderName);
        Assert.Equal("prod", a.EnvironmentName);
        Assert.Equal(Strings.MonitorEvery("5 min"), a.IntervalText);
        Assert.Equal(Strings.MonitorNever, a.LastRunText);
        Assert.Null(a.LastStatus);
        Assert.False(a.IsFailing);
        Assert.NotEqual("", a.NextRunText);
        var b = _vm.Monitors[1];
        Assert.Equal(Strings.MonitorFolderMissing, b.FolderName);
        Assert.Equal(Strings.MonitorNoEnvironment, b.EnvironmentName);
        Assert.Equal(Strings.MonitorEvery("1 h"), b.IntervalText);
        Assert.Equal(0, _vm.FailingCount);
        Assert.Equal("", _vm.FailingText);
    }

    [Fact]
    public void Interval_choices_use_the_short_spellings()
    {
        Assert.Equal(["5 min", "10 min", "15 min", "30 min", "1 h", "3 h", "6 h", "12 h", "24 h"], _vm.IntervalChoices.Select(c => c.Text));
    }

    [Fact]
    public async Task Folder_choices_list_top_level_folders_first_with_their_path()
    {
        var b = await FolderAsync("b", withRequest: false);
        var a = await FolderAsync("a", withRequest: false);
        await FolderAsync("z", a.Id, withRequest: false);
        await _vm.LoadAsync();

        Assert.Equal(["a", "b", "a/z"], _vm.FolderChoices.Select(c => c.Path));
        Assert.Equal(b.Id, _vm.FolderChoices[1].Id);
        Assert.Equal(Strings.MonitorNoEnvironment, _vm.EnvironmentChoices[0].Name);
        Assert.Null(_vm.EnvironmentChoices[0].Id);
    }

    [Fact]
    public async Task NewMonitor_is_disabled_without_folder_and_creates_for_the_first_top_level_folder()
    {
        await _vm.LoadAsync();
        Assert.False(_vm.NewMonitorCommand.CanExecute(null));

        var folder = await FolderAsync("api", withRequest: false);
        await _vm.LoadAsync();
        Assert.True(_vm.NewMonitorCommand.CanExecute(null));
        await _vm.NewMonitorCommand.ExecuteAsync(null);

        var stored = (await _monitors.ListAsync(Ct)).Single();
        Assert.Equal(Strings.MonitorNewName, stored.Name);
        Assert.Equal(folder.Id, stored.FolderId);
        Assert.Equal(stored.Id, _vm.SelectedMonitor?.Id);
        Assert.Equal("api", _vm.SelectedFolder?.Path);
        Assert.Equal("1 h", _vm.SelectedInterval?.Text);
    }

    [Fact]
    public async Task NewForFolder_creates_saves_and_selects()
    {
        var folder = await FolderAsync("api", withRequest: false);
        await _vm.LoadAsync();
        var nested = await FolderAsync("sub", folder.Id, withRequest: false);

        await _vm.NewForFolderAsync(nested.Id);

        Assert.Equal(nested.Id, (await _monitors.ListAsync(Ct)).Single().FolderId);
        Assert.Equal("api/sub", _vm.SelectedMonitor?.FolderName);
    }

    [Fact]
    public async Task Edit_save_and_revert()
    {
        var folder = await FolderAsync("api", withRequest: false);
        var other = await FolderAsync("other", withRequest: false);
        await MonitorAsync("a", folder.Id);
        await _vm.LoadAsync();
        Assert.False(_vm.SaveCommand.CanExecute(null));

        _vm.Name = "renamed";
        _vm.SelectedFolder = _vm.FolderChoices.Single(c => c.Id == other.Id);
        _vm.SelectedInterval = _vm.IntervalChoices.Single(c => c.Minutes == 1440);
        Assert.True(_vm.IsDirty);
        Assert.True(_vm.SaveCommand.CanExecute(null));
        await _vm.SaveCommand.ExecuteAsync(null);

        var stored = (await _monitors.ListAsync(Ct)).Single();
        Assert.Equal(("renamed", other.Id, 1440), (stored.Name, stored.FolderId, stored.IntervalMinutes));
        Assert.False(_vm.IsDirty);
        Assert.Equal("renamed", _vm.SelectedMonitor?.Name);
        Assert.Equal("other", _vm.SelectedMonitor?.FolderName);

        _vm.Name = "again";
        Assert.True(_vm.IsDirty);
        _vm.RevertCommand.Execute(null);
        Assert.Equal("renamed", _vm.Name);
        Assert.False(_vm.IsDirty);
    }

    [Fact]
    public async Task A_blank_name_cannot_be_saved()
    {
        var folder = await FolderAsync("api", withRequest: false);
        await MonitorAsync("a", folder.Id);
        await _vm.LoadAsync();

        _vm.Name = " ";

        Assert.False(_vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Changing_selection_with_unsaved_edits_asks_first()
    {
        var folder = await FolderAsync("api", withRequest: false);
        await MonitorAsync("a", folder.Id);
        await MonitorAsync("b", folder.Id);
        await _vm.LoadAsync();
        _vm.Name = "edited";

        _dialogs.UnsavedAnswer = UnsavedChoice.Cancel;
        Assert.False(await _vm.SelectMonitorAsync(_vm.Monitors[1]));
        Assert.Equal("a", _vm.SelectedMonitor?.Name);
        Assert.Equal("edited", _vm.Name);

        _dialogs.UnsavedAnswer = UnsavedChoice.Discard;
        Assert.True(await _vm.SelectMonitorAsync(_vm.Monitors[1]));
        Assert.Equal("b", _vm.Name);
        Assert.Equal(["a", "b"], (await _monitors.ListAsync(Ct)).Select(m => m.Name));

        _vm.Name = "saved";
        _dialogs.UnsavedAnswer = UnsavedChoice.Save;
        Assert.True(await _vm.SelectMonitorAsync(_vm.Monitors[0]));
        Assert.Contains("saved", (await _monitors.ListAsync(Ct)).Select(m => m.Name));
    }

    [Fact]
    public async Task Toggling_enabled_saves_at_once()
    {
        var folder = await FolderAsync("api", withRequest: false);
        await MonitorAsync("a", folder.Id);
        await _vm.LoadAsync();

        _vm.Monitors[0].Enabled = false;

        Assert.False((await _monitors.ListAsync(Ct)).Single().Enabled);
        Assert.False(_vm.IsDirty);
        Assert.Equal("", _vm.Monitors[0].NextRunText);
    }

    [Fact]
    public async Task Delete_removes_the_monitor_and_its_history()
    {
        var folder = await FolderAsync("api");
        var monitor = await MonitorAsync("a", folder.Id);
        await _runs.AppendAsync(MonitorRuns.Error(monitor.Id, _time.GetUtcNow(), "x"), Ct);
        await _vm.LoadAsync();

        _dialogs.DeleteAnswer = false;
        await _vm.DeleteCommand.ExecuteAsync(null);
        Assert.Single(await _monitors.ListAsync(Ct));

        _dialogs.DeleteAnswer = true;
        await _vm.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(await _monitors.ListAsync(Ct));
        Assert.Null(await _runs.LatestAsync(monitor.Id, Ct));
        Assert.Empty(_vm.Monitors);
        Assert.Null(_vm.SelectedMonitor);
        Assert.Empty(_vm.History);
    }

    [Fact]
    public async Task Run_now_records_a_run_that_appears_in_the_history_and_the_item()
    {
        var folder = await FolderAsync("api");
        await MonitorAsync("a", folder.Id);
        await _vm.LoadAsync();

        await RunNowAndWaitAsync(1);

        var row = _vm.History[0];
        Assert.Equal(MonitorRunStatus.Passed, row.Status);
        Assert.Equal(Strings.MonitorStatusPassed, row.StatusText);
        Assert.Equal(Strings.MonitorRequestsFailed(0, 1), row.RequestsText);
        Assert.Empty(row.Failures);
        Assert.Equal(Strings.MonitorStatusPassed, _vm.Monitors[0].LastStatusText);
        Assert.NotEqual(Strings.MonitorNever, _vm.Monitors[0].LastRunText);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$", _vm.Monitors[0].LastRunText);
        Assert.Equal(0, _vm.FailingCount);
    }

    [Fact]
    public async Task A_failing_run_warns_in_the_console_and_a_passing_run_clears_the_status()
    {
        var folder = await FolderAsync("api");
        await MonitorAsync("a", folder.Id);
        await _vm.LoadAsync();

        _failing = true;
        await RunNowAndWaitAsync(1);

        Assert.Equal(1, _vm.FailingCount);
        Assert.Equal(Strings.MonitorStatusBar(1), _vm.FailingText);
        Assert.True(_vm.Monitors[0].IsFailing);
        Assert.Contains(_console.Lines, l => l.Message.Contains(Strings.MonitorFailed("a", _vm.History[0].Message), StringComparison.Ordinal));
        Assert.Single(_vm.History[0].Failures);
        Assert.Contains("api request", _vm.History[0].Failures[0], StringComparison.Ordinal);

        _failing = false;
        await RunNowAndWaitAsync(2);

        Assert.Equal(0, _vm.FailingCount);
        Assert.Equal("", _vm.FailingText);
        Assert.False(_vm.Monitors[0].IsFailing);
    }

    [Fact]
    public async Task Next_run_is_the_last_run_plus_the_interval()
    {
        var folder = await FolderAsync("api");
        var monitor = await MonitorAsync("a", folder.Id, m => m with { IntervalMinutes = 30 });
        var at = _time.GetUtcNow();
        await _runs.AppendAsync(MonitorRuns.Error(monitor.Id, at, "x"), Ct);

        await _vm.LoadAsync();

        Assert.Equal(at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), _vm.Monitors[0].LastRunText);
        Assert.Equal((at + TimeSpan.FromMinutes(30)).ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), _vm.Monitors[0].NextRunText);
        Assert.Equal(1, _vm.FailingCount);
        Assert.Single(_vm.History);
    }
}
