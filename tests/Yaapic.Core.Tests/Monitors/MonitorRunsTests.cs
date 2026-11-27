using Yaapic.Core.Monitors;
using Yaapic.Core.Runner;
using Yaapic.Core.Storage;
using Yaapic.Domain.Http;
using Yaapic.Domain.Monitors;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Monitors;

public class MonitorRunsTests
{
    private static readonly Guid Id = Guid.NewGuid();

    private static RunRequestResult Req(string name, RunRequestOutcome outcome, int? status = 200, string? error = null, params Yaapic.Domain.Scripting.TestResult[] tests) =>
        new(Guid.NewGuid(), name, "", HttpMethodKind.Get, "u", outcome, status, TimeSpan.FromMilliseconds(5), 0, tests, [], error);

    private static RunResult Run(RunStatus status, string? error, params RunRequestResult[] reqs) =>
        new("r", DateTimeOffset.UnixEpoch, TimeSpan.FromMilliseconds(1234.9), status, error, RunOptions.Default, 0, [new RunIterationResult(0, reqs)]);

    private static Yaapic.Domain.Scripting.TestResult T(string name, bool passed, string? message = null) => new(name, passed, message, TimeSpan.Zero, "test");

    [Fact]
    public void Passing_run()
    {
        var run = MonitorRuns.From(Id, Run(RunStatus.Completed, null, Req("a", RunRequestOutcome.Passed, tests: T("ok", true))));
        Assert.Equal(MonitorRunStatus.Passed, run.Status);
        Assert.Equal(1, run.Requests);
        Assert.Equal(0, run.FailedRequests);
        Assert.Equal(1, run.TestsPassed);
        Assert.Equal(1234, run.DurationMs);
        Assert.Equal(DateTimeOffset.UnixEpoch, run.At);
        Assert.Null(run.Message);
        Assert.Empty(run.Failures);
    }

    [Fact]
    public void Failed_test_gives_detail_with_message()
    {
        var run = MonitorRuns.From(Id, Run(RunStatus.Completed, null, Req("a", RunRequestOutcome.Failed, 500, null, T("p", true), T("status is 200", false, "got 500"))));
        Assert.Equal(MonitorRunStatus.Failed, run.Status);
        Assert.Equal(1, run.TestsFailed);
        Assert.Equal("status is 200: got 500", run.Failures[0].Detail);
        Assert.Equal(500, run.Failures[0].StatusCode);
        Assert.Equal("Failed", run.Failures[0].Outcome);
        Assert.Equal("a: status is 200: got 500", run.Message);
    }

    [Fact]
    public void Failed_test_without_message_uses_the_name()
    {
        var run = MonitorRuns.From(Id, Run(RunStatus.Completed, null, Req("a", RunRequestOutcome.Failed, 200, null, T("t", false))));
        Assert.Equal("t", run.Failures[0].Detail);
    }

    [Fact]
    public void Error_request_uses_the_error_text()
    {
        var run = MonitorRuns.From(Id, Run(RunStatus.Completed, null, Req("a", RunRequestOutcome.Error, null, "boom")));
        Assert.Equal(MonitorRunStatus.Failed, run.Status);
        Assert.Equal(1, run.FailedRequests);
        Assert.Equal("boom", run.Failures[0].Detail);
        Assert.Equal("a: boom", run.Message);
    }

    [Fact]
    public void Skipped_only_run_passes_with_no_requests()
    {
        var run = MonitorRuns.From(Id, Run(RunStatus.Completed, null, Req("a", RunRequestOutcome.Skipped, null)));
        Assert.Equal(MonitorRunStatus.Passed, run.Status);
        Assert.Equal(0, run.Requests);
    }

    [Fact]
    public void Failed_run_status_is_an_error_with_its_message()
    {
        var run = MonitorRuns.From(Id, Run(RunStatus.Failed, "loop"));
        Assert.Equal(MonitorRunStatus.Error, run.Status);
        Assert.Equal("loop", run.Message);
    }

    [Fact]
    public void Failures_are_capped_at_20_in_order()
    {
        RunRequestResult[] reqs = [.. Enumerable.Range(0, 25).Select(i => Req($"r{i}", RunRequestOutcome.Error, null, "e"))];
        var run = MonitorRuns.From(Id, Run(RunStatus.Completed, null, reqs));
        Assert.Equal(20, run.Failures.Count);
        Assert.Equal("r0", run.Failures[0].RequestName);
        Assert.Equal("r19", run.Failures[19].RequestName);
        Assert.Equal(25, run.FailedRequests);
    }

    [Fact]
    public void Error_factory_builds_an_error_run()
    {
        var run = MonitorRuns.Error(Id, DateTimeOffset.UnixEpoch, "no folder");
        Assert.Equal(MonitorRunStatus.Error, run.Status);
        Assert.Equal("no folder", run.Message);
        Assert.Empty(run.Failures);
    }

    [Fact]
    public async Task Run_store_keeps_200_newest_first()
    {
        var store = new InMemoryMonitorRunStore();
        for (int i = 0; i < 205; i++)
        {
            await store.AppendAsync(MonitorRuns.Error(Id, DateTimeOffset.UnixEpoch.AddMinutes(i), "m"), TestContext.Current.CancellationToken);
        }

        await store.AppendAsync(MonitorRuns.Error(Guid.NewGuid(), DateTimeOffset.UnixEpoch, "other"), TestContext.Current.CancellationToken);
        var all = await store.ListAsync(Id, 1000, TestContext.Current.CancellationToken);
        Assert.Equal(IMonitorRunStore.MaxRunsPerMonitor, all.Count);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(204), all[0].At);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(5), all[^1].At);
        Assert.Equal(3, (await store.ListAsync(Id, 3, TestContext.Current.CancellationToken)).Count);
        Assert.Equal(all[0], await store.LatestAsync(Id, TestContext.Current.CancellationToken));
        await store.DeleteAsync(Id, TestContext.Current.CancellationToken);
        Assert.Null(await store.LatestAsync(Id, TestContext.Current.CancellationToken));
    }
}
