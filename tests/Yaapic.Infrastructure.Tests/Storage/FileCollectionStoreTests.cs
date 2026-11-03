using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
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
