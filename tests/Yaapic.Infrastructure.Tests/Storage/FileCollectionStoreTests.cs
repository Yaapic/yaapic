using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class FileCollectionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
    private string Requests => Path.Combine(_root, "requests");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string P(params string[] parts) => Path.Combine([Requests, .. parts]);

    private static Task WriteRequest(string path, HttpRequestDefinition request) =>
        WorkspaceFiles.WriteAtomicAsync(path, RequestYamlSerializer.Serialize(request), CancellationToken.None);

    [Fact]
    public async Task EmptyWorkspaceGivesEmptyTree()
    {
        var store = new FileCollectionStore(_root);
        Assert.Empty(await store.LoadTreeAsync(CancellationToken.None));
        Assert.Empty(store.LastLoadErrors);
    }

    [Fact]
    public async Task CreatesTopLevelAndNestedFolders()
    {
        var store = new FileCollectionStore(_root);
        var top = FolderDefinition.Create("My API");
        var nested = FolderDefinition.Create("Users");
        await store.SaveFolderAsync(top, null, CancellationToken.None);
        await store.SaveFolderAsync(nested, top.Id, CancellationToken.None);

        Assert.True(File.Exists(P("my-api", "folder.yaml")));
        Assert.Equal(FolderYamlSerializer.Serialize(nested), File.ReadAllText(P("my-api", "users", "folder.yaml")));
        var root = Assert.Single(await store.LoadTreeAsync(CancellationToken.None));
        Assert.Equal(nested.Id, Assert.Single(root.Children).Id);
    }

    [Fact]
    public async Task TreeContainsUnfiledAndNestedRequestsInOrder()
    {
        var folder = FolderDefinition.Create("F");
        var store = new FileCollectionStore(_root);
        await store.SaveFolderAsync(folder, null, CancellationToken.None);
        var inner = HttpRequestDefinition.Create("Inner");
        var loose = HttpRequestDefinition.Create("Loose");
        await WriteRequest(P("f", "inner.request.yaml"), inner);
        await WriteRequest(P("loose.request.yaml"), loose);

        var tree = await store.LoadTreeAsync(CancellationToken.None);
        Assert.Equal([folder.Id, loose.Id], tree.Select(n => n.Id));
        Assert.Equal(inner.Id, Assert.Single(tree[0].Children).Id);
    }

    [Fact]
    public async Task RenameMovesDirectoryAndKeepsRequests()
    {
        var folder = FolderDefinition.Create("Old");
        var store = new FileCollectionStore(_root);
        await store.SaveFolderAsync(folder, null, CancellationToken.None);
        await WriteRequest(P("old", "r.request.yaml"), HttpRequestDefinition.Create("R"));

        await store.SaveFolderAsync(folder with { Name = "New" }, null, CancellationToken.None);

        Assert.False(Directory.Exists(P("old")));
        Assert.True(File.Exists(P("new", "r.request.yaml")));
        var node = Assert.Single(await store.LoadTreeAsync(CancellationToken.None));
        Assert.Equal("New", node.Name);
        Assert.Single(node.Children);
    }

    private static readonly ScriptSet Scripts = new(
        new ScriptBlock(ScriptLanguage.JavaScript, "yaapic.environment.set('a', '1');"),
        new ScriptBlock(ScriptLanguage.Lua, "print('x')"));

    [Fact]
    public async Task SaveRenameAndMoveKeepFolderScripts()
    {
        var store = new FileCollectionStore(_root);
        var top = FolderDefinition.Create("Top") with { Scripts = Scripts };
        var nested = FolderDefinition.Create("Nested") with { Scripts = Scripts };
        var other = FolderDefinition.Create("Other");
        await store.SaveFolderAsync(top, null, CancellationToken.None);
        await store.SaveFolderAsync(nested, top.Id, CancellationToken.None);
        await store.SaveFolderAsync(other, null, CancellationToken.None);

        await store.SaveFolderAsync(nested with { Name = "Renamed" }, top.Id, CancellationToken.None);
        var tree = await store.LoadTreeAsync(CancellationToken.None);
        Assert.Equal(Scripts, tree.Single(n => n.Id == top.Id).Children.Single().Folder!.Scripts);

        await store.MoveAsync(nested.Id, other.Id, CancellationToken.None);
        tree = await store.LoadTreeAsync(CancellationToken.None);
        Assert.Equal(Scripts, tree.Single(n => n.Id == top.Id).Folder!.Scripts);
        var moved = tree.Single(n => n.Id == other.Id).Children.Single();
        Assert.Equal("Renamed", moved.Name);
        Assert.Equal(Scripts, moved.Folder!.Scripts);
    }

    [Fact]
    public async Task SlugCollisionGetsSuffix()
    {
        var store = new FileCollectionStore(_root);
        await store.SaveFolderAsync(FolderDefinition.Create("Dup"), null, CancellationToken.None);
        await store.SaveFolderAsync(FolderDefinition.Create("Dup"), null, CancellationToken.None);

        Assert.True(Directory.Exists(P("dup")));
        Assert.True(Directory.Exists(P("dup-2")));
    }

    [Fact]
    public async Task UpdatesVariablesAndDescriptionInPlace()
    {
        var folder = FolderDefinition.Create("F");
        var store = new FileCollectionStore(_root);
        await store.SaveFolderAsync(folder, null, CancellationToken.None);
        var updated = folder with { Description = "d", Variables = [new KeyValueEntry("k", "v", true)] };
        await store.SaveFolderAsync(updated, null, CancellationToken.None);

        Assert.Single(Directory.GetDirectories(Requests));
        var node = Assert.Single(await store.LoadTreeAsync(CancellationToken.None));
        Assert.True(updated.IsEquivalentTo(node.Folder!));
    }

    [Fact]
    public async Task DeleteRemovesNestedContent()
    {
        var top = FolderDefinition.Create("Top");
        var store = new FileCollectionStore(_root);
        await store.SaveFolderAsync(top, null, CancellationToken.None);
        await store.SaveFolderAsync(FolderDefinition.Create("Sub"), top.Id, CancellationToken.None);
        await WriteRequest(P("top", "sub", "r.request.yaml"), HttpRequestDefinition.Create("R"));

        await store.DeleteFolderAsync(top.Id, CancellationToken.None);

        Assert.Empty(Directory.GetFileSystemEntries(Requests));
    }

    [Fact]
    public async Task UnknownIdsAndParents()
    {
        var store = new FileCollectionStore(_root);
        await store.DeleteFolderAsync(Guid.NewGuid(), CancellationToken.None);
        await Assert.ThrowsAsync<StorageException>(() =>
            store.SaveFolderAsync(FolderDefinition.Create("X"), Guid.NewGuid(), CancellationToken.None));
    }

    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task MovesRequestIntoFolderAndBack()
    {
        var store = new FileCollectionStore(_root);
        var folder = FolderDefinition.Create("F");
        var r = HttpRequestDefinition.Create("R");
        await store.SaveFolderAsync(folder, null, Ct);
        await WriteRequest(P("r.request.yaml"), r);

        await store.MoveAsync(r.Id, folder.Id, Ct);
        Assert.True(File.Exists(P("f", "r.request.yaml")));
        Assert.False(File.Exists(P("r.request.yaml")));

        await store.MoveAsync(r.Id, null, Ct);
        Assert.True(File.Exists(P("r.request.yaml")));
        Assert.False(File.Exists(P("f", "r.request.yaml")));
    }

    [Fact]
    public async Task MovesFolderWithItsRequests()
    {
        var store = new FileCollectionStore(_root);
        var a = FolderDefinition.Create("A");
        var b = FolderDefinition.Create("B");
        await store.SaveFolderAsync(a, null, Ct);
        await store.SaveFolderAsync(b, null, Ct);
        await WriteRequest(P("a", "r.request.yaml"), HttpRequestDefinition.Create("R"));

        await store.MoveAsync(a.Id, b.Id, Ct);

        Assert.True(File.Exists(P("b", "a", "r.request.yaml")));
        Assert.False(Directory.Exists(P("a")));
        var root = Assert.Single(await store.LoadTreeAsync(Ct));
        Assert.Single(Assert.Single(root.Children).Children);
    }

    [Fact]
    public async Task NameCollisionAtTargetGetsSuffix()
    {
        var store = new FileCollectionStore(_root);
        var f = FolderDefinition.Create("F");
        var r = HttpRequestDefinition.Create("R");
        var inner = FolderDefinition.Create("Dup");
        await store.SaveFolderAsync(f, null, Ct);
        await store.SaveFolderAsync(inner, f.Id, Ct);
        await store.SaveFolderAsync(FolderDefinition.Create("Dup"), null, Ct);
        await WriteRequest(P("f", "r.request.yaml"), HttpRequestDefinition.Create("R"));
        await WriteRequest(P("r.request.yaml"), r);

        await store.MoveAsync(r.Id, f.Id, Ct);
        Assert.True(File.Exists(P("f", "r-2.request.yaml")));

        await store.MoveAsync(inner.Id, null, Ct);
        Assert.True(Directory.Exists(P("dup-2")));
    }

    [Fact]
    public async Task MovingFolderIntoItselfOrDescendantThrowsAndChangesNothing()
    {
        var store = new FileCollectionStore(_root);
        var a = FolderDefinition.Create("A");
        var b = FolderDefinition.Create("B");
        await store.SaveFolderAsync(a, null, Ct);
        await store.SaveFolderAsync(b, a.Id, Ct);

        await Assert.ThrowsAsync<StorageException>(() => store.MoveAsync(a.Id, a.Id, Ct));
        await Assert.ThrowsAsync<StorageException>(() => store.MoveAsync(a.Id, b.Id, Ct));

        Assert.True(File.Exists(P("a", "b", "folder.yaml")));
    }

    [Fact]
    public async Task UnknownIdAndTargetThrow()
    {
        var store = new FileCollectionStore(_root);
        var a = FolderDefinition.Create("A");
        await store.SaveFolderAsync(a, null, Ct);

        await Assert.ThrowsAsync<StorageException>(() => store.MoveAsync(Guid.NewGuid(), null, Ct));
        await Assert.ThrowsAsync<StorageException>(() => store.MoveAsync(a.Id, Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task SameParentMoveIsNoOp()
    {
        var store = new FileCollectionStore(_root);
        var a = FolderDefinition.Create("A");
        var r = HttpRequestDefinition.Create("R");
        await store.SaveFolderAsync(a, null, Ct);
        await WriteRequest(P("r.request.yaml"), r);

        await store.MoveAsync(a.Id, null, Ct);
        await store.MoveAsync(r.Id, null, Ct);

        Assert.True(File.Exists(P("a", "folder.yaml")));
        Assert.True(File.Exists(P("r.request.yaml")));
    }

    [Fact]
    public async Task RequestStoreListsMovedRequestsAfterScan()
    {
        var collections = new FileCollectionStore(_root);
        var f = FolderDefinition.Create("F");
        var r = HttpRequestDefinition.Create("R");
        await collections.SaveFolderAsync(f, null, Ct);
        await WriteRequest(P("r.request.yaml"), r);
        var requests = new FileRequestStore(_root);
        Assert.Single(await requests.ListAsync(Ct));

        await collections.MoveAsync(r.Id, f.Id, Ct);

        var listed = Assert.Single(await requests.ListAsync(Ct));
        Assert.Equal(r.Id, listed.Id);
        Assert.True(File.Exists(P("f", "r.request.yaml")));
    }

    [Fact]
    public async Task NoTmpFilesLeft()
    {
        var folder = FolderDefinition.Create("F");
        var store = new FileCollectionStore(_root);
        await store.SaveFolderAsync(folder, null, CancellationToken.None);
        await store.SaveFolderAsync(folder with { Name = "G" }, null, CancellationToken.None);

        Assert.Empty(Directory.GetFiles(Requests, "*.tmp", SearchOption.AllDirectories));
    }
}
