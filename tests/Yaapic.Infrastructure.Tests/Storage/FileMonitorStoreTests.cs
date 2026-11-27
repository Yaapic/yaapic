using Yaapic.Domain.Monitors;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class FileMonitorStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
    private string Monitors => Path.Combine(_root, "monitors");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string[] FileNames() =>
        [.. Directory.GetFiles(Monitors).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal)];

    private static MonitorDefinition Sample() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"), "Nightly checks",
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"), 15, false);

    [Fact]
    public async Task RoundTripKeepsEveryField()
    {
        var monitor = Sample();
        await new FileMonitorStore(_root).SaveAsync(monitor, CancellationToken.None);

        var list = await new FileMonitorStore(_root).ListAsync(CancellationToken.None);
        Assert.Equal(["nightly-checks.monitor.yaml"], FileNames());
        Assert.Equal(monitor, Assert.Single(list));
    }

    [Fact]
    public async Task NoEnvironmentIsOmittedAndRoundTrips()
    {
        var monitor = Sample() with { EnvironmentId = null };
        await new FileMonitorStore(_root).SaveAsync(monitor, CancellationToken.None);

        Assert.DoesNotContain("environmentId", await File.ReadAllTextAsync(Path.Combine(Monitors, "nightly-checks.monitor.yaml"), CancellationToken.None));
        Assert.Equal(monitor, Assert.Single(await new FileMonitorStore(_root).ListAsync(CancellationToken.None)));
    }

    [Fact]
    public void YamlMatchesExpectedLiteral()
    {
        const string expected = """
            version: 1
            id: 11111111-1111-1111-1111-111111111111
            name: Nightly checks
            folderId: 22222222-2222-2222-2222-222222222222
            environmentId: 33333333-3333-3333-3333-333333333333
            intervalMinutes: 15
            enabled: false

            """;
        Assert.Equal(expected.Replace("\r\n", "\n").TrimEnd('\n') + "\n", MonitorYamlSerializer.Serialize(Sample()));
    }

    [Fact]
    public async Task RenameMovesFile()
    {
        var store = new FileMonitorStore(_root);
        var monitor = MonitorDefinition.Create("Old", Guid.NewGuid());
        await store.SaveAsync(monitor, CancellationToken.None);
        await store.SaveAsync(monitor with { Name = "New" }, CancellationToken.None);

        Assert.Equal(["new.monitor.yaml"], FileNames());
    }

    [Fact]
    public async Task SameNameGetsDistinctFiles()
    {
        var store = new FileMonitorStore(_root);
        await store.SaveAsync(MonitorDefinition.Create("Users", Guid.NewGuid()), CancellationToken.None);
        await store.SaveAsync(MonitorDefinition.Create("users", Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(["users-2.monitor.yaml", "users.monitor.yaml"], FileNames());
    }

    [Fact]
    public async Task DeleteRemovesFileAndUnknownIdIsNoOp()
    {
        var store = new FileMonitorStore(_root);
        var monitor = MonitorDefinition.Create("A", Guid.NewGuid());
        await store.SaveAsync(monitor, CancellationToken.None);
        await store.DeleteAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Single(FileNames());

        await store.DeleteAsync(monitor.Id, CancellationToken.None);
        Assert.Empty(FileNames());
    }

    [Fact]
    public async Task CorruptFileIsSkippedAndReported()
    {
        var store = new FileMonitorStore(_root);
        await store.SaveAsync(MonitorDefinition.Create("Good", Guid.NewGuid()), CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(Monitors, "bad.monitor.yaml"), "version: [", CancellationToken.None);

        Assert.Single(await store.ListAsync(CancellationToken.None));
        Assert.Equal(["bad.monitor.yaml"], store.LastListErrors);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(7, 10)]
    [InlineData(61, 180)]
    [InlineData(5000, 1440)]
    public async Task InvalidIntervalIsRaisedToNextAllowedAndReported(int stored, int expected)
    {
        var store = new FileMonitorStore(_root);
        var monitor = Sample();
        Directory.CreateDirectory(Monitors);
        var yaml = MonitorYamlSerializer.Serialize(monitor).Replace("intervalMinutes: 15", $"intervalMinutes: {stored}");
        await File.WriteAllTextAsync(Path.Combine(Monitors, "m.monitor.yaml"), yaml, CancellationToken.None);

        Assert.Equal(expected, Assert.Single(await store.ListAsync(CancellationToken.None)).IntervalMinutes);
        Assert.Equal(["m.monitor.yaml"], store.LastListErrors);
    }

    [Fact]
    public async Task ListIsSortedByNameIgnoringCase()
    {
        var store = new FileMonitorStore(_root);
        await store.SaveAsync(MonitorDefinition.Create("b", Guid.NewGuid()), CancellationToken.None);
        await store.SaveAsync(MonitorDefinition.Create("A", Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(["A", "b"], (await store.ListAsync(CancellationToken.None)).Select(s => s.Name));
    }
}
