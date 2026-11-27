using Yaapic.Core.Http;
using Yaapic.Core.Runner;
using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Runner;

public class CollectionRunnerTests
{
    private static readonly ScriptBlock Code = new(ScriptLanguage.JavaScript, "x();");

    private sealed class FakeScopes : IRunScopeSource
    {
        public List<(Guid Id, IReadOnlyList<VariableChange> Changes)> Persisted { get; } = [];

        public IReadOnlyList<string> PersistErrors { get; set; } = [];

        public VariableScopes GetScopes(Guid requestId) => new(new EnvironmentDefinition(Guid.NewGuid(), "e", []), [], []);

        public IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId) => [VariableScopeKind.Environment];

        public Task<IReadOnlyList<string>> PersistAsync(Guid requestId, IReadOnlyList<VariableChange> changes, CancellationToken ct)
        {
            Persisted.Add((requestId, changes));
            return Task.FromResult(PersistErrors);
        }
    }

    private sealed class Rig
    {
        public Rig(Func<ScriptInvocation, ScriptRunResult>? reply = null, Func<ResolvedRequest, CancellationToken, Task<SendResult>>? send = null)
        {
            Transport = new FakeHttpTransport(send ?? ((_, _) => Task.FromResult(SendResult.Success(Response()))));
            Engine = new FakeScriptEngine(reply ?? (i => ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language)));
            ScriptEngineRegistry registry = new();
            registry.Add(Engine);
            Scopes = new FakeScopes();
            Time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
            Runner = new CollectionRunner(
                new RunStepExecutor(new ScriptedRequestSender(new RequestSender(Transport), registry, Time), new FakeScriptChainProvider(), Scopes),
                Time);
        }

        public FakeHttpTransport Transport { get; }

        public FakeScriptEngine Engine { get; }

        public FakeScopes Scopes { get; }

        public ManualTimeProvider Time { get; }

        public CollectionRunner Runner { get; }
    }

    private static HttpResponseData Response() => new(200, "OK", [], [1, 2, 3], TimeSpan.FromMilliseconds(5), "http://a/");

    private static ScriptSet Post() => ScriptSet.Empty with { PostResponse = Code };

    private static RunItem Item(string name, string url = "http://a/", ScriptSet? scripts = null) =>
        new(HttpRequestDefinition.Create(name) with { Url = url, Scripts = scripts ?? ScriptSet.Empty }, "root");

    private static RunPlan Plan(RunItem[] items, RunOptions? options = null, params IterationDataRow[] data) =>
        new("root", items, options ?? RunOptions.Default, data);

    private static ScriptRunResult WithTests(ScriptInvocation i, params bool[] passed) =>
        new(i.Phase, i.Origin, i.Block.Language, ScriptRunStatus.Succeeded, null, [], [.. passed.Select((p, n) => new Yaapic.Domain.Scripting.TestResult($"t{n}", p, null, TimeSpan.Zero, i.Origin))], TimeSpan.Zero);

    private sealed class SyncProgress(List<RunProgress> sink) : IProgress<RunProgress>
    {
        public void Report(RunProgress value) => sink.Add(value);
    }

    [Fact]
    public async Task Runs_three_items_over_two_iterations_in_order_with_the_data_row()
    {
        var rig = new Rig();
        RunItem[] items = [Item("a", "http://h/a/{{id}}"), Item("b", "http://h/b/{{id}}"), Item("c", "http://h/c/{{id}}")];
        IterationDataRow[] rows = [new([new("id", "1")]), new([new("id", "2")])];

        var result = await rig.Runner.RunAsync(Plan(items, new RunOptions(Iterations: 2), rows), null, CancellationToken.None);

        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Equal(2, result.DataRows);
        Assert.Equal(
            ["http://h/a/1", "http://h/b/1", "http://h/c/1", "http://h/a/2", "http://h/b/2", "http://h/c/2"],
            rig.Transport.Requests.Select(r => r.Url));
        Assert.Equal([0, 1], result.Iterations.Select(i => i.Index));
        Assert.All(result.Iterations, i => Assert.Equal(["a", "b", "c"], i.Requests.Select(r => r.Name)));
        Assert.Equal(6, RunSummary.From(result).Sent);
    }

    [Fact]
    public async Task Iteration_data_and_context_reach_the_script_host()
    {
        List<(int Iteration, int Count, string Value)> seen = [];
        var rig = new Rig(i =>
        {
            seen.Add((i.Host.Info.Iteration, i.Host.Info.IterationCount, i.Host.IterationData.Single().Value));
            return ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language);
        });
        IterationDataRow[] rows = [new([new("id", "x")]), new([new("id", "y")])];

        await rig.Runner.RunAsync(Plan([Item("a", scripts: Post())], new RunOptions(Iterations: 2), rows), null, CancellationToken.None);

        Assert.Equal([(0, 2, "x"), (1, 2, "y")], seen);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task A_variable_set_by_item_one_is_used_by_item_two_and_persisted_only_when_asked(bool persist, int persistCalls)
    {
        var rig = new Rig(i =>
        {
            if (i.Host.Info.RequestName == "one")
            {
                i.Host.SetVariable(ScriptScope.Environment, "token", "abc");
            }

            return ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language);
        });
        RunItem[] items = [Item("one", scripts: Post()), Item("two", "http://h/{{token}}")];

        var result = await rig.Runner.RunAsync(Plan(items, new RunOptions(PersistVariables: persist)), null, CancellationToken.None);

        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Equal("http://h/abc", rig.Transport.Requests[1].Url);
        Assert.Equal(persistCalls, rig.Scopes.Persisted.Count);
        if (persist)
        {
            Assert.Equal(items[0].Request.Id, rig.Scopes.Persisted[0].Id);
        }
    }

    [Fact]
    public async Task A_persist_error_becomes_a_console_warning()
    {
        var rig = new Rig(i =>
        {
            i.Host.SetVariable(ScriptScope.Environment, "k", "v");
            return ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language);
        });
        rig.Scopes.PersistErrors = ["disk full"];

        var result = await rig.Runner.RunAsync(Plan([Item("a", scripts: Post())]), null, CancellationToken.None);

        var entry = result.Iterations[0].Requests[0].Console.Single(c => c.Level == ConsoleLevel.Warn);
        Assert.Contains("disk full", entry.Message);
        Assert.Equal(ConsoleSource.System, entry.Source);
    }

    [Fact]
    public async Task Delay_waits_between_sends_only()
    {
        var rig = new Rig();
        var task = rig.Runner.RunAsync(Plan([Item("a"), Item("b")], new RunOptions(Iterations: 2, DelayMs: 1000)), null, CancellationToken.None);

        for (int expected = 1; expected <= 4; expected++)
        {
            await Eventually(() => rig.Transport.Requests.Count == expected);
            Assert.Equal(expected, rig.Transport.Requests.Count);
            if (expected == 4)
            {
                break;
            }

            Assert.False(task.IsCompleted);
            rig.Time.Advance(TimeSpan.FromMilliseconds(999));
            Assert.Equal(expected, rig.Transport.Requests.Count);
            rig.Time.Advance(TimeSpan.FromMilliseconds(1));
        }

        var result = await task;
        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Equal(4, rig.Transport.Requests.Count);
    }

    [Fact]
    public async Task Stops_on_failure_after_a_failed_test_and_keeps_the_results_so_far()
    {
        var rig = new Rig(i => WithTests(i, true, false));
        RunItem[] items = [Item("a", scripts: Post()), Item("b", scripts: Post())];

        var result = await rig.Runner.RunAsync(Plan(items, new RunOptions(Iterations: 2, StopOnFailure: true)), null, CancellationToken.None);

        Assert.Equal(RunStatus.StoppedOnFailure, result.Status);
        var only = Assert.Single(Assert.Single(result.Iterations).Requests);
        Assert.Equal(RunRequestOutcome.Failed, only.Outcome);
        Assert.Single(rig.Transport.Requests);
    }

    [Fact]
    public async Task Keeps_going_after_a_failure_when_stop_is_off()
    {
        var rig = new Rig(i => WithTests(i, false));

        var result = await rig.Runner.RunAsync(Plan([Item("a", scripts: Post()), Item("b", scripts: Post())]), null, CancellationToken.None);

        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Equal(2, result.Iterations[0].Requests.Count);
    }

    [Fact]
    public async Task Cancellation_mid_run_returns_canceled_with_the_completed_results()
    {
        using CancellationTokenSource cts = new();
        var rig = new Rig(send: (r, _) =>
        {
            if (r.Url.EndsWith("/2", StringComparison.Ordinal))
            {
                cts.Cancel();
            }

            return Task.FromResult(r.Url.EndsWith("/2", StringComparison.Ordinal) ? SendResult.Fail(SendFailureKind.Canceled, "canceled") : SendResult.Success(Response()));
        });
        RunItem[] items = [Item("a", "http://h/1"), Item("b", "http://h/2"), Item("c", "http://h/3")];

        var result = await rig.Runner.RunAsync(Plan(items), null, cts.Token);

        Assert.Equal(RunStatus.Canceled, result.Status);
        Assert.Equal(["a"], result.Iterations.Single().Requests.Select(r => r.Name));
        Assert.Equal(2, rig.Transport.Requests.Count);
    }

    [Fact]
    public async Task Cancellation_during_the_delay_returns_canceled()
    {
        using CancellationTokenSource cts = new();
        var rig = new Rig();
        var task = rig.Runner.RunAsync(Plan([Item("a"), Item("b")], new RunOptions(DelayMs: 5000)), null, cts.Token);
        await Eventually(() => rig.Transport.Requests.Count == 1);

        cts.Cancel();
        var result = await task;

        Assert.Equal(RunStatus.Canceled, result.Status);
        Assert.Single(result.Iterations.Single().Requests);
    }

    [Fact]
    public async Task Progress_reports_the_count_the_last_result_and_the_report()
    {
        var rig = new Rig();
        List<RunProgress> seen = [];

        await rig.Runner.RunAsync(Plan([Item("a"), Item("b")], new RunOptions(Iterations: 2)), new SyncProgress(seen), CancellationToken.None);

        Assert.Equal([1, 2, 3, 4], seen.Select(p => p.Completed));
        Assert.All(seen, p => Assert.Equal(4, p.Total));
        Assert.Equal([0, 0, 1, 1], seen.Select(p => p.Iteration));
        Assert.Equal("b", seen[1].Last!.Name);
        Assert.NotNull(seen[1].LastReport!.Report);
    }

    [Fact]
    public async Task An_empty_plan_completes_without_iteration_and_bad_options_throw()
    {
        var rig = new Rig();

        var result = await rig.Runner.RunAsync(Plan([]), null, CancellationToken.None);

        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Empty(result.Iterations);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => rig.Runner.RunAsync(Plan([Item("a")], new RunOptions(Iterations: 0)), null, CancellationToken.None));
    }

    [Fact]
    public async Task A_script_failure_is_an_error_result_with_the_script_message()
    {
        var rig = new Rig(i => new ScriptRunResult(i.Phase, i.Origin, i.Block.Language, ScriptRunStatus.Failed, new ScriptError("boom"), [], [], TimeSpan.Zero));

        var result = await rig.Runner.RunAsync(
            Plan([new RunItem(HttpRequestDefinition.Create("a") with { Url = "http://a/", Scripts = ScriptSet.Empty with { PreRequest = Code } }, "root")]),
            null,
            CancellationToken.None);

        var r = result.Iterations[0].Requests[0];
        Assert.Equal(RunRequestOutcome.Error, r.Outcome);
        Assert.Equal("boom", r.Error);
        Assert.Empty(rig.Transport.Requests);
    }

    private static ScriptSet Pre() => ScriptSet.Empty with { PreRequest = Code };

    private static Rig Flow(Action<ScriptInvocation> act)
    {
        return new Rig(i =>
        {
            act(i);
            return ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language);
        });
    }

    private static string[] Sent(Rig rig) => [.. rig.Transport.Requests.Select(r => r.Url)];

    [Fact]
    public async Task Set_next_request_jumps_forward_over_the_items_in_between()
    {
        var rig = Flow(i => { if (i.Host.Info.RequestName == "a") { i.Host.SetNextRequest("c"); } });
        RunItem[] items = [Item("a", "http://h/a", Post()), Item("b", "http://h/b"), Item("c", "http://h/c")];

        var result = await rig.Runner.RunAsync(Plan(items), null, CancellationToken.None);

        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Equal(["http://h/a", "http://h/c"], Sent(rig));
    }

    [Fact]
    public async Task Set_next_request_jumps_back_once_then_continues()
    {
        int visits = 0;
        var rig = Flow(i =>
        {
            if (i.Host.Info.RequestName == "b" && ++visits == 1)
            {
                i.Host.SetNextRequest("a");
            }
        });
        RunItem[] items = [Item("a", "http://h/a"), Item("b", "http://h/b", Post()), Item("c", "http://h/c")];

        var result = await rig.Runner.RunAsync(Plan(items), null, CancellationToken.None);

        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Equal(["http://h/a", "http://h/b", "http://h/a", "http://h/b", "http://h/c"], Sent(rig));
    }

    [Fact]
    public async Task Set_next_request_accepts_an_id_in_any_case_and_progress_never_exceeds_the_total()
    {
        RunItem[] items = [Item("a", "http://h/a", Post()), Item("b", "http://h/b"), Item("c", "http://h/c")];
        string id = items[2].Request.Id.ToString("D").ToUpperInvariant();
        var rig = Flow(i => { if (i.Host.Info.RequestName == "a") { i.Host.SetNextRequest(id); } });
        List<RunProgress> seen = [];

        await rig.Runner.RunAsync(Plan(items), new SyncProgress(seen), CancellationToken.None);

        Assert.Equal(["http://h/a", "http://h/c"], Sent(rig));
        Assert.All(seen, p => Assert.True(p.Completed <= p.Total));
    }

    [Fact]
    public async Task A_null_next_request_stops_the_run_without_further_iterations()
    {
        var rig = Flow(i => { if (i.Host.Info.RequestName == "a") { i.Host.SetNextRequest(null); } });

        var result = await rig.Runner.RunAsync(Plan([Item("a", "http://h/a", Post()), Item("b")], new RunOptions(Iterations: 3)), null, CancellationToken.None);

        Assert.Equal(RunStatus.Stopped, result.Status);
        Assert.Single(rig.Transport.Requests);
        Assert.Single(result.Iterations);
    }

    [Fact]
    public async Task An_unknown_next_request_fails_the_run_with_the_message()
    {
        var rig = Flow(i => i.Host.SetNextRequest("nope"));

        var result = await rig.Runner.RunAsync(Plan([Item("a", scripts: Post()), Item("b")]), null, CancellationToken.None);

        Assert.Equal(RunStatus.Failed, result.Status);
        Assert.Contains("nope", result.Error);
        Assert.Single(rig.Transport.Requests);
    }

    [Fact]
    public async Task An_infinite_loop_stops_at_1001_steps_with_the_guard_message()
    {
        var rig = Flow(i => i.Host.SetNextRequest("a"));

        var result = await rig.Runner.RunAsync(Plan([Item("a", scripts: Post())]), null, CancellationToken.None);

        Assert.Equal(RunStatus.Failed, result.Status);
        Assert.Contains("1000", result.Error);
        Assert.Equal(1000, rig.Transport.Requests.Count);
    }

    [Fact]
    public async Task A_skipped_request_is_recorded_and_the_next_one_runs_and_stop_on_failure_ignores_it()
    {
        var rig = Flow(i => { if (i.Host.Info.RequestName == "a") { i.Host.SkipRequest(); } });

        var result = await rig.Runner.RunAsync(Plan([Item("a", scripts: Pre()), Item("b", "http://h/b")], new RunOptions(StopOnFailure: true)), null, CancellationToken.None);

        Assert.Equal(RunStatus.Completed, result.Status);
        var requests = result.Iterations[0].Requests;
        Assert.Equal(RunRequestOutcome.Skipped, requests[0].Outcome);
        Assert.Null(requests[0].StatusCode);
        Assert.Equal(["http://h/b"], Sent(rig));
    }

    [Fact]
    public async Task A_skip_and_a_next_request_in_the_same_pre_request_jump()
    {
        var rig = Flow(i =>
        {
            if (i.Host.Info.RequestName == "a")
            {
                i.Host.SkipRequest();
                i.Host.SetNextRequest("c");
            }
        });

        var result = await rig.Runner.RunAsync(Plan([Item("a", scripts: Pre()), Item("b", "http://h/b"), Item("c", "http://h/c")]), null, CancellationToken.None);

        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Equal(["http://h/c"], Sent(rig));
        Assert.Equal(RunRequestOutcome.Skipped, result.Iterations[0].Requests[0].Outcome);
    }

    private static async Task Eventually(Func<bool> condition)
    {
        for (int i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}
