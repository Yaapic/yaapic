using Yaapic.Composition;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;
using Yaapic.Infrastructure.Tests.EndToEnd;
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
    public void CreateShellAttachesAPaletteListingTheCommands()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
        try
        {
            var shell = AppComposition.CreateShell(root, new FakeDialogService());

            shell.Palette.OpenCommand.Execute(null);

            Assert.Contains(shell.Palette.Results, r => r.Item.Id == "command:new-request");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SendingARequestRecordsItInTheHistoryAndTheHistoryFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
        using var server = new LoopbackServer();
        try
        {
            var shell = AppComposition.CreateShell(root, new FakeDialogService());
            await shell.InitializeAsync();
            var tab = shell.Tabs.ActiveTab!;
            tab.Url = server.BaseUrl + "/echo";

            await tab.SendCommand.ExecuteAsync(null);
            await Eventually(() => shell.History.Groups.Count > 0);

            var item = Assert.Single(shell.History.Groups.SelectMany(g => g.Items));
            Assert.Equal(server.BaseUrl + "/echo", item.Url);
            var file = await File.ReadAllTextAsync(Path.Combine(root, ".yaapic", "history.jsonl"), TestContext.Current.CancellationToken);
            Assert.Contains("/echo", file, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
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
