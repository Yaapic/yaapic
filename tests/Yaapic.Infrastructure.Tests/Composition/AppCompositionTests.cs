using Yaapic.Composition;
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
}
