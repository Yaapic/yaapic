using System.Text;
using Yaapic.Core.Storage;
using Yaapic.Domain.History;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class FileHistoryStoreTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
    private string HistoryPath => Path.Combine(_root, ".yaapic", "history.jsonl");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static HistoryEntry Success(DateTimeOffset at) =>
        new(Guid.NewGuid(), at, HttpRequestDefinition.Create("ok") with { Url = "https://example.test/a" }, 404, "Not Found", TimeSpan.FromMilliseconds(123), 4567, null, null);

    private static HistoryEntry Failure(DateTimeOffset at) =>
        new(Guid.NewGuid(), at, HttpRequestDefinition.Create("ko"), null, "", TimeSpan.Zero, 0, SendFailureKind.Timeout, "boom \"quoted\"\nline");

    [Fact]
    public async Task SuccessAndFailureRoundTrip()
    {
        var ok = Success(T0);
        var ko = Failure(T0.AddMinutes(1));
        var store = new FileHistoryStore(_root);
        await store.AppendAsync(ok, Ct);
        await store.AppendAsync(ko, Ct);

        var list = await new FileHistoryStore(_root).ListAsync(Ct);
        Assert.Equal(2, list.Count);
        Assert.True(list[0].IsEquivalentTo(ko));
        Assert.True(list[1].IsEquivalentTo(ok));
        Assert.Equal(2, File.ReadAllLines(HistoryPath).Length);
    }

    [Fact]
    public async Task ListIsNewestFirstWhateverTheAppendOrder()
    {
        var a = Success(T0.AddMinutes(5));
        var b = Success(T0);
        var store = new FileHistoryStore(_root);
        await store.AppendAsync(a, Ct);
        await store.AppendAsync(b, Ct);

        Assert.Equal([a.Id, b.Id], (await store.ListAsync(Ct)).Select(e => e.Id));
    }

    [Fact]
    public async Task MissingDirectoryAndFileListEmpty() =>
        Assert.Empty(await new FileHistoryStore(_root).ListAsync(Ct));

    [Fact]
    public async Task CorruptLineInTheMiddleIsSkipped()
    {
        var a = Success(T0);
        var c = Success(T0.AddMinutes(1));
        var store = new FileHistoryStore(_root);
        await store.AppendAsync(a, Ct);
        File.AppendAllText(HistoryPath, "{ not json\n{\"id\":\"x\"}\n");
        await store.AppendAsync(c, Ct);

        Assert.Equal([c.Id, a.Id], (await store.ListAsync(Ct)).Select(e => e.Id));
    }

    [Fact]
    public async Task AppendAfterUnterminatedLineKeepsLinesApart()
    {
        var a = Success(T0);
        Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);
        File.WriteAllText(HistoryPath, "{ truncated");
        await new FileHistoryStore(_root).AppendAsync(a, Ct);

        Assert.Equal([a.Id], (await new FileHistoryStore(_root).ListAsync(Ct)).Select(e => e.Id));
    }

    [Fact]
    public async Task Retention551AppendsLeave500Newest()
    {
        var store = new FileHistoryStore(_root);
        var all = Enumerable.Range(0, 551).Select(i => Success(T0.AddMinutes(i))).ToList();
        foreach (var e in all)
        {
            await store.AppendAsync(e, Ct);
        }

        Assert.Equal(500, File.ReadAllLines(HistoryPath).Length);
        var list = await store.ListAsync(Ct);
        Assert.Equal(500, list.Count);
        Assert.Equal(all.Skip(51).Reverse().Select(e => e.Id), list.Select(e => e.Id));
        Assert.False(File.Exists(HistoryPath + ".tmp"));
    }

    [Fact]
    public async Task DeleteRemovesOneEntryAndUnknownIdIsNoOp()
    {
        var a = Success(T0);
        var b = Success(T0.AddMinutes(1));
        var store = new FileHistoryStore(_root);
        await store.AppendAsync(a, Ct);
        await store.AppendAsync(b, Ct);

        await store.DeleteAsync(Guid.NewGuid(), Ct);
        Assert.Equal(2, (await store.ListAsync(Ct)).Count);
        await store.DeleteAsync(a.Id, Ct);
        Assert.Equal([b.Id], (await store.ListAsync(Ct)).Select(e => e.Id));
        Assert.False(File.Exists(HistoryPath + ".tmp"));
    }

    [Fact]
    public async Task ClearDeletesTheFileAndAppendWorksAfter()
    {
        var store = new FileHistoryStore(_root);
        await store.ClearAsync(Ct);
        await store.AppendAsync(Success(T0), Ct);
        await store.ClearAsync(Ct);

        Assert.False(File.Exists(HistoryPath));
        Assert.Empty(await store.ListAsync(Ct));
        await store.AppendAsync(Success(T0), Ct);
        Assert.Single(await store.ListAsync(Ct));
    }

    [Fact]
    public async Task ConcurrentAppendsLoseNothingAndNeverInterleave()
    {
        var store = new FileHistoryStore(_root);
        var entries = Enumerable.Range(0, 20).Select(i => Success(T0.AddMinutes(i))).ToList();
        await Task.WhenAll(entries.Select(e => Task.Run(() => store.AppendAsync(e, Ct))));

        Assert.Equal(20, File.ReadAllLines(HistoryPath).Length);
        Assert.Equal(entries.Select(e => e.Id).Order(), (await store.ListAsync(Ct)).Select(e => e.Id).Order());
    }

    [Fact]
    public async Task FileHasNoBomAndNoCarriageReturns()
    {
        await new FileHistoryStore(_root).AppendAsync(Failure(T0), Ct);

        var bytes = File.ReadAllBytes(HistoryPath);
        Assert.NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.Contains("\"statusCode\":null", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task IoFailureBecomesStorageException()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);
        Directory.CreateDirectory(HistoryPath);
        await Assert.ThrowsAsync<StorageException>(() => new FileHistoryStore(_root).AppendAsync(Success(T0), Ct));
    }
}
