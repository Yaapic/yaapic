using System.Text;
using Yaapic.Composition;
using Yaapic.Core.Import;
using Yaapic.Core.Secrets;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;
using Yaapic.Infrastructure.Tests.EndToEnd;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Shell;

namespace Yaapic.Infrastructure.Tests.Composition;

public sealed class M5CompositionTests : IDisposable
{
    private const string Collection = """{"info":{"name":"Pets","schema":"https://schema.getpostman.com/json/collection/v2.1.0/collection.json"},"item":[{"name":"List","request":{"method":"GET","url":"https://api.example.test/pets"}}]}""";

    private const string Environment = """{"name":"Prod","values":[{"key":"token","value":"s3cret","type":"secret","enabled":true}]}""";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
    private readonly string _data = Path.Combine(Path.GetTempPath(), "yaapic-data-" + Guid.NewGuid().ToString("N"));
    private readonly FakeDialogService _dialogs = new();

    public void Dispose()
    {
        TempWorkspace.Delete(_root);
        TempWorkspace.Delete(_data);
    }

    private async Task<ShellViewModel> CreateAsync()
    {
        var shell = AppComposition.CreateShell(_root, _dialogs, null, new DefaultVaultLocation(_data), [], userDataDirectory: _data);
        await shell.InitializeAsync();
        return shell;
    }

    private string WorkspaceText() =>
        string.Concat(Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Select(File.ReadAllText));

    private static async Task ImportAsync(ShellViewModel shell, string text)
    {
        var import = shell.CreateImport();
        import.PastedText = text;
        await import.PreviewPastedCommand.ExecuteAsync(null);
        await import.ImportCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task TheShellOffersTheSevenImportersAndSixExportersInOrder()
    {
        var shell = await CreateAsync();

        Assert.Equal(
            [null, "postman", "postman-environment", "openapi", "insomnia", "har", "bruno", "curl"],
            shell.CreateImport().Formats.Select(f => f.Id));
        Assert.Equal(
            ["postman", "postman-environment", "openapi-json", "openapi-yaml", "insomnia", "bruno"],
            shell.CreateExport().Formats.Select(f => f.Id));
    }

    [Fact]
    public async Task APastedPostmanCollectionImportsThenExportsBack()
    {
        var shell = await CreateAsync();
        await ImportAsync(shell, Collection);

        var root = Assert.Single(shell.Collections.Roots);
        Assert.Equal("Pets", root.Name);

        var export = shell.CreateExport();
        await export.LoadAsync(null);
        export.SelectedFormat = export.Formats.Single(f => f.Id == "postman");
        _dialogs.SaveFileAnswer = true;
        await export.ExportCommand.ExecuteAsync(null);

        var saved = Assert.Single(_dialogs.SavedFiles);
        Assert.NotNull(DataNodeJson.Parse(Encoding.UTF8.GetString(saved.Content)));
    }

    [Fact]
    public async Task ThePaletteListsTheImportAndExportEntries()
    {
        var shell = await CreateAsync();

        shell.Palette.OpenCommand.Execute(null);

        Assert.Contains(shell.Palette.Results, r => r.Item.Id == "command:import");
        Assert.Contains(shell.Palette.Results, r => r.Item.Id == "command:export-collection");
    }

    [Fact]
    public async Task ASecretImportsBlankAndWarnsWhileTheVaultIsLocked()
    {
        var shell = await CreateAsync();
        var import = shell.CreateImport();
        import.PastedText = Environment;
        await import.PreviewPastedCommand.ExecuteAsync(null);
        await import.ImportCommand.ExecuteAsync(null);

        Assert.Contains(import.Report.Rows, r => r.Severity == ImportSeverity.Warning);
        Assert.DoesNotContain("s3cret", WorkspaceText());
        Assert.Contains("Prod", WorkspaceText());
    }

    [Fact]
    public async Task ASecretReachesTheVaultWhenItIsUnlocked()
    {
        var shell = await CreateAsync();
        var vault = shell.Vault;
        vault.SelectedProtector = vault.Protectors.Single(p => p.Id == "passphrase");
        vault.Passphrase = "correct horse";
        vault.ConfirmPassphrase = "correct horse";
        await vault.SetupCommand.ExecuteAsync(null);
        Assert.Equal(VaultState.Unlocked, vault.State);

        var import = shell.CreateImport();
        import.PastedText = Environment;
        await import.PreviewPastedCommand.ExecuteAsync(null);
        await import.ImportCommand.ExecuteAsync(null);

        Assert.DoesNotContain(import.Report.Rows, r => r.Severity == ImportSeverity.Warning);
        Assert.DoesNotContain("s3cret", WorkspaceText());
        Assert.Contains("Prod", WorkspaceText());
        Assert.True(File.Exists(new DefaultVaultLocation(_data).GetVaultPath(Workspace.ReadWorkspaceId(_root))));
    }
}
