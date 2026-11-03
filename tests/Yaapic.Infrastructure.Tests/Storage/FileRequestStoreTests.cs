using System.Text;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class FileRequestStoreTests : IDisposable
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

    private string[] FileNames() =>
        [.. Directory.GetFiles(Requests).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal)];

    [Fact]
    public async Task SaveThenListReturnsRequest()
    {
        var store = new FileRequestStore(_root);
        var request = HttpRequestDefinition.Create("Get users");
        await store.SaveAsync(request, CancellationToken.None);

        var list = await new FileRequestStore(_root).ListAsync(CancellationToken.None);
        Assert.Equal(["get-users.request.yaml"], FileNames());
        Assert.True(request.IsEquivalentTo(Assert.Single(list)));
    }

    [Fact]
    public async Task SaveTwiceSameIdOverwritesOneFile()
    {
        var store = new FileRequestStore(_root);
        var request = HttpRequestDefinition.Create("A");
        await store.SaveAsync(request, CancellationToken.None);
        await store.SaveAsync(request with { Url = "http://x" }, CancellationToken.None);

        Assert.Single(FileNames());
        Assert.Equal("http://x", Assert.Single(await store.ListAsync(CancellationToken.None)).Url);
    }

    [Fact]
    public async Task RenameMovesFile()
    {
        var store = new FileRequestStore(_root);
        var request = HttpRequestDefinition.Create("Old name");
        await store.SaveAsync(request, CancellationToken.None);
        await store.SaveAsync(request with { Name = "New name" }, CancellationToken.None);

        Assert.Equal(["new-name.request.yaml"], FileNames());
    }

    [Fact]
    public async Task SlugCollisionGetsSuffix()
    {
        var store = new FileRequestStore(_root);
        await store.SaveAsync(HttpRequestDefinition.Create("Users"), CancellationToken.None);
        await store.SaveAsync(HttpRequestDefinition.Create("users!"), CancellationToken.None);
        await store.SaveAsync(HttpRequestDefinition.Create("USERS"), CancellationToken.None);

        Assert.Equal(["users-2.request.yaml", "users-3.request.yaml", "users.request.yaml"], FileNames());
    }

    [Fact]
    public async Task EmptySlugFallsBackToRequest()
    {
        await new FileRequestStore(_root).SaveAsync(HttpRequestDefinition.Create("???"), CancellationToken.None);
        Assert.Equal(["request.request.yaml"], FileNames());
    }

    [Fact]
    public async Task DeleteRemovesFileAndUnknownIdIsNoOp()
    {
        var store = new FileRequestStore(_root);
        var request = HttpRequestDefinition.Create("A");
        await store.SaveAsync(request, CancellationToken.None);
        await store.DeleteAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Single(FileNames());

        await store.DeleteAsync(request.Id, CancellationToken.None);
        Assert.Empty(FileNames());
    }

    [Fact]
    public async Task MissingDirectoryListsEmptyAndCreatesNothing()
    {
        Assert.Empty(await new FileRequestStore(_root).ListAsync(CancellationToken.None));
        Assert.False(Directory.Exists(Requests));
    }

    [Fact]
    public async Task CorruptFileIsSkippedAndReported()
    {
        var store = new FileRequestStore(_root);
        await store.SaveAsync(HttpRequestDefinition.Create("Good"), CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(Requests, "bad.request.yaml"), "version: [", CancellationToken.None);

        Assert.Single(await store.ListAsync(CancellationToken.None));
        Assert.StartsWith("bad.request.yaml:", Assert.Single(store.LastListErrors));
        await store.ListAsync(CancellationToken.None);
        Assert.StartsWith("bad.request.yaml:", Assert.Single(store.LastListErrors));
    }

    [Fact]
    public async Task DuplicateIdKeepsFirstFileAndReportsSecond()
    {
        var store = new FileRequestStore(_root);
        await store.SaveAsync(HttpRequestDefinition.Create("B"), CancellationToken.None);
        File.Copy(Path.Combine(Requests, "b.request.yaml"), Path.Combine(Requests, "c.request.yaml"));

        Assert.Single(await store.ListAsync(CancellationToken.None));
        Assert.StartsWith("c.request.yaml:", Assert.Single(store.LastListErrors));
    }

    [Fact]
    public async Task ListIsSortedByNameIgnoringCase()
    {
        var store = new FileRequestStore(_root);
        await store.SaveAsync(HttpRequestDefinition.Create("b"), CancellationToken.None);
        await store.SaveAsync(HttpRequestDefinition.Create("A"), CancellationToken.None);

        var names = (await store.ListAsync(CancellationToken.None)).Select(r => r.Name);
        Assert.Equal(["A", "b"], names);
    }

    [Fact]
    public async Task FileHasNoTmpNoBomAndLfEndings()
    {
        await new FileRequestStore(_root).SaveAsync(HttpRequestDefinition.Create("A"), CancellationToken.None);

        Assert.DoesNotContain(FileNames(), n => n.EndsWith(".tmp", StringComparison.Ordinal));
        var bytes = await File.ReadAllBytesAsync(Path.Combine(Requests, "a.request.yaml"), TestContext.Current.CancellationToken);
        Assert.NotEqual(Encoding.UTF8.GetPreamble(), bytes.Take(3));
        Assert.DoesNotContain((byte)'\r', bytes);
    }

    [Fact]
    public void SlugifyFollowsFileFormatRules()
    {
        Assert.Equal("get-users-caf", WorkspaceFiles.Slugify("  Get users - café ☕", "request"));
        Assert.Equal(60, WorkspaceFiles.Slugify(new string('a', 100), "request").Length);
    }

    private async Task<HttpRequestDefinition> SeedNestedAsync(string folderDir, string name)
    {
        Directory.CreateDirectory(Path.Combine(Requests, folderDir));
        var folder = Path.Combine(Requests, folderDir, "folder.yaml");
        if (!File.Exists(folder))
        {
            await File.WriteAllTextAsync(folder, FolderYamlSerializer.Serialize(Yaapic.Domain.Collections.FolderDefinition.Create(folderDir)));
        }

        var request = HttpRequestDefinition.Create(name);
        await File.WriteAllTextAsync(Path.Combine(Requests, folderDir, "seed.request.yaml"), RequestYamlSerializer.Serialize(request));
        return request;
    }

    [Fact]
    public async Task NestedRequestsAreListed()
    {
        var nested = await SeedNestedAsync("api", "N");
        var list = await new FileRequestStore(_root).ListAsync(CancellationToken.None);
        Assert.Equal(nested.Id, Assert.Single(list).Id);
    }

    [Fact]
    public async Task SavingNestedRequestKeepsItsFolderAndRenamesInPlace()
    {
        var nested = await SeedNestedAsync("api", "N");
        var store = new FileRequestStore(_root);
        await store.SaveAsync(nested, CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(Requests, "api", "n.request.yaml")));
        Assert.False(File.Exists(Path.Combine(Requests, "api", "seed.request.yaml")));

        await store.SaveAsync(nested with { Name = "Renamed" }, CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(Requests, "api", "renamed.request.yaml")));
        Assert.False(File.Exists(Path.Combine(Requests, "api", "n.request.yaml")));
        Assert.Empty(Directory.GetFiles(Requests));
    }

    [Fact]
    public async Task NameCollisionIsPerDirectory()
    {
        await SeedNestedAsync("api", "Same");
        File.Move(Path.Combine(Requests, "api", "seed.request.yaml"), Path.Combine(Requests, "api", "same.request.yaml"));
        var store = new FileRequestStore(_root);
        await store.SaveAsync(HttpRequestDefinition.Create("Same"), CancellationToken.None);
        Assert.Equal(["same.request.yaml"], FileNames());

        await store.SaveAsync(HttpRequestDefinition.Create("Same"), CancellationToken.None);
        Assert.Equal(["same-2.request.yaml", "same.request.yaml"], FileNames());
    }

    [Fact]
    public async Task DeleteRemovesNestedRequestAndKeepsFolder()
    {
        var nested = await SeedNestedAsync("api", "N");
        var store = new FileRequestStore(_root);
        await store.DeleteAsync(nested.Id, CancellationToken.None);
        Assert.False(File.Exists(Path.Combine(Requests, "api", "seed.request.yaml")));
        Assert.True(File.Exists(Path.Combine(Requests, "api", "folder.yaml")));
    }

    [Fact]
    public async Task FolderErrorsSurface()
    {
        Directory.CreateDirectory(Path.Combine(Requests, "nofolderyaml"));
        var store = new FileRequestStore(_root);
        Assert.Empty(await store.ListAsync(CancellationToken.None));
        Assert.StartsWith("nofolderyaml:", Assert.Single(store.LastListErrors));
    }
}
