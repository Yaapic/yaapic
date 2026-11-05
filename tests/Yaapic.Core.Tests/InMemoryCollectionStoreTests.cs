using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class InMemoryCollectionStoreTests
{
    static readonly CancellationToken Ct = CancellationToken.None;

    readonly InMemoryRequestStore _requests = new();
    readonly InMemoryCollectionStore _store;

    public InMemoryCollectionStoreTests() => _store = new(_requests);

    async Task<FolderDefinition> AddFolder(string name, Guid? parent = null)
    {
        var f = FolderDefinition.Create(name);
        await _store.SaveFolderAsync(f, parent, Ct);
        return f;
    }

    async Task<HttpRequestDefinition> AddRequest(string name)
    {
        var r = HttpRequestDefinition.Create(name);
        await _requests.SaveAsync(r, Ct);
        return r;
    }

    [Fact]
    public async Task Empty_store_has_an_empty_tree() => Assert.Empty(await _store.LoadTreeAsync(Ct));

    [Fact]
    public async Task Creates_top_level_and_nested_folders()
    {
        var a = await AddFolder("a");
        var b = await AddFolder("b", a.Id);

        var root = Assert.Single(await _store.LoadTreeAsync(Ct));
        Assert.Equal(a.Id, root.Id);
        Assert.Equal(b.Id, Assert.Single(root.Children).Id);
    }

    [Fact]
    public async Task Creating_under_an_unknown_parent_throws() =>
        await Assert.ThrowsAsync<StorageException>(() => AddFolder("a", Guid.NewGuid()));

    [Fact]
    public async Task Update_keeps_the_location_and_ignores_the_parent()
    {
        var a = await AddFolder("a");
        var b = await AddFolder("b", a.Id);
        var other = await AddFolder("other");

        await _store.SaveFolderAsync(b with { Name = "renamed", Description = "d" }, other.Id, Ct);

        var root = (await _store.LoadTreeAsync(Ct)).Single(n => n.Id == a.Id);
        var child = Assert.Single(root.Children);
        Assert.Equal("renamed", child.Name);
        Assert.Equal("d", child.Folder!.Description);
    }

    [Fact]
    public async Task Unfiled_request_appears_at_the_root()
    {
        var r = await AddRequest("r");
        var f = await AddFolder("f");

        var tree = await _store.LoadTreeAsync(Ct);
        Assert.Equal([f.Id, r.Id], tree.Select(n => n.Id));
    }

    [Fact]
    public async Task Moves_a_request_and_a_folder()
    {
        var a = await AddFolder("a");
        var b = await AddFolder("b");
        var r = await AddRequest("r");

        await _store.MoveAsync(r.Id, a.Id, Ct);
        await _store.MoveAsync(b.Id, a.Id, Ct);

        var root = Assert.Single(await _store.LoadTreeAsync(Ct));
        Assert.Equal([b.Id, r.Id], root.Children.Select(n => n.Id));

        await _store.MoveAsync(r.Id, null, Ct);
        await _store.MoveAsync(r.Id, null, Ct);
        Assert.Contains(await _store.LoadTreeAsync(Ct), n => n.Id == r.Id);
    }

    [Fact]
    public async Task Moving_a_folder_into_itself_or_a_descendant_throws()
    {
        var a = await AddFolder("a");
        var b = await AddFolder("b", a.Id);

        await Assert.ThrowsAsync<StorageException>(() => _store.MoveAsync(a.Id, a.Id, Ct));
        await Assert.ThrowsAsync<StorageException>(() => _store.MoveAsync(a.Id, b.Id, Ct));
    }

    [Fact]
    public async Task Move_with_unknown_ids_throws()
    {
        var a = await AddFolder("a");
        await Assert.ThrowsAsync<StorageException>(() => _store.MoveAsync(Guid.NewGuid(), null, Ct));
        await Assert.ThrowsAsync<StorageException>(() => _store.MoveAsync(a.Id, Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Delete_removes_nested_folders_and_requests_and_unknown_id_is_noop()
    {
        var a = await AddFolder("a");
        var b = await AddFolder("b", a.Id);
        var inside = await AddRequest("inside");
        var outside = await AddRequest("outside");
        await _store.MoveAsync(inside.Id, b.Id, Ct);

        await _store.DeleteFolderAsync(Guid.NewGuid(), Ct);
        await _store.DeleteFolderAsync(a.Id, Ct);

        Assert.Equal(outside.Id, Assert.Single(await _store.LoadTreeAsync(Ct)).Id);
        Assert.Equal(outside.Id, Assert.Single(await _requests.ListAsync(Ct)).Id);
    }

    [Fact]
    public async Task Roots_are_sorted_folders_first_then_by_name()
    {
        var z = await AddFolder("z");
        var a = await AddFolder("A");
        var r = await AddRequest("0");

        Assert.Equal([a.Id, z.Id, r.Id], (await _store.LoadTreeAsync(Ct)).Select(n => n.Id));
    }
}
