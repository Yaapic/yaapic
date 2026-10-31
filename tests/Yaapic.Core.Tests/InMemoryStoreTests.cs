using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Workspace;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class InMemoryStoreTests
{
    static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Requests_are_sorted_by_name_ignoring_case_then_id()
    {
        var store = new InMemoryRequestStore();
        var b = HttpRequestDefinition.Create("b");
        var a1 = HttpRequestDefinition.Create("A");
        var a2 = HttpRequestDefinition.Create("a");
        foreach (var r in new[] { b, a1, a2 }) await store.SaveAsync(r, Ct);

        var list = await store.ListAsync(Ct);
        Assert.Equal(["A", "a", "b"], list.Select(r => r.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("b", list[2].Name);
        Assert.Equal(new[] { a1.Id, a2.Id }.Order(), list.Take(2).Select(r => r.Id));
    }

    [Fact]
    public async Task Request_save_replaces_by_id_and_counts()
    {
        var store = new InMemoryRequestStore();
        var r = HttpRequestDefinition.Create("x");
        await store.SaveAsync(r, Ct);
        await store.SaveAsync(r with { Url = "http://a" }, Ct);

        var list = await store.ListAsync(Ct);
        Assert.Equal("http://a", Assert.Single(list).Url);
        Assert.Equal(2, store.SaveCount);
    }

    [Fact]
    public async Task Request_delete_removes_and_unknown_id_is_noop()
    {
        var store = new InMemoryRequestStore();
        var r = HttpRequestDefinition.Create("x");
        await store.SaveAsync(r, Ct);
        await store.DeleteAsync(Guid.NewGuid(), Ct);
        Assert.Single(await store.ListAsync(Ct));
        await store.DeleteAsync(r.Id, Ct);
        Assert.Empty(await store.ListAsync(Ct));
    }

    [Fact]
    public async Task Environments_are_sorted_replaced_and_deleted()
    {
        var store = new InMemoryEnvironmentStore();
        var b = EnvironmentDefinition.Create("b");
        var a = EnvironmentDefinition.Create("A");
        await store.SaveAsync(b, Ct);
        await store.SaveAsync(a, Ct);
        await store.SaveAsync(a with { Name = "C" }, Ct);

        Assert.Equal(["b", "C"], (await store.ListAsync(Ct)).Select(e => e.Name));
        Assert.Equal(3, store.SaveCount);
        await store.DeleteAsync(Guid.NewGuid(), Ct);
        await store.DeleteAsync(b.Id, Ct);
        Assert.Equal(["C"], (await store.ListAsync(Ct)).Select(e => e.Name));
    }

    [Fact]
    public async Task Session_defaults_to_empty_and_round_trips()
    {
        var store = new InMemorySessionStore();
        Assert.Same(SessionState.Empty, await store.LoadAsync(Ct));

        var state = new SessionState([Guid.NewGuid()], null, Guid.NewGuid());
        await store.SaveAsync(state, Ct);
        Assert.Same(state, await store.LoadAsync(Ct));
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Fake_transport_records_requests_and_returns_the_reply()
    {
        var expected = SendResult.Fail(SendFailureKind.Timeout, "slow");
        var transport = new FakeHttpTransport((_, _) => Task.FromResult(expected));
        var request = new Yaapic.Core.Http.ResolvedRequest(HttpMethodKind.Get, "http://a", [], null, null, []);

        Assert.Same(expected, await transport.SendAsync(request, Ct));
        Assert.Same(request, Assert.Single(transport.Requests));
    }
}
