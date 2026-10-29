using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class WorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public void EnsureCreatedBuildsTreeAndIsIdempotent()
    {
        Workspace.EnsureCreated(_root);
        Workspace.EnsureCreated(_root);

        Assert.True(Directory.Exists(Path.Combine(_root, "requests")));
        Assert.True(Directory.Exists(Path.Combine(_root, "environments")));
        Assert.True(Directory.Exists(Path.Combine(_root, ".yaapic")));
        Assert.Equal(".yaapic/\n", File.ReadAllText(Path.Combine(_root, ".gitignore")));
    }

    [Fact]
    public void EnsureCreatedKeepsExistingGitignore()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, ".gitignore"), "custom\n");
        Workspace.EnsureCreated(_root);
        Assert.Equal("custom\n", File.ReadAllText(Path.Combine(_root, ".gitignore")));
    }

    [Fact]
    public void DefaultRootEndsWithYaapicDefault() =>
        Assert.EndsWith(Path.Combine("Yaapic", "Default"), Workspace.DefaultRoot());
}
