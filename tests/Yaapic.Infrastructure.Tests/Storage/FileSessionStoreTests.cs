using Yaapic.Domain.Workspace;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class FileSessionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
    private string SessionPath => Path.Combine(_root, ".yaapic", "session.yaml");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private void Write(string yaml)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
        File.WriteAllText(SessionPath, yaml);
    }

    [Fact]
    public async Task SaveThenLoadRoundTrips()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), env = Guid.NewGuid();
        await new FileSessionStore(_root).SaveAsync(new SessionState([a, b], b, env), CancellationToken.None);

        var loaded = await new FileSessionStore(_root).LoadAsync(CancellationToken.None);
        Assert.Equal([a, b], loaded.OpenRequestIds);
        Assert.Equal(b, loaded.ActiveRequestId);
        Assert.Equal(env, loaded.ActiveEnvironmentId);
    }

    [Fact]
    public async Task SaveEmptyThenLoadReturnsNulls()
    {
        await new FileSessionStore(_root).SaveAsync(SessionState.Empty, CancellationToken.None);
        var loaded = await new FileSessionStore(_root).LoadAsync(CancellationToken.None);
        Assert.Empty(loaded.OpenRequestIds);
        Assert.Null(loaded.ActiveRequestId);
        Assert.Null(loaded.ActiveEnvironmentId);
    }

    [Fact]
    public async Task MissingFileReturnsEmpty() =>
        Assert.Same(SessionState.Empty, await new FileSessionStore(_root).LoadAsync(CancellationToken.None));

    [Theory]
    [InlineData("::: not yaml [")]
    [InlineData("version: 2\nopenRequests: []\nactiveRequest: \"\"\nactiveEnvironment: \"\"\n")]
    public async Task InvalidFileReturnsEmpty(string yaml)
    {
        Write(yaml);
        Assert.Same(SessionState.Empty, await new FileSessionStore(_root).LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InvalidGuidsAreSkipped()
    {
        var good = Guid.NewGuid();
        Write($"version: 1\nopenRequests:\n- nope\n- {good:D}\nactiveRequest: bad\nactiveEnvironment: \"\"\n");
        var loaded = await new FileSessionStore(_root).LoadAsync(CancellationToken.None);
        Assert.Equal([good], loaded.OpenRequestIds);
        Assert.Null(loaded.ActiveRequestId);
        Assert.Null(loaded.ActiveEnvironmentId);
    }
}
