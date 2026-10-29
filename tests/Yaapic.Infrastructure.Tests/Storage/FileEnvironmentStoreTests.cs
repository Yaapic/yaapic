using System.Text;
using Yaapic.Domain.Environments;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class FileEnvironmentStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
    private string Environments => Path.Combine(_root, "environments");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string[] FileNames() =>
        [.. Directory.GetFiles(Environments).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal)];

    [Fact]
    public async Task SaveThenListReturnsEnvironment()
    {
        var store = new FileEnvironmentStore(_root);
        var environment = EnvironmentDefinition.Create("Get users");
        await store.SaveAsync(environment, CancellationToken.None);

        var list = await new FileEnvironmentStore(_root).ListAsync(CancellationToken.None);
        Assert.Equal(["get-users.env.yaml"], FileNames());
        Assert.True(environment.IsEquivalentTo(Assert.Single(list)));
    }

    [Fact]
    public async Task SaveTwiceSameIdOverwritesOneFile()
    {
        var store = new FileEnvironmentStore(_root);
        var environment = EnvironmentDefinition.Create("A");
        await store.SaveAsync(environment, CancellationToken.None);
        await store.SaveAsync(environment with { Variables = [new("k", "v")] }, CancellationToken.None);

        Assert.Single(FileNames());
        Assert.Equal("k", Assert.Single(Assert.Single(await store.ListAsync(CancellationToken.None)).Variables).Key);
    }

    [Fact]
    public async Task RenameMovesFile()
    {
        var store = new FileEnvironmentStore(_root);
        var environment = EnvironmentDefinition.Create("Old name");
        await store.SaveAsync(environment, CancellationToken.None);
        await store.SaveAsync(environment with { Name = "New name" }, CancellationToken.None);

        Assert.Equal(["new-name.env.yaml"], FileNames());
    }

    [Fact]
    public async Task SlugCollisionGetsSuffix()
    {
        var store = new FileEnvironmentStore(_root);
        await store.SaveAsync(EnvironmentDefinition.Create("Users"), CancellationToken.None);
        await store.SaveAsync(EnvironmentDefinition.Create("users!"), CancellationToken.None);
        await store.SaveAsync(EnvironmentDefinition.Create("USERS"), CancellationToken.None);

        Assert.Equal(["users-2.env.yaml", "users-3.env.yaml", "users.env.yaml"], FileNames());
    }

    [Fact]
    public async Task EmptySlugFallsBackToEnvironment()
    {
        await new FileEnvironmentStore(_root).SaveAsync(EnvironmentDefinition.Create("???"), CancellationToken.None);
        Assert.Equal(["environment.env.yaml"], FileNames());
    }

    [Fact]
    public async Task DeleteRemovesFileAndUnknownIdIsNoOp()
    {
        var store = new FileEnvironmentStore(_root);
        var environment = EnvironmentDefinition.Create("A");
        await store.SaveAsync(environment, CancellationToken.None);
        await store.DeleteAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Single(FileNames());

        await store.DeleteAsync(environment.Id, CancellationToken.None);
        Assert.Empty(FileNames());
    }

    [Fact]
    public async Task MissingDirectoryListsEmptyAndCreatesNothing()
    {
        Assert.Empty(await new FileEnvironmentStore(_root).ListAsync(CancellationToken.None));
        Assert.False(Directory.Exists(Environments));
    }

    [Fact]
    public async Task CorruptFileIsSkippedAndReported()
    {
        var store = new FileEnvironmentStore(_root);
        await store.SaveAsync(EnvironmentDefinition.Create("Good"), CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(Environments, "bad.env.yaml"), "version: [", CancellationToken.None);

        Assert.Single(await store.ListAsync(CancellationToken.None));
        Assert.Equal(["bad.env.yaml"], store.LastListErrors);
        await store.ListAsync(CancellationToken.None);
        Assert.Equal(["bad.env.yaml"], store.LastListErrors);
    }

    [Fact]
    public async Task DuplicateIdKeepsFirstFileAndReportsSecond()
    {
        var store = new FileEnvironmentStore(_root);
        await store.SaveAsync(EnvironmentDefinition.Create("B"), CancellationToken.None);
        File.Copy(Path.Combine(Environments, "b.env.yaml"), Path.Combine(Environments, "c.env.yaml"));

        Assert.Single(await store.ListAsync(CancellationToken.None));
        Assert.Equal(["c.env.yaml"], store.LastListErrors);
    }

    [Fact]
    public async Task ListIsSortedByNameIgnoringCase()
    {
        var store = new FileEnvironmentStore(_root);
        await store.SaveAsync(EnvironmentDefinition.Create("b"), CancellationToken.None);
        await store.SaveAsync(EnvironmentDefinition.Create("A"), CancellationToken.None);

        var names = (await store.ListAsync(CancellationToken.None)).Select(e => e.Name);
        Assert.Equal(["A", "b"], names);
    }

    [Fact]
    public async Task FileHasNoTmpNoBomAndLfEndings()
    {
        await new FileEnvironmentStore(_root).SaveAsync(EnvironmentDefinition.Create("A"), CancellationToken.None);

        Assert.DoesNotContain(FileNames(), n => n.EndsWith(".tmp", StringComparison.Ordinal));
        var bytes = await File.ReadAllBytesAsync(Path.Combine(Environments, "a.env.yaml"), TestContext.Current.CancellationToken);
        Assert.NotEqual(Encoding.UTF8.GetPreamble(), bytes.Take(3));
        Assert.DoesNotContain((byte)'\r', bytes);
    }
}
