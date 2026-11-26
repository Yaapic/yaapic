using Yaapic.Domain.Mocks;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Storage;

public sealed class InMemoryMockServerStoreTests
{
    [Fact]
    public async Task SaveReplacesByIdListIsSortedAndDeleteIsNoOpForUnknownId()
    {
        var store = new InMemoryMockServerStore();
        var b = MockServerDefinition.Create("b");
        await store.SaveAsync(b, CancellationToken.None);
        await store.SaveAsync(MockServerDefinition.Create("A"), CancellationToken.None);
        await store.SaveAsync(b with { Port = 4000 }, CancellationToken.None);
        await store.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        var list = await store.ListAsync(CancellationToken.None);
        Assert.Equal(["A", "b"], list.Select(s => s.Name));
        Assert.Equal(4000, list[1].Port);

        await store.DeleteAsync(b.Id, CancellationToken.None);
        Assert.Single(await store.ListAsync(CancellationToken.None));
    }
}
