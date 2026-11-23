using System.IO.Compression;
using System.Text;
using Yaapic.Core.Import;
using Yaapic.Core.Secrets;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Import;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Localization;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Import;
using Yaapic.ViewModels.Shell;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>
/// Exports through the real shell (export view model, exporters, packager, stores) from a workspace built by importing the
/// Postman, Insomnia, Bruno and OpenAPI samples, then reads every export back with the matching importer.
/// </summary>
public sealed class Milestone5ExportEndToEndTests : IClassFixture<LoopbackServer>, IDisposable
{
    private const string Passphrase = "correct horse battery";
    private const string SecretValue = "s3cr3t-VALUE";

    private static readonly string[] CollectionFormats = ["postman", "openapi-json", "openapi-yaml", "insomnia", "bruno"];

    private readonly LoopbackServer _server;
    private readonly ScriptedShellFixture _fx = new();
    private readonly string _inputs = Path.Combine(Path.GetTempPath(), "yaapic-e2e5x-inputs-" + Guid.NewGuid().ToString("N"));

    public Milestone5ExportEndToEndTests(LoopbackServer server)
    {
        _server = server;
        Directory.CreateDirectory(_inputs);
        _fx.Dialogs.SaveFileAnswer = true;
    }

    public void Dispose()
    {
        _fx.Dispose();
        TempWorkspace.Delete(_inputs);
    }

    private string Sample(string fileName, string text)
    {
        var path = Path.Combine(_inputs, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private static IEnumerable<CollectionNodeViewModel> Flatten(IEnumerable<CollectionNodeViewModel> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));

    private static string Snapshot(ShellViewModel shell, string name, SnapshotIgnore ignore = SnapshotIgnore.None) =>
        ModelSnapshot.Of(shell.Collections.Roots, name, ignore);

    private async Task ImportFileAsync(ShellViewModel shell, string path)
    {
        _fx.Dialogs.PickFileAnswer = path;
        var import = shell.CreateImport();
        await import.ChooseFileCommand.ExecuteAsync(null);
        Assert.Null(import.Error);
        await import.ImportCommand.ExecuteAsync(null);
        Assert.True(import.IsDone, import.Error);
    }

    private async Task ImportFolderAsync(ShellViewModel shell, string folder)
    {
        _fx.Dialogs.PickFolderAnswer = folder;
        var import = shell.CreateImport();
        await import.ChooseFolderCommand.ExecuteAsync(null);
        Assert.Null(import.Error);
        await import.ImportCommand.ExecuteAsync(null);
        Assert.True(import.IsDone, import.Error);
    }

    private async Task<ShellViewModel> BuildWorkspaceAsync()
    {
        var shell = await _fx.NewShellAsync(protectors: new PassphraseKeyProtectorFactory(100_000));
        var vault = shell.Vault;
        vault.SelectedProtector = vault.Protectors.Single(p => p.Id == "passphrase");
        vault.Passphrase = Passphrase;
        vault.ConfirmPassphrase = Passphrase;
        await vault.SetupCommand.ExecuteAsync(null);
        Assert.Equal(VaultState.Unlocked, vault.State);

        await ImportFileAsync(shell, Sample("env.postman_environment.json", Milestone5Samples.PostmanEnvironment));
        await ImportFileAsync(shell, Sample("shop.postman_collection.json", Milestone5Samples.PostmanCollection.Replace("@@BASE@@", _server.BaseUrl, StringComparison.Ordinal)));
        await ImportFileAsync(shell, Sample("billing.insomnia.json", Milestone5Samples.Insomnia));
        await ImportFileAsync(shell, Sample("pets.yaml", Milestone5Samples.OpenApiYaml));
        Sample("library/bruno.json", Milestone5Samples.BrunoJson);
        Sample("library/books/folder.bru", Milestone5Samples.BrunoFolder);
        Sample("library/books/get-book.bru", Milestone5Samples.BrunoGet);
        Sample("library/books/add-book.bru", Milestone5Samples.BrunoPost);
        Sample("library/environments/local.bru", Milestone5Samples.BrunoEnvironment);
        await ImportFolderAsync(shell, Path.Combine(_inputs, "library"));
        return shell;
    }

    /// <summary>Exports through the dialog view model and returns the file the user "saved".</summary>
    private async Task<(string Name, byte[] Content, ExportViewModel Export)> ExportAsync(ShellViewModel shell, string format, string? collection, params string[] environments)
    {
        var export = shell.CreateExport();
        await export.LoadAsync(null);
        export.SelectedFormat = export.Formats.Single(f => f.Id == format);
        if (collection is not null)
        {
            export.SelectedCollection = export.Collections.Single(c => c.Name == collection);
        }

        foreach (var e in export.EnvironmentChoices)
        {
            e.IsSelected = environments.Contains(e.Name);
        }

        var before = _fx.Dialogs.SavedFiles.Count;
        await export.ExportCommand.ExecuteAsync(null);
        Assert.Null(export.Error);
        Assert.True(export.Saved);
        Assert.Equal(before + 1, _fx.Dialogs.SavedFiles.Count);
        var (name, content) = _fx.Dialogs.SavedFiles[^1];
        return (name, content, export);
    }

    private string Save(string name, byte[] content)
    {
        var path = Path.Combine(_inputs, "exports", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>Unpacks a Bruno zip into a folder, without the root directory, and checks every entry stays inside it.</summary>
    private string Unpack(byte[] zipBytes, string folderName)
    {
        var folder = Path.Combine(_inputs, folderName);
        using var zip = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            var parts = entry.FullName.Split('/');
            Assert.DoesNotContain("..", parts);
            Assert.False(Path.IsPathRooted(entry.FullName), entry.FullName);
            var target = Path.GetFullPath(Path.Combine(folder, string.Join('/', parts.Skip(1))));
            Assert.StartsWith(Path.GetFullPath(folder), target, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var s = entry.Open();
            using var f = File.Create(target);
            s.CopyTo(f);
        }

        return folder;
    }

    private static string[] AllText(string name, byte[] content)
    {
        if (!name.EndsWith(".zip", StringComparison.Ordinal))
        {
            return [Encoding.UTF8.GetString(content)];
        }

        using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        return [.. zip.Entries.Select(e =>
        {
            using var r = new StreamReader(e.Open(), Encoding.UTF8);
            return r.ReadToEnd();
        })];
    }

    [Fact]
    public async Task PostmanCollectionRoundTripKeepsTheWholeModel()
    {
        var shell = await BuildWorkspaceAsync();
        var (name, content, _) = await ExportAsync(shell, "postman", "Shop API");

        await ImportFileAsync(shell, Save(name, content));

        Assert.Equal(Snapshot(shell, "Shop API"), Snapshot(shell, "Shop API (2)"));
    }

    [Fact]
    public async Task PostmanEnvironmentRoundTripKeepsRowsAndBlanksTheSecret()
    {
        var shell = await BuildWorkspaceAsync();
        var before = shell.Environments.Environments.Count;
        var (name, content, export) = await ExportAsync(shell, "postman-environment", null, "Staging");

        await ImportFileAsync(shell, Save(name, content));

        Assert.Equal(before + 1, shell.Environments.Environments.Count);
        var original = shell.Environments.Environments.First(e => e.Name == "Staging").Variables.ToEntries();
        var copy = shell.Environments.Environments.Last().Variables.ToEntries();
        Assert.Equal(["token", "apiSecret"], [.. copy.Select(r => r.Key)]);
        Assert.Equal(original.Single(r => r.Key == "token"), copy.Single(r => r.Key == "token"));
        var secret = copy.Single(r => r.Key == "apiSecret");
        Assert.Equal(("", true), (secret.Value, secret.IsSecret));
        Assert.Equal(SecretValue, original.Single(r => r.Key == "apiSecret").Value);
        Assert.Contains(export.Report.Rows, r => r.Severity == ImportSeverity.Info && r.Message == Strings.ExportSecretsOmitted(1));
    }

    [Fact]
    public async Task InsomniaRoundTripKeepsFoldersRequestsAndTheScopedEnvironment()
    {
        var shell = await BuildWorkspaceAsync();
        var (name, content, _) = await ExportAsync(shell, "insomnia", "Billing");

        await ImportFileAsync(shell, Save(name, content));

        Assert.Equal(Snapshot(shell, "Billing"), Snapshot(shell, "Billing (2)"));
        var copyId = shell.Collections.Roots.Single(r => r.Name == "Billing (2)").Id;
        var scoped = Assert.Single(shell.Environments.Environments, e => e.CollectionId == copyId);
        Assert.StartsWith("Staging", scoped.Name, StringComparison.Ordinal); // numbered on a name clash
        Assert.Contains(scoped.Variables.ToEntries(), v => v.Key == "baseUrl" && v.Value == "https://staging.billing.example.test");
    }

    [Fact]
    public async Task InsomniaRoundTripDropsTheScriptsOfAFullCollection()
    {
        var shell = await BuildWorkspaceAsync();
        var (name, content, export) = await ExportAsync(shell, "insomnia", "Shop API");

        await ImportFileAsync(shell, Save(name, content));

        Assert.Equal(Snapshot(shell, "Shop API", SnapshotIgnore.Scripts), Snapshot(shell, "Shop API (2)", SnapshotIgnore.Scripts));
        Assert.Contains("pre-list", Snapshot(shell, "Shop API"), StringComparison.Ordinal);
        Assert.DoesNotContain("pre-list", Snapshot(shell, "Shop API (2)"), StringComparison.Ordinal);
        Assert.True(export.Report.HasProblems);
    }

    [Fact]
    public async Task BrunoRoundTripKeepsFoldersRequestsAndEnvironments()
    {
        var shell = await BuildWorkspaceAsync();
        var (name, content, _) = await ExportAsync(shell, "bruno", "Library");
        Assert.EndsWith(".zip", name, StringComparison.Ordinal);

        await ImportFolderAsync(shell, Unpack(content, "bruno-rt-library"));

        Assert.Equal(Snapshot(shell, "Library"), Snapshot(shell, "Library (2)"));
        Assert.Equal(2, shell.Environments.Environments.Count(e => e.Name.StartsWith("local", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task BrunoRoundTripTurnsCollectionVariablesIntoScriptLines()
    {
        var shell = await BuildWorkspaceAsync();
        var (_, content, _) = await ExportAsync(shell, "bruno", "Shop API");

        await ImportFolderAsync(shell, Unpack(content, "bruno-rt-shop"));

        var ignore = SnapshotIgnore.Variables | SnapshotIgnore.Scripts;
        Assert.Equal(Snapshot(shell, "Shop API", ignore), Snapshot(shell, "Shop API (2)", ignore));
        var copy = Snapshot(shell, "Shop API (2)");
        Assert.DoesNotContain("var baseUrl=", copy, StringComparison.Ordinal);
        Assert.Contains("yaapic.variables.set(\"baseUrl\"", copy, StringComparison.Ordinal);
        Assert.Contains("pre-list", copy, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("openapi-json")]
    [InlineData("openapi-yaml")]
    public async Task OpenApiRoundTripKeepsNamesMethodsPathsAndBodyKinds(string format)
    {
        var shell = await BuildWorkspaceAsync();
        var (name, content, _) = await ExportAsync(shell, format, "Pet Store");

        await ImportFileAsync(shell, Save(name, content));

        var ignore = SnapshotIgnore.Auth | SnapshotIgnore.Variables | SnapshotIgnore.Headers | SnapshotIgnore.Bodies | SnapshotIgnore.Urls;
        Assert.Equal(Snapshot(shell, "Pet Store", ignore), Snapshot(shell, "Pet Store (2)", ignore));
        var copy = Flatten(shell.Collections.Roots.Single(r => r.Name == "Pet Store (2)").Children)
            .Where(n => !n.IsFolder).Select(n => n.Node.Request!).ToList();
        Assert.Contains(copy, r => r.Name == "Create pet" && r.Body.Kind == BodyKind.Raw && r.Body.ContentType == RawContentType.Json);
        Assert.Contains(copy, r => r.Name == "Get owner" && r.Url.Contains("{{ownerId}}", StringComparison.Ordinal));
        Assert.All(copy, r => Assert.StartsWith("{{baseUrl}}/", r.Url, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenApiYamlExportEqualsTheJsonExport()
    {
        var shell = await BuildWorkspaceAsync();
        var json = await ExportAsync(shell, "openapi-json", "Pet Store");
        var yaml = await ExportAsync(shell, "openapi-yaml", "Pet Store");

        var fromYaml = new VYamlTreeReader().Parse(Encoding.UTF8.GetString(yaml.Content));
        var fromJson = DataNodeJson.Parse(Encoding.UTF8.GetString(json.Content));

        Assert.EndsWith(".yaml", yaml.Name, StringComparison.Ordinal);
        Assert.EndsWith(".json", json.Name, StringComparison.Ordinal);
        Assert.Equal(DataNodeJson.Write(fromJson, indented: false), DataNodeJson.Write(fromYaml, indented: false));
    }

    [Fact]
    public async Task NoExportContainsTheVaultSecretAndTheRowIsKeptEmpty()
    {
        var shell = await BuildWorkspaceAsync();
        var sources = new List<(string Name, byte[] Content)>();
        foreach (var format in CollectionFormats)
        {
            var collection = format.StartsWith("openapi", StringComparison.Ordinal) ? "Pet Store" : "Shop API";
            var e = await ExportAsync(shell, format, collection);
            sources.Add((e.Name, e.Content));
        }

        var env = await ExportAsync(shell, "postman-environment", null, "Staging");
        sources.Add((env.Name, env.Content));

        foreach (var (name, content) in sources)
        {
            Assert.All(AllText(name, content), t => Assert.DoesNotContain(SecretValue, t, StringComparison.Ordinal));
        }

        var row = DataNodeJson.Parse(Encoding.UTF8.GetString(env.Content))["values"]!.Items.Single(v => v.GetString("key") == "apiSecret");
        Assert.Equal("", row.GetString("value"));
        Assert.Equal("secret", row.GetString("type"));
        Assert.Contains(env.Export.Report.Rows, r => r.Severity == ImportSeverity.Info && r.Message == Strings.ExportSecretsOmitted(1));
        var kept = shell.Environments.Environments.First(e => e.Name == "Staging").Variables.ToEntries().Single(r => r.Key == "apiSecret");
        Assert.Equal(SecretValue, kept.Value);
    }

    [Theory]
    [InlineData("postman", "Shop API")]
    [InlineData("openapi-json", "Pet Store")]
    [InlineData("openapi-yaml", "Pet Store")]
    [InlineData("insomnia", "Billing")]
    [InlineData("bruno", "Library")]
    [InlineData("postman-environment", null)]
    public async Task ExportingTwiceGivesIdenticalBytes(string format, string? collection)
    {
        var shell = await BuildWorkspaceAsync();

        var first = await ExportAsync(shell, format, collection, "Staging");
        var second = await ExportAsync(shell, format, collection, "Staging");

        Assert.Equal(first.Name, second.Name);
        Assert.Equal(first.Content, second.Content);
    }

    [Fact]
    public async Task BrunoZipHoldsTheExpectedEntriesInsideOneRootDirectory()
    {
        var shell = await BuildWorkspaceAsync();
        var (_, content, _) = await ExportAsync(shell, "bruno", "Library");

        using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        var entries = zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToList();

        var root = entries[0].Split('/')[0];
        Assert.All(entries, e =>
        {
            Assert.StartsWith(root + "/", e, StringComparison.Ordinal);
            Assert.DoesNotContain("..", e.Split('/'));
            Assert.DoesNotContain('\\', e);
            Assert.False(Path.IsPathRooted(e));
        });
        var relative = entries.Select(e => e[(root.Length + 1)..]).ToList();
        Assert.Contains("bruno.json", relative);
        Assert.Contains(relative, e => e.StartsWith("environments/", StringComparison.Ordinal) && e.EndsWith(".bru", StringComparison.Ordinal));
        Assert.Contains(relative, e => e.EndsWith("get-book.bru", StringComparison.Ordinal));
        Assert.Contains(relative, e => e.EndsWith("add-book.bru", StringComparison.Ordinal));
        Assert.Contains(relative, e => e.EndsWith("folder.bru", StringComparison.Ordinal));
        Assert.Equal(relative.Count, relative.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task PostmanExportFollowsTheV21Structure()
    {
        var shell = await BuildWorkspaceAsync();
        var (name, content, _) = await ExportAsync(shell, "postman", "Shop API");

        var doc = DataNodeJson.Parse(Encoding.UTF8.GetString(content));

        Assert.EndsWith(".json", name, StringComparison.Ordinal);
        Assert.Contains("v2.1", doc.GetString("info", "schema"), StringComparison.Ordinal);
        Assert.Equal("Shop API", doc.GetString("info", "name"));
        Assert.True(doc["item"]!.IsArray);
        var requests = Requests(doc["item"]!).ToList();
        Assert.Equal(3, requests.Count);
        Assert.All(requests, r =>
        {
            Assert.False(string.IsNullOrEmpty(r["request"]!.GetString("method")));
            Assert.False(string.IsNullOrEmpty(r["request"]!.GetString("url", "raw")));
        });
    }

    private static IEnumerable<DataNode> Requests(DataNode items) =>
        items.Items.SelectMany(i => i["request"] is not null ? [i] : Requests(i["item"] ?? DataNode.Array([])));

    [Fact]
    public async Task CancellingTheSaveWritesNothingAndReportsNoError()
    {
        var shell = await BuildWorkspaceAsync();
        _fx.Dialogs.SaveFileAnswer = false;
        var export = shell.CreateExport();
        await export.LoadAsync(null);
        export.SelectedCollection = export.Collections.Single(c => c.Name == "Shop API");

        await export.ExportCommand.ExecuteAsync(null);

        Assert.Null(export.Error);
        Assert.False(export.Saved);
        Assert.Null(export.SavedName);
        Assert.False(Directory.Exists(Path.Combine(_inputs, "exports")));
    }

    [Fact]
    public async Task ACollectionDeletedBetweenLoadAndExportIsReported()
    {
        var shell = await BuildWorkspaceAsync();
        var export = shell.CreateExport();
        await export.LoadAsync(null);
        export.SelectedCollection = export.Collections.Single(c => c.Name == "Library");
        var dirs = Directory.GetDirectories(_fx.PathOf("requests"))
            .Where(d => File.ReadAllText(Path.Combine(d, "folder.yaml")).Contains("name: Library", StringComparison.Ordinal));
        foreach (var dir in dirs.ToList())
        {
            Directory.Delete(dir, recursive: true);
        }

        await export.ExportCommand.ExecuteAsync(null);

        Assert.Equal(Strings.ExportCollectionNotFound, export.Error);
        Assert.False(export.Saved);
        Assert.Empty(_fx.Dialogs.SavedFiles);
    }
}
