using System.Text;
using Yaapic.Core.Http;
using Yaapic.Core.Import;
using Yaapic.Core.LoadTesting;
using Yaapic.Core.Runner;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.LoadTesting;
using Yaapic.ViewModels.Runner;

namespace Yaapic.ViewModels.Tests.LoadTesting;

public sealed class LoadTestViewModelTests
{
    private sealed class NoReader : IImportInputReader
    {
        public Task<ImportInput> ReadFileAsync(string path, CancellationToken ct) => throw new NotSupportedException();

        public Task<ImportInput> ReadFolderAsync(string path, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class LatencyTransport(TimeProvider time) : IHttpTransport
    {
        public async Task<SendResult> SendAsync(ResolvedRequest request, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), time, ct);
            int status = request.Url.Contains("/bad", StringComparison.Ordinal) ? 500 : 200;
            return SendResult.Success(new HttpResponseData(status, "x", [], [1], TimeSpan.FromMilliseconds(1000), request.Url));
        }
    }

    private sealed class DefaultDialogs : IDialogService
    {
        public Task<UnsavedChoice> ConfirmUnsavedAsync(string itemName) => Task.FromResult(UnsavedChoice.Cancel);

        public Task<bool> ConfirmDeleteAsync(string itemName) => Task.FromResult(false);
    }

    private readonly FakeDialogService _dialogs = new();
    private readonly ManualTimeProvider _time = new(DateTimeOffset.UnixEpoch);
    private readonly LoadTestViewModel _vm;

    public LoadTestViewModelTests()
    {
        ScriptEngineRegistry registry = new();
        registry.Add(new FakeScriptEngine(i => ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language)));
        var provider = new FakeVariableScopeProvider();
        var scopes = new VariableScopeRunSource(provider);
        var runner = new LoadTestRunner(
            new RunStepExecutor(new ScriptedRequestSender(new RequestSender(new LatencyTransport(_time)), registry, _time), new FakeScriptChainProvider(), scopes),
            scopes,
            _time);
        _vm = new LoadTestViewModel(new RunnerSetupViewModel(new NoReader(), _dialogs), runner, provider, _dialogs);
        _vm.Open(Folder("Root", Req("ok", "http://a.test/ok"), Req("bad", "http://b.test/bad")));
        _vm.VirtualUsers = 2;
        _vm.DurationSeconds = 10;
    }

    private static CollectionNode Req(string name, string url) => CollectionNode.ForRequest(HttpRequestDefinition.Create(name) with { Url = url });

    private static CollectionNode Folder(string name, params CollectionNode[] children) =>
        CollectionNode.ForFolder(FolderDefinition.Create(name), children);

    private async Task StepAsync()
    {
        _time.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(40, TestContext.Current.CancellationToken);
    }

    private async Task FinishAsync(Task run)
    {
        for (int i = 0; i < 40 && !run.IsCompleted; i++)
        {
            await StepAsync();
        }

        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Defaults_are_valid()
    {
        var vm = new LoadTestViewModel(new RunnerSetupViewModel(new NoReader(), _dialogs), null!, new FakeVariableScopeProvider(), _dialogs);

        Assert.Equal((10, 60, LoadProfile.Fixed, 10, 0), (vm.VirtualUsers, vm.DurationSeconds, vm.Profile, vm.RampUpSeconds, vm.ThinkTimeMs));
        Assert.False(vm.IsRampUp);
        Assert.Equal(Strings.RunnerNothingSelected, vm.ValidationError);
        Assert.Null(_vm.ValidationError);
        Assert.True(_vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public void Validation_reports_each_out_of_range_value()
    {
        _vm.VirtualUsers = 0;
        Assert.Equal(Strings.LoadUsersRange(LoadTestPlan.MaxVirtualUsers), _vm.ValidationError);
        Assert.False(_vm.StartCommand.CanExecute(null));
        _vm.VirtualUsers = LoadTestPlan.MaxVirtualUsers + 1;
        Assert.Equal(Strings.LoadUsersRange(LoadTestPlan.MaxVirtualUsers), _vm.ValidationError);
        _vm.VirtualUsers = 5;

        _vm.DurationSeconds = 9;
        Assert.Equal(Strings.LoadDurationRange, _vm.ValidationError);
        _vm.DurationSeconds = 3601;
        Assert.Equal(Strings.LoadDurationRange, _vm.ValidationError);
        _vm.DurationSeconds = 20;

        _vm.RampUpSeconds = 0;
        Assert.Null(_vm.ValidationError);
        _vm.Profile = LoadProfile.RampUp;
        Assert.True(_vm.IsRampUp);
        Assert.Equal(Strings.LoadRampRange, _vm.ValidationError);
        _vm.RampUpSeconds = 21;
        Assert.Equal(Strings.LoadRampRange, _vm.ValidationError);
        _vm.RampUpSeconds = 20;
        Assert.Null(_vm.ValidationError);

        _vm.ThinkTimeMs = 10001;
        Assert.Equal(Strings.LoadThinkRange, _vm.ValidationError);
        _vm.ThinkTimeMs = 0;

        _vm.Requests.SelectNoneCommand.Execute(null);
        Assert.Equal(Strings.RunnerNothingSelected, _vm.ValidationError);
        Assert.False(_vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task The_first_start_asks_for_confirmation_with_the_hosts_and_a_decline_starts_nothing()
    {
        _dialogs.ConfirmAnswer = false;

        await _vm.StartCommand.ExecuteAsync(null);

        Assert.Equal([Strings.LoadConfirm("http://b.test:80, http://a.test:80")], _dialogs.ConfirmMessages);
        Assert.False(_vm.IsRunning);
        Assert.Null(_vm.Result);

        _dialogs.ConfirmAnswer = true;
        Task run = _vm.StartCommand.ExecuteAsync(null);
        await FinishAsync(run);
        Task second = _vm.StartCommand.ExecuteAsync(null);
        await FinishAsync(second);

        Assert.Equal(2, _dialogs.ConfirmMessages.Count);
        Assert.NotNull(_vm.Result);
    }

    [Fact]
    public async Task Snapshots_update_the_tiles_and_normalized_charts_and_the_final_result_fills_the_tables()
    {
        Task run = _vm.StartCommand.ExecuteAsync(null);
        await Task.Delay(40, TestContext.Current.CancellationToken);
        Assert.True(_vm.IsRunning);
        for (int i = 0; i < 4; i++)
        {
            await StepAsync();
        }

        Assert.True(_vm.TotalRequests > 0);
        Assert.EndsWith("/s", _vm.Throughput, StringComparison.Ordinal);
        Assert.Contains(" / 0:10", _vm.ElapsedText, StringComparison.Ordinal);
        Assert.NotEmpty(_vm.ThroughputPoints);

        await FinishAsync(run);

        Assert.False(_vm.IsRunning);
        Assert.NotNull(_vm.Result);
        Assert.Equal(0, _vm.ActiveUsers);
        foreach (IReadOnlyList<ChartPoint> series in new[] { _vm.ThroughputPoints, _vm.ResponseTimePoints, _vm.ErrorPoints, _vm.UserPoints })
        {
            Assert.All(series, p => Assert.InRange(p.X, 0, 1));
            Assert.All(series, p => Assert.InRange(p.Y, 0, 1));
            Assert.Equal(series.Select(p => p.X).Order(), series.Select(p => p.X));
        }

        Assert.Equal(["bad", "ok"], _vm.RequestRows.Select(r => r.Name));
        Assert.Equal(["GET", "GET"], _vm.RequestRows.Select(r => r.Method));
        Assert.Equal(0, _vm.RequestRows[1].Errors);
        Assert.True(_vm.RequestRows[0].Errors > 0);
        Assert.Equal(_vm.TotalRequests, _vm.RequestRows.Sum(r => r.Count));
        Assert.NotEqual("", _vm.ErrorRateText);
        Assert.Equal("", _vm.Note);
        Assert.True(_vm.ExportCommand.CanExecute(null));
    }

    [Fact]
    public async Task Stop_cancels_the_test_and_keeps_the_partial_result()
    {
        Task run = _vm.StartCommand.ExecuteAsync(null);
        await Task.Delay(40, TestContext.Current.CancellationToken);
        await StepAsync();

        Assert.True(_vm.StopCommand.CanExecute(null));
        _vm.StopCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(_vm.IsRunning);
        Assert.True(_vm.Result!.Canceled);
        Assert.False(_vm.StopCommand.CanExecute(null));
    }

    [Fact]
    public async Task Export_saves_the_json_report_under_its_suggested_name()
    {
        _dialogs.SaveFileAnswer = true;
        Task run = _vm.StartCommand.ExecuteAsync(null);
        await FinishAsync(run);

        await _vm.ExportCommand.ExecuteAsync(null);

        (string name, byte[] content) = Assert.Single(_dialogs.SavedFiles);
        Assert.Equal(LoadTestReportJson.SuggestedFileName(_vm.Result!), name);
        Assert.Equal(LoadTestReportJson.Write(_vm.Result!), content);
        Assert.StartsWith("{", Encoding.UTF8.GetString(content), StringComparison.Ordinal);
        Assert.Equal(Strings.RunnerExported(name), _vm.ExportMessage);
    }

    [Fact]
    public async Task Requests_with_scripts_add_a_note()
    {
        _vm.Open(Folder("S", CollectionNode.ForRequest(HttpRequestDefinition.Create("s") with
        {
            Url = "http://a.test/ok",
            Scripts = ScriptSet.Empty with { PreRequest = new ScriptBlock(ScriptLanguage.JavaScript, "x();") },
        })));
        _vm.VirtualUsers = 1;
        _vm.DurationSeconds = 10;

        Task run = _vm.StartCommand.ExecuteAsync(null);
        await FinishAsync(run);

        Assert.Equal(Strings.LoadScriptsSkipped, _vm.Note);
    }

    [Fact]
    public async Task The_default_confirmation_returns_true()
    {
        IDialogService dialogs = new DefaultDialogs();

        Assert.True(await dialogs.ConfirmAsync("t", "m"));
    }
}
