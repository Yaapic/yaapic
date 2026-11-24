using Yaapic.Core.Import;
using Yaapic.Core.Secrets;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Localization;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Import;
using Yaapic.ViewModels.Shell;
using static Yaapic.Infrastructure.Tests.EndToEnd.ScriptedShellFixture;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>
/// Imports every supported format through the real shell (import view model, importers, appliers, file stores) on a temporary
/// workspace, then sends imported requests to the loopback server.
/// </summary>
public sealed class Milestone5ImportEndToEndTests : IClassFixture<LoopbackServer>, IDisposable
{
    private const string Passphrase = "correct horse battery";

    private readonly LoopbackServer _server;
    private readonly ScriptedShellFixture _fx = new();
    private readonly string _inputs = Path.Combine(Path.GetTempPath(), "yaapic-e2e5-inputs-" + Guid.NewGuid().ToString("N"));

    public Milestone5ImportEndToEndTests(LoopbackServer server)
    {
        _server = server;
        Directory.CreateDirectory(_inputs);
    }

    public void Dispose()
    {
        _fx.Dispose();
        TempWorkspace.Delete(_inputs);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Base(string text) => text.Replace("@@BASE@@", _server.BaseUrl, StringComparison.Ordinal);

    private string Sample(string fileName, string text)
    {
        var path = Path.Combine(_inputs, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private async Task<ImportViewModel> PreviewFileAsync(ShellViewModel shell, string path)
    {
        _fx.Dialogs.PickFileAnswer = path;
        var import = shell.CreateImport();
        await import.ChooseFileCommand.ExecuteAsync(null);
        return import;
    }

    private async Task<ImportViewModel> ImportFileAsync(ShellViewModel shell, string path)
    {
        var import = await PreviewFileAsync(shell, path);
        Assert.Null(import.Error);
        await import.ImportCommand.ExecuteAsync(null);
        Assert.True(import.IsDone, import.Error);
        return import;
    }

    private async Task<ImportViewModel> ImportTextAsync(ShellViewModel shell, string text)
    {
        var import = shell.CreateImport();
        import.PastedText = text;
        await import.PreviewPastedCommand.ExecuteAsync(null);
        Assert.Null(import.Error);
        await import.ImportCommand.ExecuteAsync(null);
        Assert.True(import.IsDone, import.Error);
        return import;
    }

    private static IEnumerable<CollectionNodeViewModel> Flatten(IEnumerable<CollectionNodeViewModel> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));

    private static List<CollectionNodeViewModel> Folders(ShellViewModel shell) => [.. Flatten(shell.Collections.Roots).Where(n => n.IsFolder)];

    private static List<CollectionNodeViewModel> RequestNodes(ShellViewModel shell) => [.. Flatten(shell.Collections.Roots).Where(n => !n.IsFolder)];

    private static HttpRequestDefinition Request(ShellViewModel shell, string name) =>
        RequestNodes(shell).Single(n => n.Name == name).Node.Request!;

    private static string[] FolderNames(ShellViewModel shell) => [.. Folders(shell).Select(f => f.Name)];

    private static void Activate(ShellViewModel shell, string environment) =>
        shell.Environments.ActiveEnvironment = shell.Environments.Environments.Single(e => e.Name == environment);

    private static int Count(ImportViewModel import, ImportSeverity severity) => import.Outcome!.Report.Count(severity);

    private static string Dump(ImportViewModel import) => string.Join(Environment.NewLine, import.Outcome!.Report.Entries.Select(e => $"{e.Severity} {e.Path}: {e.Message}"));

    private static async Task SetUpVaultAsync(ShellViewModel shell)
    {
        var vault = shell.Vault;
        vault.SelectedProtector = vault.Protectors.Single(p => p.Id == "passphrase");
        vault.Passphrase = Passphrase;
        vault.ConfirmPassphrase = Passphrase;
        await vault.SetupCommand.ExecuteAsync(null);
        Assert.Equal(VaultState.Unlocked, vault.State);
    }

    private Task<ShellViewModel> NewShellAsync() => _fx.NewShellAsync(protectors: new PassphraseKeyProtectorFactory(100_000));

    [Fact]
    public async Task PostmanCollectionLandsFoldersRequestsAuthBodiesAndVariables()
    {
        var shell = await NewShellAsync();

        var import = await ImportFileAsync(shell, Sample("shop.postman_collection.json", Base(Milestone5Samples.PostmanCollection)));

        Assert.Equal("postman", import.DetectedId);
        Assert.Equal((2, 3, 0), (import.Outcome!.Folders, import.Outcome.Requests, import.Outcome.Environments));
        Assert.Equal(["Shop API", "Products"], FolderNames(shell));
        var list = Request(shell, "List products");
        Assert.Equal((HttpMethodKind.Get, "{{baseUrl}}/echo"), (list.Method, list.Url));
        Assert.Equal([new KeyValueEntry("page", "1"), new KeyValueEntry("limit", "10")], list.QueryParams);
        Assert.Equal((AuthKind.Bearer, "{{token}}"), (list.Auth.Kind, list.Auth.Token));
        Assert.False(list.Scripts.PreRequest.IsEmpty);
        var create = Request(shell, "Create product");
        Assert.Equal((HttpMethodKind.Post, BodyKind.Raw, RawContentType.Json), (create.Method, create.Body.Kind, create.Body.ContentType));
        Assert.Equal("""{"name":"Widget"}""", create.Body.Text);
        Assert.Equal(AuthKind.Bearer, create.Auth.Kind);
        Assert.False(create.Scripts.PostResponse.IsEmpty);
        var collection = Folders(shell).Single(f => f.Name == "Shop API");
        Assert.Contains(shell.Collections.FindCollectionVariables(list.Id), v => v.Key == "baseUrl" && v.Value == _server.BaseUrl);
        Assert.NotNull(collection);
        Assert.True(Count(import, ImportSeverity.Warning) == 2, Dump(import));
        Assert.True(Count(import, ImportSeverity.Info) == 2, Dump(import));
    }

    [Fact]
    public async Task ImportedPostmanScriptsRunWhenTheRequestIsSent()
    {
        var shell = await NewShellAsync();
        await ImportFileAsync(shell, Sample("env.postman_environment.json", Milestone5Samples.PostmanEnvironment));
        await ImportFileAsync(shell, Sample("shop.postman_collection.json", Base(Milestone5Samples.PostmanCollection)));
        Activate(shell, "Staging");

        var list = Open(shell, Request(shell, "List products").Id);
        await list.SendCommand.ExecuteAsync(null);
        var echoed = Json(list);
        Assert.Equal("Bearer tok-123", echoed.GetProperty("headers").GetProperty("Authorization").GetString());
        Assert.Contains("page=1", echoed.GetProperty("path").GetString(), StringComparison.Ordinal);
        Assert.Contains("pre-list", Messages(shell, ConsoleLineKind.Log));

        var create = Open(shell, Request(shell, "Create product").Id);
        await create.SendCommand.ExecuteAsync(null);
        Assert.Equal("""{"name":"Widget"}""", Json(create).GetProperty("body").GetString());
        Assert.Contains("lastStatus", await File.ReadAllTextAsync(_fx.PathOf("environments", "staging.env.yaml"), Ct), StringComparison.Ordinal);
        Assert.Contains("200", await File.ReadAllTextAsync(_fx.PathOf("environments", "staging.env.yaml"), Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostmanMultipartFileReferenceCarriesAWarning()
    {
        var shell = await NewShellAsync();

        var import = await ImportFileAsync(shell, Sample("shop.json", Base(Milestone5Samples.PostmanCollection)));

        var upload = Request(shell, "Upload avatar");
        Assert.Equal(BodyKind.FormData, upload.Body.Kind);
        Assert.Contains(upload.Body.Fields, f => f.Kind == FormFieldKind.File && f.Value == "./avatar.png");
        Assert.Contains(import.Outcome!.Report.Entries, e => e.Severity == ImportSeverity.Warning && e.Message.Contains("./avatar.png", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenApiYamlAndSwagger2JsonLandTheSameApi()
    {
        var shell = await NewShellAsync();

        var yaml = await ImportFileAsync(shell, Sample("pets.yaml", Milestone5Samples.OpenApiYaml));
        var swagger = await ImportFileAsync(shell, Sample("pets-v2.json", Milestone5Samples.Swagger2Json));

        Assert.Equal("openapi", yaml.DetectedId);
        Assert.Equal("openapi", swagger.DetectedId);
        Assert.Equal((3, 3), (yaml.Outcome!.Requests, swagger.Outcome!.Requests));
        Assert.Equal(["Pet Store", "owners", "pets", "Pet Store (2)", "owners", "pets"], FolderNames(shell));
        Assert.Equal(2, RequestNodes(shell).Count(n => n.Name == "Get owner" && n.Url.Contains("{{ownerId}}", StringComparison.Ordinal)));
        var all = RequestNodes(shell).Select(n => n.Node.Request!).ToList();
        Assert.Equal(2, all.Count(r => r.Name == "Create pet" && r.Body.Kind == BodyKind.Raw && r.Body.ContentType == RawContentType.Json));
        Assert.Contains(all, r => r.Auth.Kind == AuthKind.Bearer);
        Assert.Contains(all, r => r.Auth.Kind == AuthKind.Basic);
        Assert.Contains(swagger.Outcome.Report.Entries, e => e.Severity == ImportSeverity.Info && e.Message.Contains("Pet Store (2)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CurlCommandIsSentToTheLoopbackServerWithItsHeadersAndBody()
    {
        var shell = await NewShellAsync();

        var import = await ImportTextAsync(shell, Base(Milestone5Samples.Curl));

        Assert.Equal("curl", import.DetectedId);
        Assert.Equal(1, import.Outcome!.Requests);
        var tab = Open(shell, RequestNodes(shell).Single().Id);
        await tab.SendCommand.ExecuteAsync(null);
        var echo = Json(tab);
        Assert.Equal("POST", echo.GetProperty("method").GetString());
        Assert.Equal("/echo?x=1", echo.GetProperty("path").GetString());
        Assert.Equal("abc-123", echo.GetProperty("headers").GetProperty("x-trace").GetString());
        Assert.StartsWith("application/json", echo.GetProperty("headers").GetProperty("Content-Type").GetString(), StringComparison.Ordinal);
        Assert.Equal("""{"hello":"world"}""", echo.GetProperty("body").GetString());
    }

    [Fact]
    public async Task HarFiltersStaticResourcesPreflightsAndDuplicatesUnlessAsked()
    {
        var shell = await NewShellAsync();
        var path = Sample("capture.har", Milestone5Samples.Har);

        var preview = await PreviewFileAsync(shell, path);
        Assert.Equal("har", preview.DetectedId);
        Assert.True(preview.ShowStaticResourcesOption);
        Assert.Equal(2, preview.Requests);
        Assert.True(preview.Report.HasRows);

        preview.IncludeStaticResources = true;
        await preview.Completion;
        Assert.Equal(3, preview.Requests);

        preview.IncludeStaticResources = false;
        await preview.Completion;
        await preview.ImportCommand.ExecuteAsync(null);

        Assert.Equal(2, RequestNodes(shell).Count);
        Assert.Equal(["capture"], FolderNames(shell));
        Assert.Contains(RequestNodes(shell), n => n.Node.Request is { Method: HttpMethodKind.Post, Url: "https://api.example.test/users" });
        Assert.DoesNotContain(RequestNodes(shell), n => n.Url.Contains("logo.png", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InsomniaExportLandsFoldersRequestsAndTheStagingEnvironment()
    {
        var shell = await NewShellAsync();

        var import = await ImportFileAsync(shell, Sample("billing.insomnia.json", Milestone5Samples.Insomnia));

        Assert.Equal("insomnia", import.DetectedId);
        Assert.True((2, 2, 1) == (import.Outcome!.Folders, import.Outcome.Requests, import.Outcome.Environments), Dump(import) + " | " + string.Join(',', shell.Environments.Environments.Select(e => e.Name)));
        Assert.Equal(["Billing", "Invoices"], FolderNames(shell));
        Assert.Equal("{{baseUrl}}/invoices", Request(shell, "List invoices").Url);
        Assert.Equal((AuthKind.Bearer, "{{token}}"), (Request(shell, "List invoices").Auth.Kind, Request(shell, "List invoices").Auth.Token));
        var create = Request(shell, "Create invoice");
        Assert.Equal(("""{"amount": 10}""", RawContentType.Json), (create.Body.Text, create.Body.ContentType));
        var environment = Assert.Single(shell.Environments.Environments);
        Assert.Equal("Staging", environment.Name);
        Assert.Contains(environment.Variables.ToEntries(), v => v.Key == "baseUrl" && v.Value == "https://staging.billing.example.test");
        var collection = shell.Collections.FindCollectionVariables(Request(shell, "List invoices").Id);
        Assert.Contains(collection, v => v.Key == "baseUrl" && v.Value == "https://billing.example.test");
        Assert.Contains(collection, v => v.Key == "token" && v.Value == "base-token");
        Assert.Equal(1, Count(import, ImportSeverity.Warning));
    }

    [Fact]
    public async Task BrunoFolderLandsNestedFoldersRequestsAndEnvironments()
    {
        var shell = await NewShellAsync();
        Sample("library/bruno.json", Milestone5Samples.BrunoJson);
        Sample("library/books/folder.bru", Milestone5Samples.BrunoFolder);
        Sample("library/books/get-book.bru", Milestone5Samples.BrunoGet);
        Sample("library/books/add-book.bru", Milestone5Samples.BrunoPost);
        Sample("library/environments/local.bru", Milestone5Samples.BrunoEnvironment);
        _fx.Dialogs.PickFolderAnswer = Path.Combine(_inputs, "library");
        var import = shell.CreateImport();

        await import.ChooseFolderCommand.ExecuteAsync(null);
        Assert.Null(import.Error);
        await import.ImportCommand.ExecuteAsync(null);

        Assert.True(import.IsDone, import.Error);
        Assert.Equal("bruno", import.DetectedId);
        Assert.Equal((2, 2, 1), (import.Outcome!.Folders, import.Outcome.Requests, import.Outcome.Environments));
        Assert.Equal(["Library", "Books"], FolderNames(shell));
        var add = Request(shell, "Add book");
        Assert.Equal((HttpMethodKind.Post, BodyKind.Raw, RawContentType.Json), (add.Method, add.Body.Kind, add.Body.ContentType));
        Assert.Contains("Dune", add.Body.Text, StringComparison.Ordinal);
        Assert.Equal("https://library.example.test/books/1", Request(shell, "Get book").Url);
        Assert.Equal("local", Assert.Single(shell.Environments.Environments).Name);
    }

    [Fact]
    public async Task SecretEnvironmentRowIsBlankedWithAWarningWhenTheVaultIsLocked()
    {
        var shell = await NewShellAsync();

        var import = await ImportFileAsync(shell, Sample("env.json", Milestone5Samples.PostmanEnvironment));

        Assert.Equal("postman-environment", import.DetectedId);
        Assert.Equal(1, Count(import, ImportSeverity.Warning));
        var rows = shell.Environments.Environments.Single().Variables.ToEntries();
        var secret = rows.Single(r => r.Key == "apiSecret");
        Assert.Equal(("", false), (secret.Value, secret.IsSecret));
        Assert.DoesNotContain("s3cr3t-VALUE", await File.ReadAllTextAsync(_fx.PathOf("environments", "staging.env.yaml"), Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecretEnvironmentRowGoesToTheVaultWhenItIsUnlocked()
    {
        var shell = await NewShellAsync();
        await SetUpVaultAsync(shell);

        var import = await ImportFileAsync(shell, Sample("env.json", Milestone5Samples.PostmanEnvironment));

        Assert.Equal(0, Count(import, ImportSeverity.Warning));
        var file = await File.ReadAllTextAsync(_fx.PathOf("environments", "staging.env.yaml"), Ct);
        Assert.DoesNotContain("s3cr3t-VALUE", file, StringComparison.Ordinal);
        Assert.Contains("secret: true", file, StringComparison.Ordinal);
        var secret = shell.Environments.Environments.Single().Variables.ToEntries().Single(r => r.Key == "apiSecret");
        Assert.Equal(("s3cr3t-VALUE", true), (secret.Value, secret.IsSecret));
        Assert.DoesNotContain("s3cr3t-VALUE", await File.ReadAllTextAsync(_fx.VaultFile, Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportingTheSameCollectionTwiceCreatesANumberedCopy()
    {
        var shell = await NewShellAsync();
        var path = Sample("shop.json", Base(Milestone5Samples.PostmanCollection));

        await ImportFileAsync(shell, path);
        var second = await ImportFileAsync(shell, path);

        Assert.Equal(["Shop API", "Products", "Shop API (2)", "Products"], FolderNames(shell));
        Assert.Equal(6, RequestNodes(shell).Count);
        Assert.Contains(second.Outcome!.Report.Entries, e => e.Severity == ImportSeverity.Info && e.Message.Contains("Shop API (2)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AutoDetectionPicksTheRightImporterForEverySample()
    {
        var shell = await NewShellAsync();
        Sample("bruno/bruno.json", Milestone5Samples.BrunoJson);
        Sample("bruno/get.bru", Milestone5Samples.BrunoGet);
        var cases = new (string Id, ImportViewModel Import)[]
        {
            ("postman", await PreviewFileAsync(shell, Sample("a.json", Base(Milestone5Samples.PostmanCollection)))),
            ("postman-environment", await PreviewFileAsync(shell, Sample("b.json", Milestone5Samples.PostmanEnvironment))),
            ("openapi", await PreviewFileAsync(shell, Sample("c.yaml", Milestone5Samples.OpenApiYaml))),
            ("openapi", await PreviewFileAsync(shell, Sample("d.json", Milestone5Samples.Swagger2Json))),
            ("har", await PreviewFileAsync(shell, Sample("e.har", Milestone5Samples.Har))),
            ("insomnia", await PreviewFileAsync(shell, Sample("f.json", Milestone5Samples.Insomnia))),
        };
        foreach (var (id, import) in cases)
        {
            Assert.Null(import.Error);
            Assert.Equal(id, import.DetectedId);
        }

        var curl = shell.CreateImport();
        curl.PastedText = Base(Milestone5Samples.Curl);
        await curl.PreviewPastedCommand.ExecuteAsync(null);
        Assert.Equal("curl", curl.DetectedId);

        _fx.Dialogs.PickFolderAnswer = Path.Combine(_inputs, "bruno");
        var bruno = shell.CreateImport();
        await bruno.ChooseFolderCommand.ExecuteAsync(null);
        Assert.Equal("bruno", bruno.DetectedId);
    }

    [Fact]
    public async Task GarbageIsNotRecognizedAndAManualFormatFailsCleanly()
    {
        var shell = await NewShellAsync();
        var import = shell.CreateImport();
        import.PastedText = "this is certainly not an API description";

        await import.PreviewPastedCommand.ExecuteAsync(null);

        Assert.Equal(Strings.ImportFormatNotRecognized, import.Error);
        Assert.False(import.HasPreview);
        Assert.False(import.ImportCommand.CanExecute(null));

        import.SelectedFormat = import.Formats.Single(f => f.Id == "postman");
        await import.Completion;

        Assert.NotNull(import.Error);
        Assert.NotEqual(Strings.ImportFormatNotRecognized, import.Error);
        Assert.False(import.HasPreview);
        Assert.False(import.ImportCommand.CanExecute(null));
        Assert.Empty(RequestNodes(shell));
    }

    [Fact]
    public async Task AFolderOfMoreThan5000FilesIsRefused()
    {
        var shell = await NewShellAsync();
        var folder = Path.Combine(_inputs, "huge");
        Directory.CreateDirectory(folder);
        for (var i = 0; i < 5001; i++)
        {
            File.WriteAllText(Path.Combine(folder, $"r{i}.bru"), "meta {\n  name: r\n}\n");
        }

        _fx.Dialogs.PickFolderAnswer = folder;
        var import = shell.CreateImport();
        await import.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Equal(Strings.ImportFolderTooLarge, import.Error);
        Assert.False(import.HasPreview);
    }

    [Fact]
    public async Task MoreThan20000RequestsAreRefused()
    {
        var shell = await NewShellAsync();
        var items = string.Join(',', Enumerable.Range(0, 20001).Select(i => "{\"name\":\"r" + i + "\",\"request\":{\"method\":\"GET\",\"url\":\"https://a.test/" + i + "\"}}"));
        var import = shell.CreateImport();
        import.PastedText = """{"info":{"name":"Big","schema":"https://schema.getpostman.com/json/collection/v2.1.0/collection.json"},"item":[""" + items + "]}";

        await import.PreviewPastedCommand.ExecuteAsync(null);

        Assert.Equal(Strings.ImportTooLarge(20001), import.Error);
        Assert.False(import.ImportCommand.CanExecute(null));
        Assert.Empty(RequestNodes(shell));
    }

    [Fact]
    public async Task OpenApiReferencesToAUrlOrAFileAreReportedAndNeverFollowed()
    {
        var shell = await NewShellAsync();
        var before = _server.Hits.Count;

        var import = await ImportFileAsync(shell, Sample("evil.json", Base(Milestone5Samples.OpenApiMaliciousRefs)));

        Assert.Equal(2, Count(import, ImportSeverity.Warning));
        Assert.Equal(2, import.Outcome!.Requests);
        Assert.Equal(before, _server.Hits.Count);
    }
}
