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

        public async Task StepAsync()
        {
            Time.Advance(TimeSpan.FromSeconds(1));
            await SettleAsync();
        }

        /// <summary>Waits until the request count has not moved for 50 ms; a fixed delay is too short on a loaded machine.</summary>
        public async Task SettleAsync()
        {
            int last = -1;
            int stable = 0;
            for (int i = 0; i < 500 && stable < 5; i++)
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
                int now = Transport.Urls.Count;
                stable = now == last ? stable + 1 : 0;
                last = now;
            }
        }

        /// <summary>Waits until the transport has seen <paramref name="count"/> requests; a fixed delay is too short on a loaded machine.</summary>
        public async Task WaitForRequestsAsync(int count)
        {
            for (int i = 0; i < 500 && Transport.Urls.Count < count; i++)
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
        }

        public async Task<LoadTestResult> FinishAsync(Task<LoadTestResult> run)
        {
            for (int i = 0; i < 40 && !run.IsCompleted; i++)
            {
                await StepAsync();
            }

            return await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
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
        var run = rig.Runner.RunAsync(Plan([Item()], users: 3), null, CancellationToken.None);
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
        var run = rig.Runner.RunAsync(Plan([Item()], users: 4, profile: LoadProfile.RampUp, ramp: 4), null, cts.Token);
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
        var run = rig.Runner.RunAsync(Plan([Item()], think: 2000), null, cts.Token);
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

        var result = await rig.FinishAsync(rig.Runner.RunAsync(Plan(items), null, CancellationToken.None));

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

        var result = await rig.FinishAsync(rig.Runner.RunAsync(Plan(items), null, CancellationToken.None));

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
        var run = rig.Runner.RunAsync(Plan([Item("http://h/{{id}}")], users: 3, data: rows), null, cts.Token);
        await rig.WaitForRequestsAsync(3);
        string[] first = [.. rig.Transport.Urls.Order()];
        await rig.StepAsync();
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
        var run = rig.Runner.RunAsync(Plan([Item()], users: 2), null, cts.Token);
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

        var result = await rig.FinishAsync(rig.Runner.RunAsync(Plan([Item()], users: 3), progress, CancellationToken.None));

        Assert.True(progress.Snapshots.Count >= 10, $"{progress.Snapshots.Count} snapshots");
        Assert.Contains(progress.Snapshots, s => s.ActiveUsers == 3);
        Assert.Equal(result.Overall.Count, progress.Snapshots.Last().Overall.Count);
    }

    [Fact]
    public async Task Variable_changes_made_by_scripts_are_never_persisted()
    {
        var rig = new Rig(i =>
        {
            i.Host.SetVariable(ScriptScope.Environment, "token", "abc");
            return ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language);
        });

        var result = await rig.FinishAsync(rig.Runner.RunAsync(Plan([Item(scripts: Post())]), null, CancellationToken.None));

        Assert.True(result.Overall.Count > 0);
        Assert.Equal(0, rig.Scopes.PersistCalls);
    }
}
