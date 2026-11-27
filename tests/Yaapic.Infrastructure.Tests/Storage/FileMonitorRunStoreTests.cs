using Yaapic.Domain.Monitors;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class FileMonitorRunStoreTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
    private string RunsPath => Path.Combine(_root, ".yaapic", "monitor-runs.jsonl");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static MonitorRun Run(Guid id, int minute, MonitorRunStatus status = MonitorRunStatus.Passed) =>
        new(id, T0.AddMinutes(minute), 120, status, 3, 1, 5, 2, "msg \"q\"\nline", [new MonitorFailure("Get user", "TestFailed", 500, "expected 200")]);

    [Fact]
    public async Task AppendAndListNewestFirstRoundTrips()
    {
        var id = Guid.NewGuid();
        var first = Run(id, 1);
        var second = Run(id, 2, MonitorRunStatus.Failed);
        var store = new FileMonitorRunStore(_root);
        await store.AppendAsync(second, Ct);
        await store.AppendAsync(first, Ct);

        var list = await new FileMonitorRunStore(_root).ListAsync(id, 10, Ct);
        Assert.Equal(2, list.Count);
        Assert.Equal(second.At, list[0].At);
        Assert.Equal(MonitorRunStatus.Failed, list[0].Status);
        Assert.Equal(first.At, list[1].At);
        Assert.Equal(first.Message, list[1].Message);
        Assert.Equal(first.Failures, list[1].Failures);
        Assert.Single(await store.ListAsync(id, 1, Ct));
    }

    [Fact]
    public async Task Run201KeepsTheNewest200WithoutTouchingOtherMonitors()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var store = new FileMonitorRunStore(_root);
        await store.AppendAsync(Run(b, 0), Ct);
        for (var i = 0; i < 201; i++)
        {
            await store.AppendAsync(Run(a, i + 1), Ct);
        }

        var runsA = await store.ListAsync(a, 1000, Ct);
        Assert.Equal(200, runsA.Count);
        Assert.Equal(T0.AddMinutes(201), runsA[0].At);
        Assert.Equal(T0.AddMinutes(2), runsA[^1].At);
        Assert.Single(await store.ListAsync(b, 1000, Ct));
    }

    [Fact]
    public async Task LatestReturnsTheNewestOrNull()
    {
        var id = Guid.NewGuid();
        var store = new FileMonitorRunStore(_root);
        Assert.Null(await store.LatestAsync(id, Ct));
        await store.AppendAsync(Run(id, 1), Ct);
        await store.AppendAsync(Run(id, 3), Ct);
        await store.AppendAsync(Run(id, 2), Ct);
        Assert.Equal(T0.AddMinutes(3), (await store.LatestAsync(id, Ct))!.At);
    }

    [Fact]
    public async Task DeleteRemovesOnlyThatMonitor()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var store = new FileMonitorRunStore(_root);
        await store.AppendAsync(Run(a, 1), Ct);
        await store.AppendAsync(Run(b, 1), Ct);
        await store.DeleteAsync(a, Ct);
        await store.DeleteAsync(Guid.NewGuid(), Ct);

        Assert.Empty(await store.ListAsync(a, 10, Ct));
        Assert.Single(await store.ListAsync(b, 10, Ct));
    }

    [Fact]
    public async Task CorruptLinesAreSkipped()
    {
        var id = Guid.NewGuid();
        var store = new FileMonitorRunStore(_root);
        await store.AppendAsync(Run(id, 1), Ct);
        await File.AppendAllTextAsync(RunsPath, "{not json\n{\"monitorId\":\"x\"}\n", Ct);
        await store.AppendAsync(Run(id, 2), Ct);

        Assert.Equal(2, (await store.ListAsync(id, 10, Ct)).Count);
    }

    [Fact]
    public async Task FileLivesInsideTheGitIgnoredFolder()
    {
        await new FileMonitorRunStore(_root).AppendAsync(Run(Guid.NewGuid(), 1), Ct);
        Assert.True(File.Exists(RunsPath));
        Assert.Equal(".yaapic", Path.GetFileName(Path.GetDirectoryName(RunsPath)));
    }

    [Fact]
    public async Task ParallelAppendsLoseNothing()
    {
        var id = Guid.NewGuid();
        var store = new FileMonitorRunStore(_root);
        await Task.WhenAll(Enumerable.Range(0, 40).Select(i => store.AppendAsync(Run(id, i), Ct)));

        Assert.Equal(40, (await store.ListAsync(id, 100, Ct)).Count);
    }
}
