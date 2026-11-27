using System.Collections.Concurrent;
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

namespace Yaapic.Core.Tests.Monitors;

public class MonitorSchedulerTests
{
    private sealed class FakeScopes : IRunScopeSource
    {
        public int Persisted { get; private set; }

        public VariableScopes GetScopes(Guid requestId) => new(new EnvironmentDefinition(Guid.NewGuid(), "active", [new("host", "active")]), [], []);

        public IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId) => [VariableScopeKind.Environment];

        public Task<IReadOnlyList<string>> PersistAsync(Guid requestId, IReadOnlyList<VariableChange> changes, CancellationToken ct)
        {
            Persisted++;
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }

    private sealed class Rig : IAsyncDisposable
    {
        private readonly ConcurrentQueue<MonitorRun> _completed = [];
        private int _inFlight;

        public Rig(bool gated = false)
        {
            Transport = new FakeHttpTransport(async (_, ct) =>
            {
                MaxInFlight = Math.Max(MaxInFlight, Interlocked.Increment(ref _inFlight));
                try
                {
                    if (gated)
                    {
                        await Gate.Task.WaitAsync(ct);
                    }

                    return SendResult.Success(new HttpResponseData(200, "OK", [], [], TimeSpan.Zero, "http://a/"));
                }
                finally
                {
                    Interlocked.Decrement(ref _inFlight);
                }
            });
            ScriptEngineRegistry registry = new();
            registry.Add(new FakeScriptEngine(i => ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language)));
            Time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
            Scopes = new FakeScopes();
            var executor = new RunStepExecutor(new ScriptedRequestSender(new RequestSender(Transport), registry, Time), new FakeScriptChainProvider(), Scopes);
            Scheduler = new MonitorScheduler(Monitors, Runs, Collections, Environments, Scopes, executor, Time);
            Scheduler.RunCompleted += (_, run) => _completed.Enqueue(run);
        }

        public InMemoryMonitorStore Monitors { get; } = new();

        public InMemoryMonitorRunStore Runs { get; } = new();

        public InMemoryEnvironmentStore Environments { get; } = new();

        public InMemoryRequestStore Requests { get; } = new();

        public InMemoryCollectionStore Collections => field ??= new(Requests);

        public FakeHttpTransport Transport { get; }

        public FakeScopes Scopes { get; }

        public ManualTimeProvider Time { get; }

        public MonitorScheduler Scheduler { get; }

        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MaxInFlight { get; private set; }

        public IReadOnlyList<MonitorRun> Completed => [.. _completed];

        public async Task<MonitorDefinition> AddMonitorAsync(string name, string url = "http://h/{{host}}", Func<MonitorDefinition, MonitorDefinition>? edit = null)
        {
            FolderDefinition folder = FolderDefinition.Create(name);
            await Collections.SaveFolderAsync(folder, null, TestContext.Current.CancellationToken);
            HttpRequestDefinition request = HttpRequestDefinition.Create(name + " request") with { Url = url };
            await Requests.SaveAsync(request, TestContext.Current.CancellationToken);
            await Collections.MoveAsync(request.Id, folder.Id, TestContext.Current.CancellationToken);
            MonitorDefinition monitor = MonitorDefinition.Create(name, folder.Id) with { IntervalMinutes = 5 };
            monitor = edit?.Invoke(monitor) ?? monitor;
            await Monitors.SaveAsync(monitor, TestContext.Current.CancellationToken);
            return monitor;
        }

        public async Task WaitForCompletedAsync(int count) => await WaitAsync(() => Completed.Count >= count);

        public ValueTask DisposeAsync() => Scheduler.DisposeAsync();
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(5, timeout.Token);
        }
    }

    [Fact]
    public async Task A_never_run_enabled_monitor_runs_at_start_and_again_only_after_its_interval()
    {
        await using var rig = new Rig();
        MonitorDefinition monitor = await rig.AddMonitorAsync("m");

        rig.Scheduler.Start();
        rig.Scheduler.Start();
        await rig.WaitForCompletedAsync(1);
        Assert.Equal(MonitorRunStatus.Passed, rig.Completed[0].Status);
        Assert.Equal(monitor.Id, rig.Completed[0].MonitorId);
        Assert.Equal(DateTimeOffset.UnixEpoch, (await rig.Runs.LatestAsync(monitor.Id, TestContext.Current.CancellationToken))!.At);

        // Four minutes later: ticks fire, but the interval (5 minutes) has not elapsed.
        for (int i = 0; i < 8; i++)
        {
            rig.Time.Advance(TimeSpan.FromSeconds(30));
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Single(rig.Completed);

        for (int i = 0; i < 2 && rig.Completed.Count < 2; i++)
        {
            rig.Time.Advance(TimeSpan.FromSeconds(30));
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        await rig.WaitForCompletedAsync(2);
        Assert.Equal(2, rig.Transport.Requests.Count);
    }

    [Fact]
    public async Task A_disabled_monitor_runs_only_on_demand()
    {
        await using var rig = new Rig();
        MonitorDefinition disabled = await rig.AddMonitorAsync("a-disabled", edit: m => m with { Enabled = false });
        MonitorDefinition sentinel = await rig.AddMonitorAsync("b-sentinel");

        rig.Scheduler.Start();
        await rig.WaitForCompletedAsync(1);
        Assert.Equal(sentinel.Id, rig.Completed[0].MonitorId);
        Assert.Null(await rig.Runs.LatestAsync(disabled.Id, TestContext.Current.CancellationToken));

        rig.Scheduler.RunNow(disabled.Id);
        await rig.WaitForCompletedAsync(2);
        Assert.Equal(disabled.Id, rig.Completed[1].MonitorId);
    }

    [Fact]
    public async Task Due_monitors_run_one_after_the_other_never_in_parallel()
    {
        await using var rig = new Rig(gated: true);
        MonitorDefinition first = await rig.AddMonitorAsync("a");
        MonitorDefinition second = await rig.AddMonitorAsync("b");

        rig.Scheduler.Start();
        await WaitAsync(() => rig.Scheduler.Running is not null && rig.Scheduler.Queued.Count == 1);
        Guid running = rig.Scheduler.Running!.Value;
        Assert.Equal([running == first.Id ? second.Id : first.Id], rig.Scheduler.Queued);
        Assert.Single(rig.Transport.Requests);

        rig.Gate.SetResult();
        await rig.WaitForCompletedAsync(2);

        Assert.Equal(1, rig.MaxInFlight);
        Assert.Equal(2, rig.Completed.Select(r => r.MonitorId).Distinct().Count());
    }

    [Fact]
    public async Task The_monitor_environment_is_used_instead_of_the_active_one_and_none_means_none()
    {
        await using var rig = new Rig();
        var environment = new EnvironmentDefinition(Guid.NewGuid(), "prod", [new("host", "prod")]);
        await rig.Environments.SaveAsync(environment, TestContext.Current.CancellationToken);
        await rig.AddMonitorAsync("m", edit: m => m with { EnvironmentId = environment.Id });
        await rig.AddMonitorAsync("n");

        rig.Scheduler.Start();
        await rig.WaitForCompletedAsync(2);

        Assert.Equal(["http://h/prod", "http://h/{{host}}"], rig.Transport.Requests.Select(r => r.Url).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Variable_changes_are_not_persisted()
    {
        await using var rig = new Rig();
        await rig.AddMonitorAsync("m");

        rig.Scheduler.Start();
        await rig.WaitForCompletedAsync(1);

        Assert.Equal(0, rig.Scopes.Persisted);
    }

    [Fact]
    public async Task A_missing_folder_and_a_missing_environment_record_error_runs()
    {
        await using var rig = new Rig();
        MonitorDefinition noFolder = await rig.AddMonitorAsync("a");
        noFolder = noFolder with { FolderId = Guid.NewGuid() };
        await rig.Monitors.SaveAsync(noFolder, TestContext.Current.CancellationToken);
        MonitorDefinition noEnvironment = await rig.AddMonitorAsync("b", edit: m => m with { EnvironmentId = Guid.NewGuid() });

        rig.Scheduler.Start();
        await rig.WaitForCompletedAsync(2);

        Assert.All(rig.Completed, r => Assert.Equal(MonitorRunStatus.Error, r.Status));
        Assert.Equal(Strings.MonitorFolderMissing, rig.Completed.Single(r => r.MonitorId == noFolder.Id).Message);
        Assert.Equal(Strings.MonitorEnvironmentMissing, rig.Completed.Single(r => r.MonitorId == noEnvironment.Id).Message);
        Assert.Empty(rig.Transport.Requests);
    }

    [Fact]
    public async Task StopAsync_cancels_the_current_run_without_recording_it()
    {
        await using var rig = new Rig(gated: true);
        MonitorDefinition monitor = await rig.AddMonitorAsync("m");

        rig.Scheduler.Start();
        await WaitAsync(() => rig.Transport.Requests.Count == 1);
        await rig.Scheduler.StopAsync();

        Assert.Null(rig.Scheduler.Running);
        Assert.Empty(rig.Completed);
        Assert.Null(await rig.Runs.LatestAsync(monitor.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunNow_does_not_queue_a_monitor_twice()
    {
        await using var rig = new Rig(gated: true);
        MonitorDefinition monitor = await rig.AddMonitorAsync("m");

        rig.Scheduler.RunNow(monitor.Id);
        await WaitAsync(() => rig.Scheduler.Running == monitor.Id);
        rig.Scheduler.RunNow(monitor.Id);
        Assert.Empty(rig.Scheduler.Queued);

        rig.Gate.SetResult();
        await rig.WaitForCompletedAsync(1);
        Assert.Single(rig.Completed);
    }

    [Fact]
    public void NextRun_is_null_when_disabled_now_when_never_run_and_after_the_interval_otherwise()
    {
        var monitor = MonitorDefinition.Create("m", Guid.NewGuid()) with { IntervalMinutes = 15 };
        DateTimeOffset now = DateTimeOffset.UnixEpoch.AddHours(1);
        var latest = MonitorRuns.Error(monitor.Id, DateTimeOffset.UnixEpoch, "x");

        Assert.Null(MonitorScheduler.NextRun(monitor with { Enabled = false }, latest, now));
        Assert.Equal(now, MonitorScheduler.NextRun(monitor, null, now));
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(15), MonitorScheduler.NextRun(monitor, latest, now));
    }
}
