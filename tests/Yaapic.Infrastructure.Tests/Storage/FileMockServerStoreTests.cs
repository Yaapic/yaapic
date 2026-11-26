using Yaapic.Domain.Http;
using Yaapic.Domain.Mocks;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class FileMockServerStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
    private string Mocks => Path.Combine(_root, "mocks");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string[] FileNames() =>
        [.. Directory.GetFiles(Mocks).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal)];

    private static MockServerDefinition Sample() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"), "Users API", 4000, false,
        [
            new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "List", true, "GET", "/users", 200,
                [new("Content-Type", "application/json"), new("X-Off", "1", false)], "{\n  \"a\": 1\n}\n", 150),
            new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Gone", false, "ANY", "/old/*", 410, [], "", 0),
        ]);

    [Fact]
    public async Task RoundTripKeepsEveryField()
    {
        var server = Sample();
        await new FileMockServerStore(_root).SaveAsync(server, CancellationToken.None);

        var list = await new FileMockServerStore(_root).ListAsync(CancellationToken.None);
        Assert.Equal(["users-api.mock.yaml"], FileNames());
        Assert.True(server.IsEquivalentTo(Assert.Single(list)));
    }

    [Fact]
    public void YamlMatchesExpectedLiteral()
    {
        var server = Sample() with { Routes = [Sample().Routes[0] with { Headers = [new("X-A", "1")], Body = "line1\nline2\n" }] };
        const string expected = """
            version: 1
            id: 11111111-1111-1111-1111-111111111111
            name: Users API
            port: 4000
            cors: false
            routes:
            - id: 22222222-2222-2222-2222-222222222222
              name: List
              enabled: true
              method: GET
              path: /users
              status: 200
              headers:
              - key: X-A
                value: "1"
                enabled: true
              body: |
                line1
                line2
              delayMs: 150

            """;
        Assert.Equal(expected.Replace("\r\n", "\n"), MockServerYamlSerializer.Serialize(server));
    }

    [Fact]
    public async Task RenameMovesFile()
    {
        var store = new FileMockServerStore(_root);
        var server = MockServerDefinition.Create("Old");
        await store.SaveAsync(server, CancellationToken.None);
        await store.SaveAsync(server with { Name = "New" }, CancellationToken.None);

        Assert.Equal(["new.mock.yaml"], FileNames());
    }

    [Fact]
    public async Task SameNameGetsDistinctFiles()
    {
        var store = new FileMockServerStore(_root);
        await store.SaveAsync(MockServerDefinition.Create("Users"), CancellationToken.None);
        await store.SaveAsync(MockServerDefinition.Create("users"), CancellationToken.None);

        Assert.Equal(["users-2.mock.yaml", "users.mock.yaml"], FileNames());
    }

    [Fact]
    public async Task DeleteRemovesFileAndUnknownIdIsNoOp()
    {
        var store = new FileMockServerStore(_root);
        var server = MockServerDefinition.Create("A");
        await store.SaveAsync(server, CancellationToken.None);
        await store.DeleteAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Single(FileNames());

        await store.DeleteAsync(server.Id, CancellationToken.None);
        Assert.Empty(FileNames());
    }

    [Fact]
    public async Task CorruptFileIsSkippedAndReported()
    {
        var store = new FileMockServerStore(_root);
        await store.SaveAsync(MockServerDefinition.Create("Good"), CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(Mocks, "bad.mock.yaml"), "version: [", CancellationToken.None);

        Assert.Single(await store.ListAsync(CancellationToken.None));
        Assert.Equal(["bad.mock.yaml"], store.LastListErrors);
    }

    [Fact]
    public async Task ListIsSortedByNameIgnoringCase()
    {
        var store = new FileMockServerStore(_root);
        await store.SaveAsync(MockServerDefinition.Create("b"), CancellationToken.None);
        await store.SaveAsync(MockServerDefinition.Create("A"), CancellationToken.None);

        Assert.Equal(["A", "b"], (await store.ListAsync(CancellationToken.None)).Select(s => s.Name));
    }
}
