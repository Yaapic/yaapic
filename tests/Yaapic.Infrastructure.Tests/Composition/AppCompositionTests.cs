using Yaapic.Composition;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;
using Yaapic.TestDoubles;

namespace Yaapic.Infrastructure.Tests.Composition;

public sealed class AppCompositionTests
{
    [Fact]
    public async Task CreateShellOnAnEmptyWorkspaceOpensOneNewTab()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
        try
        {
            var shell = AppComposition.CreateShell(root, new FakeDialogService());

            await shell.InitializeAsync();

            var tab = Assert.Single(shell.Tabs.Tabs);
            Assert.True(tab.IsNew);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateShellExposesTheCollectionTreeOfTheWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
        try
        {
            Workspace.EnsureCreated(root);
            var requests = new FileRequestStore(root);
            var collections = new FileCollectionStore(root);
            var folder = FolderDefinition.Create("api");
            var request = HttpRequestDefinition.Create("ping");
            await collections.SaveFolderAsync(folder, null, CancellationToken.None);
            await requests.SaveAsync(request, CancellationToken.None);
            await collections.MoveAsync(request.Id, folder.Id, CancellationToken.None);
            var shell = AppComposition.CreateShell(root, new FakeDialogService());

            await shell.InitializeAsync();

            var node = Assert.Single(shell.Collections.Roots);
            Assert.Equal(folder.Id, node.Id);
            Assert.Contains(node.Children, c => c.Id == request.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateShellLoadsTheGlobalsOfTheWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
        try
        {
            Workspace.EnsureCreated(root);
            await File.WriteAllTextAsync(
                Path.Combine(root, "globals.yaml"),
                "version: 1\nvariables:\n- key: token\n  value: abc\n  enabled: true\n", TestContext.Current.CancellationToken);
            var shell = AppComposition.CreateShell(root, new FakeDialogService());

            await shell.InitializeAsync();

            Assert.Contains(shell.Globals.Variables.ToEntries(), e => e.Key == "token" && e.Value == "abc");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CorruptGlobalsSetLastErrorAndLeaveTheShellUsable()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
        try
        {
            Workspace.EnsureCreated(root);
            await File.WriteAllTextAsync(Path.Combine(root, "globals.yaml"), "version: [unclosed", TestContext.Current.CancellationToken);
            var shell = AppComposition.CreateShell(root, new FakeDialogService());

            await shell.InitializeAsync();

            Assert.NotNull(shell.Globals.LastError);
            Assert.Single(shell.Tabs.Tabs);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
