using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class WorkspaceIndexTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string Dir(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static FolderDefinition WriteFolder(string dir, string name, Guid? id = null)
    {
        var folder = FolderDefinition.Create(name) with { Id = id ?? Guid.NewGuid() };
        File.WriteAllText(Path.Combine(dir, "folder.yaml"), FolderYamlSerializer.Serialize(folder));
        return folder;
    }

    private static HttpRequestDefinition WriteRequest(string dir, string name, Guid? id = null, string? file = null)
    {
        var request = HttpRequestDefinition.Create(name) with { Id = id ?? Guid.NewGuid() };
        File.WriteAllText(Path.Combine(dir, file ?? name + ".request.yaml"), RequestYamlSerializer.Serialize(request));
        return request;
    }

    [Fact]
    public void MissingRootGivesEmptyIndex()
    {
        var index = WorkspaceIndex.Scan(_root);
        Assert.Empty(index.Folders);
        Assert.Empty(index.Requests);
        Assert.Empty(index.Errors);
        Assert.Empty(index.BuildTree());
    }

    [Fact]
    public void EmptyRootGivesEmptyIndex()
    {
        Assert.Empty(WorkspaceIndex.Scan(Dir()).BuildTree());
    }

    [Fact]
    public void UnfiledRequestHasNoParent()
    {
        var request = WriteRequest(Dir(), "a");
        var index = WorkspaceIndex.Scan(_root);

        Assert.True(index.TryGetRequest(request.Id, out var found));
        Assert.Null(found.ParentId);
        Assert.Equal(Path.Combine(_root, "a.request.yaml"), found.FilePath);
        Assert.False(index.TryGetFolder(request.Id, out _));
    }

    [Fact]
    public void NestedFoldersAndRequestsAreIndexed()
    {
        var users = WriteFolder(Dir("users"), "Users");
        var admin = WriteFolder(Dir("users", "admin"), "Admin");
        var list = WriteRequest(Dir("users"), "list");
        var ban = WriteRequest(Dir("users", "admin"), "ban");
        var index = WorkspaceIndex.Scan(_root);

        Assert.True(index.TryGetFolder(admin.Id, out var indexedAdmin));
        Assert.Equal(users.Id, indexedAdmin.ParentId);
        Assert.Equal(Path.Combine(_root, "users", "admin"), indexedAdmin.DirectoryPath);
        Assert.True(index.TryGetRequest(ban.Id, out var indexedBan));
        Assert.Equal(admin.Id, indexedBan.ParentId);
        Assert.True(index.TryGetRequest(list.Id, out var indexedList));
        Assert.Equal(users.Id, indexedList.ParentId);
        Assert.Empty(index.Errors);
    }

    [Fact]
    public void TreeIsSorted()
    {
        WriteRequest(Dir(), "zeta");
        WriteRequest(Dir(), "alpha");
        WriteFolder(Dir("b"), "B");
        var a = WriteFolder(Dir("a"), "A");
        WriteRequest(Dir("a"), "inner");

        var tree = WorkspaceIndex.Scan(_root).BuildTree();

        Assert.Equal(["A", "B", "alpha", "zeta"], tree.Select(n => n.Name));
        Assert.Equal(a.Id, tree[0].Id);
        Assert.Equal(["inner"], tree[0].Children.Select(n => n.Name));
        Assert.Equal(CollectionNodeKind.Request, tree[2].Kind);
    }

    [Fact]
    public void DirectoryWithoutFolderFileIsSkippedWithSubtree()
    {
        Dir("loose", "deep");
        WriteFolder(Dir("loose", "deep"), "Deep");
        var hidden = WriteRequest(Path.Combine(_root, "loose"), "r");

        var index = WorkspaceIndex.Scan(_root);

        Assert.Empty(index.Folders);
        Assert.Empty(index.Requests);
        Assert.False(index.TryGetRequest(hidden.Id, out _));
        Assert.Single(index.Errors);
        Assert.StartsWith("loose:", index.Errors[0]);
    }

    [Fact]
    public void UnparsableFolderFileSkipsSubtree()
    {
        File.WriteAllText(Path.Combine(Dir("bad"), "folder.yaml"), "not: [valid");
        WriteRequest(Dir("bad"), "r");

        var index = WorkspaceIndex.Scan(_root);

        Assert.Empty(index.Requests);
        Assert.Single(index.Errors);
        Assert.StartsWith("bad/folder.yaml:", index.Errors[0]);
    }

    [Fact]
    public void CorruptRequestIsReportedAndOthersKept()
    {
        File.WriteAllText(Path.Combine(Dir(), "broken.request.yaml"), "garbage: [");
        var good = WriteRequest(Dir(), "good");

        var index = WorkspaceIndex.Scan(_root);

        Assert.Equal(good.Id, Assert.Single(index.Requests).Request.Id);
        Assert.Single(index.Errors);
        Assert.StartsWith("broken.request.yaml:", index.Errors[0]);
    }

    [Fact]
    public void DuplicateIdsFirstByOrdinalPathWins()
    {
        var id = Guid.NewGuid();
        WriteRequest(Dir(), "b", id);
        WriteRequest(Dir(), "a", id);
        var folder = WriteFolder(Dir("f"), "F");
        WriteRequest(Dir("f"), "clash", folder.Id);

        var index = WorkspaceIndex.Scan(_root);

        Assert.True(index.TryGetRequest(id, out var winner));
        Assert.EndsWith("a.request.yaml", winner.FilePath);
        Assert.Single(index.Requests);
        Assert.Equal(2, index.Errors.Count);
        Assert.Contains(index.Errors, e => e.StartsWith("b.request.yaml:", StringComparison.Ordinal));
        Assert.Contains(index.Errors, e => e.StartsWith("f/clash.request.yaml:", StringComparison.Ordinal));
    }

    [Fact]
    public void HiddenEntriesAndOtherFilesAreIgnored()
    {
        WriteFolder(Dir(".git"), "Hidden");
        WriteRequest(Dir(), "x", file: ".hidden.request.yaml");
        File.WriteAllText(Path.Combine(Dir(), "notes.txt"), "hello");
        File.WriteAllText(Path.Combine(Dir(), "req.yaml"), "hello");

        var index = WorkspaceIndex.Scan(_root);

        Assert.Empty(index.Folders);
        Assert.Empty(index.Requests);
        Assert.Empty(index.Errors);
    }

    [Fact]
    public void UniqueNameReturnsSlugWhenFree()
    {
        Assert.Equal("users.request.yaml", WorkspaceFiles.UniqueName(Dir(), "users", ".request.yaml", null));
        Assert.Equal("users", WorkspaceFiles.UniqueName(Path.Combine(_root, "missing"), "users", "", null));
    }

    [Fact]
    public void UniqueNameAddsNumericSuffixOnCollision()
    {
        var dir = Dir();
        File.WriteAllText(Path.Combine(dir, "users.request.yaml"), "");
        Assert.Equal("users-2.request.yaml", WorkspaceFiles.UniqueName(dir, "users", ".request.yaml", null));

        File.WriteAllText(Path.Combine(dir, "users-2.request.yaml"), "");
        Assert.Equal("users-3.request.yaml", WorkspaceFiles.UniqueName(dir, "users", ".request.yaml", null));
    }

    [Fact]
    public void UniqueNameIgnoresOwnPath()
    {
        var dir = Dir();
        var own = Path.Combine(dir, "users.request.yaml");
        File.WriteAllText(own, "");
        Assert.Equal("users.request.yaml", WorkspaceFiles.UniqueName(dir, "users", ".request.yaml", own));
    }

    [Fact]
    public void UniqueNameIsCaseInsensitiveAndSeesDirectories()
    {
        var dir = Dir();
        Dir("Users");
        File.WriteAllText(Path.Combine(dir, "ORDERS.request.yaml"), "");
        Assert.Equal("users-2", WorkspaceFiles.UniqueName(dir, "users", "", null));
        Assert.Equal("orders-2.request.yaml", WorkspaceFiles.UniqueName(dir, "orders", ".request.yaml", null));
    }
}
