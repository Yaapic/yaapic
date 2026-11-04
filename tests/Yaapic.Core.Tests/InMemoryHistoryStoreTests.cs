using Yaapic.Core.Storage;
using Yaapic.Domain.History;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class InMemoryHistoryStoreTests
{
    static readonly CancellationToken Ct = CancellationToken.None;
    static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    static HistoryEntry Entry(DateTimeOffset at, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), at, HttpRequestDefinition.Create("r"), 200, "OK", TimeSpan.Zero, 0, null, null);

    [Fact]
    public async Task List_is_newest_first()
    {
        var store = new InMemoryHistoryStore();
        var old = Entry(T0);
        var recent = Entry(T0.AddMinutes(1));
        await store.AppendAsync(old, Ct);
        await store.AppendAsync(recent, Ct);

        Assert.Equal([recent.Id, old.Id], (await store.ListAsync(Ct)).Select(e => e.Id));
    }

    [Fact]
    public async Task Equal_times_are_ordered_by_id_descending()
    {
        var store = new InMemoryHistoryStore();
        var low = Entry(T0, new Guid(1, 0, 0, new byte[8]));
        var high = Entry(T0, new Guid(2, 0, 0, new byte[8]));
        await store.AppendAsync(low, Ct);
        await store.AppendAsync(high, Ct);

        Assert.Equal([high.Id, low.Id], (await store.ListAsync(Ct)).Select(e => e.Id));
    }

    [Fact]
    public async Task Retention_drops_the_oldest_beyond_the_cap()
    {
        var store = new InMemoryHistoryStore();
        for (var i = 0; i < IHistoryStore.MaxEntries + 1; i++)
        {
            await store.AppendAsync(Entry(T0.AddMinutes(i)), Ct);
        }

        var list = await store.ListAsync(Ct);
        Assert.Equal(IHistoryStore.MaxEntries, list.Count);
        Assert.Equal(T0.AddMinutes(IHistoryStore.MaxEntries), list[0].At);
        Assert.Equal(T0.AddMinutes(1), list[^1].At);
    }

    [Fact]
    public async Task Delete_removes_one_entry_and_unknown_id_is_a_no_op()
    {
        var store = new InMemoryHistoryStore();
        var a = Entry(T0);
        var b = Entry(T0.AddMinutes(1));
        await store.AppendAsync(a, Ct);
        await store.AppendAsync(b, Ct);

        await store.DeleteAsync(Guid.NewGuid(), Ct);
        Assert.Equal(2, (await store.ListAsync(Ct)).Count);

        await store.DeleteAsync(a.Id, Ct);
        Assert.Equal([b.Id], (await store.ListAsync(Ct)).Select(e => e.Id));
    }

    [Fact]
    public async Task Clear_removes_everything()
    {
        var store = new InMemoryHistoryStore();
        await store.AppendAsync(Entry(T0), Ct);
        await store.ClearAsync(Ct);

        Assert.Empty(await store.ListAsync(Ct));
    }

    [Fact]
    public async Task Failure_message_makes_calls_throw()
    {
        var store = new InMemoryHistoryStore { FailureMessage = "disk" };

        await Assert.ThrowsAsync<StorageException>(() => store.ListAsync(Ct));
    }
}
