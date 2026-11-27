using System.Collections.Concurrent;
using Yaapic.Core.Http;
using Yaapic.Core.LoadTesting;
using Yaapic.Core.Runner;
using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.LoadTesting;

public class LoadTestRunnerTests
{
    private static readonly ScriptBlock Code = new(ScriptLanguage.JavaScript, "x();");

    private sealed class FakeScopes : IRunScopeSource
    {
        public int PersistCalls;

        public VariableScopes GetScopes(Guid requestId) => new(new EnvironmentDefinition(Guid.NewGuid(), "e", []), [], []);

        public IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId) => [VariableScopeKind.Environment];

        public Task<IReadOnlyList<string>> PersistAsync(Guid requestId, IReadOnlyList<VariableChange> changes, CancellationToken ct)
        {
            Interlocked.Increment(ref PersistCalls);
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }

    private sealed class LatencyTransport(TimeProvider time) : IHttpTransport
    {
        public ConcurrentQueue<string> Urls { get; } = new();

        public async Task<SendResult> SendAsync(ResolvedRequest request, CancellationToken ct)
        {
            Urls.Enqueue(request.Url);
            await Task.Delay(TimeSpan.FromSeconds(1), time, ct);
            int status = request.Url.Contains("/500", StringComparison.Ordinal) ? 500 : 200;
            return SendResult.Success(new HttpResponseData(status, "x", [], [1], TimeSpan.FromMilliseconds(1000), request.Url));
        }
    }

    private sealed class Collector : IProgress<LoadSnapshot>
    {
        public ConcurrentQueue<LoadSnapshot> Snapshots { get; } = new();

        public void Report(LoadSnapshot value) => Snapshots.Enqueue(value);
    }

    private sealed class Rig
    {
        private int _idleTimers;
        private DateTimeOffset _start;

        public Rig(Func<ScriptInvocation, ScriptRunResult>? reply = null)
        {
            Time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
            Transport = new LatencyTransport(Time);
            ScriptEngineRegistry registry = new();
            registry.Add(new FakeScriptEngine(reply ?? (i => ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language))));
            Scopes = new FakeScopes();
            Runner = new LoadTestRunner(
                new RunStepExecutor(new ScriptedRequestSender(new RequestSender(Transport), registry, Time), new FakeScriptChainProvider(), Scopes),
                Scopes,
                Time);
        }

        public ManualTimeProvider Time { get; }

        public LatencyTransport Transport { get; }

        public FakeScopes Scopes { get; }

        public LoadTestRunner Runner { get; }

        /// <summary>Starts a run and remembers how many clock waiters it has when idle: the end of the duration, the snapshot timer and one per user.</summary>
        public Task<LoadTestResult> Run(LoadTestPlan plan, IProgress<LoadSnapshot>? progress, CancellationToken ct)
        {
            _idleTimers = 2 + plan.VirtualUsers;
            _start = Time.GetUtcNow();
            return Runner.RunAsync(plan, progress, ct);
        }

        /// <summary>Waits until every user is blocked on the clock again (a start offset, a request or a think time); no real time is involved.</summary>
        public Task SettleAsync() => Time.WaitForPendingTimersAsync(_idleTimers, TestContext.Current.CancellationToken);

        public async Task StepAsync()
        {
            await SettleAsync();
            Time.Advance(TimeSpan.FromSeconds(1));
            await SettleAsync();
        }

        /// <summary>Waits until the transport has seen <paramref name="count"/> requests.</summary>
        public Task WaitForRequestsAsync(int count) =>
            ManualTimeProvider.WaitUntilAsync(() => Transport.Urls.Count >= count, $"{count} requests", TestContext.Current.CancellationToken);

        /// <summary>
        /// Runs the plan to its end: whole seconds while every user waits on the clock, so each user completes exactly one request per second,
        /// then the last second, after which users that are between two waits are caught by the next advance.
        /// </summary>
        public async Task<LoadTestResult> FinishAsync(Task<LoadTestResult> run, Func<int, Task>? afterStep = null)
        {
            while (Time.GetUtcNow() - _start < TimeSpan.FromSeconds(9))
            {
                await StepAsync();
                if (afterStep is not null)
                {
                    await afterStep((int)(Time.GetUtcNow() - _start).TotalSeconds);
                }
            }

            await SettleAsync();
            while (!run.IsCompleted)
            {
                Time.Advance(TimeSpan.FromSeconds(1));
                await ManualTimeProvider.WaitUntilAsync(() => run.IsCompleted || Time.PendingTimers >= 2, "the run to end or wait on the clock", TestContext.Current.CancellationToken);
            }

            return await run;
        }
    }

    private static RunItem Item(string url = "http://h/ok", ScriptSet? scripts = null) =>
        new(HttpRequestDefinition.Create("r") with { Url = url, Scripts = scripts ?? ScriptSet.Empty }, "root");

    private static LoadTestPlan Plan(RunItem[] items, int users = 1, LoadProfile profile = LoadProfile.Fixed, int ramp = 4, int think = 0, params IterationDataRow[] data)
        => new("t", items, users, TimeSpan.FromSeconds(10), profile, TimeSpan.FromSeconds(ramp), think, data);

    private static ScriptSet Pre() => ScriptSet.Empty with { PreRequest = Code };

    private static ScriptSet Post() => ScriptSet.Empty with { PostResponse = Code };

    [Fact]
    public async Task Fixed_users_all_start_at_once_and_each_completes_one_request_per_second()
    {
        var rig = new Rig();
        var run = rig.Run(Plan([Item()], users: 3), null, CancellationToken.None);
        await rig.WaitForRequestsAsync(3);

        Assert.Equal(3, rig.Transport.Urls.Count);

        var result = await rig.FinishAsync(run);

        Assert.False(result.Canceled);
        Assert.InRange(result.Overall.Count, 27, 30);
        Assert.Equal(0, result.Overall.Errors);
        Assert.Equal(result.Overall.Count, result.PerRequest[0].Count);
    }

    [Fact]
    public async Task Ramp_up_starts_users_at_the_scheduled_offsets()
    {
        var rig = new Rig();
        using var cts = new CancellationTokenSource();
        var run = rig.Run(Plan([Item()], users: 4, profile: LoadProfile.RampUp, ramp: 4), null, cts.Token);
        await rig.SettleAsync();
        int[] counts = [rig.Transport.Urls.Count];
        await rig.StepAsync();
        counts = [.. counts, rig.Transport.Urls.Count];
        await rig.StepAsync();
        counts = [.. counts, rig.Transport.Urls.Count];
        await cts.CancelAsync();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal([1, 3, 6], counts);
        Assert.True(result.Canceled);
    }

    [Fact]
    public async Task Think_time_pauses_a_user_between_two_requests()
    {
        var rig = new Rig();
        using var cts = new CancellationTokenSource();
        var run = rig.Run(Plan([Item()], think: 2000), null, cts.Token);
        await rig.SettleAsync();
        int[] counts = [];
        for (int i = 0; i < 3; i++)
        {
            await rig.StepAsync();
            counts = [.. counts, rig.Transport.Urls.Count];
        }

        await cts.CancelAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal([1, 1, 2], counts);
    }

    [Fact]
    public async Task A_500_status_and_a_failed_test_count_as_errors()
    {
        var rig = new Rig(i => new ScriptRunResult(i.Phase, i.Origin, i.Block.Language, ScriptRunStatus.Succeeded, null, [], [new Yaapic.Domain.Scripting.TestResult("t", false, null, TimeSpan.Zero, i.Origin)], TimeSpan.Zero));
        RunItem[] items = [Item("http://h/ok"), Item("http://h/500"), Item("http://h/ok2", Post())];

        var result = await rig.FinishAsync(rig.Run(Plan(items), null, CancellationToken.None));

        Assert.Equal(0, result.PerRequest[0].Errors);
        Assert.True(result.PerRequest[1].Count > 0);
        Assert.Equal(result.PerRequest[1].Count, result.PerRequest[1].Errors);
        Assert.True(result.PerRequest[2].Count > 0);
        Assert.Equal(result.PerRequest[2].Count, result.PerRequest[2].Errors);
        Assert.Equal(result.PerRequest[1].Errors + result.PerRequest[2].Errors, result.Overall.Errors);
    }

    [Fact]
    public async Task A_skipped_request_is_not_a_sample()
    {
        var rig = new Rig(i =>
        {
            i.Host.SkipRequest();
            return ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language);
        });
        RunItem[] items = [Item("http://h/skipped", Pre()), Item("http://h/b")];

        var result = await rig.FinishAsync(rig.Run(Plan(items), null, CancellationToken.None));

        Assert.Equal(0, result.PerRequest[0].Count);
        Assert.True(result.PerRequest[1].Count > 0);
        Assert.DoesNotContain("http://h/skipped", rig.Transport.Urls);
    }

    [Fact]
    public async Task Data_rows_are_taken_round_robin_across_users()
    {
        var rig = new Rig();
        IterationDataRow[] rows = [new([new("id", "1")]), new([new("id", "2")]), new([new("id", "3")])];
        using var cts = new CancellationTokenSource();
        var run = rig.Run(Plan([Item("http://h/{{id}}")], users: 3, data: rows), null, cts.Token);
        await rig.WaitForRequestsAsync(3);
        string[] first = [.. rig.Transport.Urls.Order()];
        await rig.StepAsync();
        await rig.WaitForRequestsAsync(6);
        string[] all = [.. rig.Transport.Urls];
        await cts.CancelAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(["http://h/1", "http://h/2", "http://h/3"], first);
        Assert.Equal(["http://h/1", "http://h/2", "http://h/3"], all[3..].Order());
    }

    [Fact]
    public async Task Cancellation_returns_the_metrics_gathered_so_far()
    {
        var rig = new Rig();
        using var cts = new CancellationTokenSource();
        var run = rig.Run(Plan([Item()], users: 2), null, cts.Token);
        await rig.SettleAsync();
        await rig.StepAsync();
        await rig.StepAsync();
        await cts.CancelAsync();

        var result = await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(result.Canceled);
        Assert.True(result.Overall.Count >= 2);
    }

    [Fact]
    public async Task A_snapshot_is_reported_every_second_and_at_the_end()
    {
        var rig = new Rig();
        var progress = new Collector();
        var run = rig.Run(Plan([Item()], users: 3), progress, CancellationToken.None);
        await rig.WaitForRequestsAsync(3);

        // The timer coalesces ticks that arrive before the reporter has run, so wait for each one.
        var result = await rig.FinishAsync(run, second =>
            ManualTimeProvider.WaitUntilAsync(() => progress.Snapshots.Count >= second, $"the snapshot of second {second}", TestContext.Current.CancellationToken));

        LoadSnapshot[] snapshots = [.. progress.Snapshots];
        Assert.Equal(Enumerable.Range(1, 9).Select(s => TimeSpan.FromSeconds(s)), snapshots[..9].Select(s => s.Elapsed));
        Assert.Contains(snapshots, s => s.ActiveUsers == 3);
        Assert.Equal(result.Overall.Count, snapshots[^1].Overall.Count);
        Assert.Equal(0, snapshots[^1].ActiveUsers);
        Assert.True(snapshots.Length >= 10, $"{snapshots.Length} snapshots");
    }

    [Fact]
    public async Task Variable_changes_made_by_scripts_are_never_persisted()
    {
        var rig = new Rig(i =>
        {
            i.Host.SetVariable(ScriptScope.Environment, "token", "abc");
            return ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language);
        });

        var result = await rig.FinishAsync(rig.Run(Plan([Item(scripts: Post())]), null, CancellationToken.None));

        Assert.True(result.Overall.Count > 0);
        Assert.Equal(0, rig.Scopes.PersistCalls);
    }
}
