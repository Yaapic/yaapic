using Yaapic.Core.Http;
using Yaapic.Core.Import;
using Yaapic.Core.Runner;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Runner;

namespace Yaapic.ViewModels.Tests.Runner;

public sealed class RunnerViewModelTests
{
    private static readonly ScriptBlock Code = new(ScriptLanguage.JavaScript, "x();");

    private sealed class NoReader : IImportInputReader
    {
        public Task<ImportInput> ReadFileAsync(string path, CancellationToken ct) => throw new NotSupportedException();

        public Task<ImportInput> ReadFolderAsync(string path, CancellationToken ct) => throw new NotSupportedException();
    }

    private readonly FakeDialogService _dialogs = new();
    private readonly ConsoleViewModel _console = new();
    private readonly FakeScriptEngine _engine;
    private readonly RunnerViewModel _vm;

    public RunnerViewModelTests()
    {
        _engine = new FakeScriptEngine(Reply);
        ScriptEngineRegistry registry = new();
        registry.Add(_engine);
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var transport = new FakeHttpTransport((r, _) => Task.FromResult(r.Url.Contains("err", StringComparison.Ordinal)
            ? SendResult.Fail(SendFailureKind.ConnectionFailed, "down")
            : SendResult.Success(new HttpResponseData(200, "OK", [], [1, 2, 3], TimeSpan.FromMilliseconds(5), r.Url))));
        var scopes = new VariableScopeRunSource(new FakeVariableScopeProvider());
        var runner = new CollectionRunner(
            new RunStepExecutor(new ScriptedRequestSender(new RequestSender(transport), registry, time), new FakeScriptChainProvider(), scopes),
            time);
        _vm = new RunnerViewModel(new RunnerSetupViewModel(new NoReader(), _dialogs), runner, _console, _dialogs);
        _vm.Open(Folder("Root", Req("aok"), Req("bbad"), Req("cskp"), Req("derr")));
    }

    private static ScriptRunResult Reply(ScriptInvocation i)
    {
        string name = i.Host.Info.RequestName;
        if (i.Phase == ScriptPhase.PreRequest)
        {
            if (name == "cskp")
            {
                i.Host.SkipRequest();
            }

            return ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language);
        }

        Yaapic.Domain.Scripting.TestResult test = new("t", name == "aok", name == "aok" ? null : "boom", TimeSpan.Zero, i.Origin);
        return new ScriptRunResult(i.Phase, i.Origin, i.Block.Language, ScriptRunStatus.Succeeded, null, [], [test], TimeSpan.Zero);
    }

    private static CollectionNode Req(string name) => CollectionNode.ForRequest(HttpRequestDefinition.Create(name) with
    {
        Url = $"http://h/{name}",
        Scripts = ScriptSet.Empty with { PreRequest = Code, PostResponse = Code },
    });

    private static CollectionNode Folder(string name, params CollectionNode[] children) =>
        CollectionNode.ForFolder(FolderDefinition.Create(name), children);

    [Fact]
    public async Task Rows_carry_the_iteration_number_only_when_the_run_has_several_iterations()
    {
        _vm.Setup.Iterations = 2;

        await _vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(["#1", "#1", "#1", "#1", "#2", "#2", "#2", "#2"], _vm.Rows.Select(r => r.IterationText));
    }

    [Fact]
    public async Task A_run_fills_the_rows_the_summary_the_status_and_the_console()
    {
        List<RunResult> completed = [];
        int started = 0;
        _vm.RunStarted += (_, _) => started++;
        _vm.RunCompleted += (_, r) => completed.Add(r);

        await _vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(1, started);
        Assert.Single(completed);
        Assert.False(_vm.IsRunning);
        Assert.Equal(["aok", "bbad", "cskp", "derr"], _vm.Rows.Select(r => r.Name));
        Assert.Equal([1, 1, 1, 1], _vm.Rows.Select(r => r.Iteration));
        Assert.All(_vm.Rows, r => Assert.Equal("", r.IterationText));
        Assert.Equal(["Passed", "Failed", "Skipped", "Error"], _vm.Rows.Select(r => r.OutcomeText));
        Assert.Equal(["200", "200", "-", "-"], _vm.Rows.Select(r => r.StatusText));
        Assert.Equal(["1/1", "0/1", "", ""], _vm.Rows.Select(r => r.TestsText));
        Assert.Equal("GET", _vm.Rows[0].Method);
        Assert.Equal("4/4", _vm.ProgressText);
        Assert.Equal(1.0, _vm.Progress);
        Assert.Equal((2, 1, 1, 1), (_vm.SummarySent, _vm.SummaryErrors, _vm.SummaryFailed, _vm.SummarySkipped));
        Assert.Equal("1/2", _vm.SummaryTests);
        Assert.Equal(Strings.RunnerStatusCompleted, _vm.StatusText);
        Assert.NotNull(_vm.Result);
        Assert.NotEmpty(_console.Lines);
    }

    [Fact]
    public async Task Filters_narrow_the_visible_rows()
    {
        await _vm.RunCommand.ExecuteAsync(null);

        _vm.Filter = RunnerFilter.Passed;
        Assert.Equal(["aok"], _vm.VisibleRows.Select(r => r.Name));
        _vm.Filter = RunnerFilter.Failed;
        Assert.Equal(["bbad", "derr"], _vm.VisibleRows.Select(r => r.Name));
        _vm.Filter = RunnerFilter.Skipped;
        Assert.Equal(["cskp"], _vm.VisibleRows.Select(r => r.Name));
        _vm.Filter = RunnerFilter.All;
        Assert.Equal(4, _vm.VisibleRows.Count);
    }

    [Fact]
    public async Task Selecting_a_row_shows_its_tests_and_error()
    {
        await _vm.RunCommand.ExecuteAsync(null);

        _vm.SelectedRow = _vm.Rows[1];
        Assert.Equal(["t"], _vm.SelectedTests.Select(t => t.Name));
        Assert.False(_vm.SelectedTests[0].Passed);
        Assert.Null(_vm.SelectedError);

        _vm.SelectedRow = _vm.Rows[3];
        Assert.NotNull(_vm.SelectedError);
        Assert.Empty(_vm.SelectedTests);

        _vm.SelectedRow = null;
        Assert.Empty(_vm.SelectedConsole);
    }

    [Fact]
    public async Task Stop_cancels_the_run_and_shows_the_canceled_status()
    {
        _engine.Delay = TimeSpan.FromHours(1);

        Task run = _vm.RunCommand.ExecuteAsync(null);
        Assert.True(_vm.IsRunning);
        Assert.True(_vm.StopCommand.CanExecute(null));
        _vm.StopCommand.Execute(null);
        await run;

        Assert.False(_vm.IsRunning);
        Assert.Equal(RunStatus.Canceled, _vm.Result!.Status);
        Assert.Equal(Strings.RunnerStatusCanceled, _vm.StatusText);
    }

    [Fact]
    public async Task Run_is_disabled_while_running_and_when_the_setup_cannot_run()
    {
        Assert.True(_vm.RunCommand.CanExecute(null));
        _engine.Delay = TimeSpan.FromHours(1);

        Task run = _vm.RunCommand.ExecuteAsync(null);
        Assert.False(_vm.RunCommand.CanExecute(null));
        _vm.StopCommand.Execute(null);
        await run;
        Assert.True(_vm.RunCommand.CanExecute(null));

        _vm.Setup.SelectNoneCommand.Execute(null);
        Assert.False(_vm.RunCommand.CanExecute(null));
    }

    [Fact]
    public async Task Open_during_a_run_keeps_the_current_setup()
    {
        _engine.Delay = TimeSpan.FromHours(1);
        Task run = _vm.RunCommand.ExecuteAsync(null);

        _vm.Open(Folder("Other", Req("x")));

        Assert.Equal("Root", _vm.Setup.FolderName);
        _vm.StopCommand.Execute(null);
        await run;
        _vm.Open(Folder("Other", Req("x")));
        Assert.Equal("Other", _vm.Setup.FolderName);
    }

    [Fact]
    public async Task Export_writes_the_reports_with_the_suggested_names_and_reports_the_save()
    {
        Assert.False(_vm.ExportJsonCommand.CanExecute(null));
        await _vm.RunCommand.ExecuteAsync(null);
        RunResult result = _vm.Result!;
        _dialogs.SaveFileAnswer = true;

        await _vm.ExportJsonCommand.ExecuteAsync(null);
        Assert.Equal(RunReportJson.SuggestedFileName(result), _dialogs.SavedFiles[0].Name);
        Assert.Equal(RunReportJson.Write(result), _dialogs.SavedFiles[0].Content);
        Assert.Equal(Strings.RunnerExported(_dialogs.SavedFiles[0].Name), _vm.ExportMessage);

        await _vm.ExportJUnitCommand.ExecuteAsync(null);
        Assert.Equal(RunReportJUnit.SuggestedFileName(result), _dialogs.SavedFiles[1].Name);
        Assert.Equal(RunReportJUnit.Write(result), _dialogs.SavedFiles[1].Content);
    }

    [Fact]
    public async Task A_canceled_save_leaves_no_message()
    {
        await _vm.RunCommand.ExecuteAsync(null);
        _dialogs.SaveFileAnswer = false;

        await _vm.ExportJsonCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.SavedFiles);
        Assert.Null(_vm.ExportMessage);
    }
}
