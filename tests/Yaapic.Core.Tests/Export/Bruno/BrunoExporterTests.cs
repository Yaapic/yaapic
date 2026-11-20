using System.IO.Compression;
using System.Text;
using Yaapic.Core.Export;
using Yaapic.Core.Export.Bruno;
using Yaapic.Core.Import;
using Yaapic.Core.Import.Bruno;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Export.Bruno;

public class BrunoExporterTests
{
    private static readonly BrunoExporter Exporter = new();
    private static readonly ScriptBlock Js = new(ScriptLanguage.JavaScript, "yaapic.log(1);");

    private static HttpRequestDefinition Req(string name, HttpMethodKind method = HttpMethodKind.Get, string url = "https://x.test",
        RequestBody? body = null, ScriptSet? scripts = null) =>
        new(Guid.NewGuid(), name, method, url, [], [], body ?? RequestBody.None, RequestAuth.None, RequestSettings.Default,
            scripts ?? ScriptSet.Empty);

    private static CollectionNode Request(string name, ScriptSet? scripts = null) => CollectionNode.ForRequest(Req(name, scripts: scripts));

    private static CollectionNode Folder(string name, params CollectionNode[] children) =>
        CollectionNode.ForFolder(new FolderDefinition(Guid.NewGuid(), name, "", []), children);

    private static CollectionNode Root(IReadOnlyList<KeyValueEntry> variables, ScriptSet? scripts, params CollectionNode[] children) =>
        CollectionNode.ForFolder(new FolderDefinition(Guid.NewGuid(), "My API", "", variables, scripts!), children);

    private static ExportResult Run(CollectionNode root, IReadOnlyList<EnvironmentDefinition>? environments = null,
        IReadOnlyList<KeyValueEntry>? globals = null) =>
        Exporter.Export(new ExportSource(root, environments ?? [], globals ?? [], root.Name));

    private static string Text(ExportResult result, string path) =>
        Encoding.UTF8.GetString(result.Files.Single(f => f.Path == path).Content);

    private static CollectionNode Tree() =>
        Root([new("base", "https://x"), new("off", "1", false)], new ScriptSet(Js, ScriptBlock.Empty),
            Folder("Sub", Request("Get users"), Request("Get users"), Request("folder")), Request("Top"));

    [Fact]
    public void Identity()
    {
        Assert.Equal("bruno", Exporter.Id);
        Assert.Equal("Bruno collection (.zip)", Exporter.DisplayName);
        Assert.Equal(".zip", Exporter.FileExtension);
        Assert.True(Exporter.RequiresCollection);
    }

    [Fact]
    public void NeedsACollection() =>
        Assert.Throws<ExportException>(() => Exporter.Export(new ExportSource(null, [], [], "x")));

    [Fact]
    public void WritesTheFilesInOrderWithUniqueSlugs()
    {
        var env = new EnvironmentDefinition(Guid.NewGuid(), "Dev", [new("host", "h")]);

        var result = Run(Tree(), [env]);

        Assert.Equal(
            [
                "my-api/bruno.json", "my-api/collection.bru", "my-api/sub/folder.bru", "my-api/sub/folder-2.bru",
                "my-api/sub/get-users.bru", "my-api/sub/get-users-2.bru", "my-api/top.bru", "my-api/environments/dev.bru",
            ],
            result.Files.Select(f => f.Path));
    }

    [Fact]
    public void WritesTheManifest() =>
        Assert.Equal(
            "{\n  \"version\": \"1\",\n  \"name\": \"My API\",\n  \"type\": \"collection\",\n  \"ignore\": [\n    \"node_modules\",\n    \".git\"\n  ]\n}\n",
            Text(Run(Tree()), "my-api/bruno.json"));

    [Fact]
    public void WritesCollectionVariablesAndScripts()
    {
        var text = Text(Run(Tree()), "my-api/collection.bru");

        Assert.StartsWith("vars:pre-request {\n  base: https://x\n}\n\nscript:pre-request {\n", text, StringComparison.Ordinal);
        Assert.Contains("\n  yaapic.log(1);\n}\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("off", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesNoCollectionFileWithoutVariablesOrScripts() =>
        Assert.DoesNotContain(Run(Root([], null, Request("A"))).Files, f => f.Path.EndsWith("collection.bru", StringComparison.Ordinal));

    [Fact]
    public void NumbersFoldersAndRequestsSeparately()
    {
        var result = Run(Root([], null, Request("A"), Folder("F1"), Request("B"), Folder("F2")));

        Assert.Contains("seq: 1\n", Text(result, "my-api/a.bru"), StringComparison.Ordinal);
        Assert.Contains("seq: 2\n", Text(result, "my-api/b.bru"), StringComparison.Ordinal);
        Assert.Equal("meta {\n  name: F1\n  seq: 1\n}\n", Text(result, "my-api/f1/folder.bru"));
        Assert.Equal("meta {\n  name: F2\n  seq: 2\n}\n", Text(result, "my-api/f2/folder.bru"));
    }

    [Fact]
    public void WritesFolderScripts()
    {
        var folder = CollectionNode.ForFolder(
            new FolderDefinition(Guid.NewGuid(), "F", "", [], new ScriptSet(ScriptBlock.Empty, Js)), []);

        var text = Text(Run(Root([], null, folder)), "my-api/f/folder.bru");

        Assert.Contains("\n\nscript:post-response {\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesEnvironmentsWithSecretNames()
    {
        var env = new EnvironmentDefinition(Guid.NewGuid(), "Dev env",
            [new("host", "h"), new("off", "x", false), new("token", "", true, true), new("key", "", true, true)]);

        var result = Run(Tree(), [env]);

        Assert.Equal("vars {\n  host: h\n  ~off: x\n}\n\nvars:secret [\n  token,\n  key\n]\n",
            Text(result, "my-api/environments/dev-env.bru"));
    }

    [Fact]
    public void KeepsEnvironmentNamesUnique()
    {
        var a = new EnvironmentDefinition(Guid.NewGuid(), "Dev", [new("a", "1")]);
        var b = new EnvironmentDefinition(Guid.NewGuid(), "dev", [new("a", "2")]);

        var result = Run(Tree(), [a, b]);

        Assert.Contains(result.Files, f => f.Path == "my-api/environments/dev-2.bru");
    }

    [Fact]
    public void IgnoresGlobalsWithAnInfo()
    {
        var result = Run(Tree(), globals: [new("g", "1")]);

        Assert.DoesNotContain(result.Files, f => f.Path.Contains("global", StringComparison.Ordinal));
        Assert.Contains(result.Report.Entries, e => e.Severity == ImportSeverity.Info && e.Message.Contains("Global", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsTheExportedScriptCountOnce()
    {
        var both = new ScriptSet(Js, Js);
        var result = Run(Root([], both, Request("A", both)));

        var warning = Assert.Single(result.Report.Entries, e => e.Message.Contains("exported unchanged", StringComparison.Ordinal));
        Assert.StartsWith("4 scripts", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReservesTheEnvironmentsDirectory() =>
        Assert.Contains(Run(Root([], null, Folder("Environments"))).Files, f => f.Path == "my-api/environments-2/folder.bru");

    [Fact]
    public void AnEmptyCollectionYieldsTheManifestOnly() =>
        Assert.Equal(["my-api/bruno.json"], Run(Root([], null)).Files.Select(f => f.Path));

    [Fact]
    public void PackagesAsAZipWhoseEntriesAreThePaths()
    {
        var result = Run(Tree());

        var (name, content) = ExportPackager.Package(result, "My API", ".zip");

        Assert.Equal("my-api.zip", name);
        using var zip = new ZipArchive(new MemoryStream(content));
        Assert.Equal(result.Files.Select(f => f.Path), zip.Entries.Select(e => e.FullName));
    }

    [Fact]
    public void RoundTripThroughTheImporter()
    {
        var post = Req("Create", HttpMethodKind.Post, "{{base}}/users", RequestBody.Raw(RawContentType.Json, """{"a":1}"""));
        var sub = Folder("Sub", CollectionNode.ForRequest(post));
        var root = Root([new("base", "https://x")], null, sub, Request("Top"));
        var env = new EnvironmentDefinition(Guid.NewGuid(), "Dev", [new("host", "h"), new("token", "", true, true)]);

        var result = Run(root, [env]);
        var input = new ImportInput("my-api", [.. result.Files.Select(f =>
            new ImportFile(f.Path["my-api/".Length..], Encoding.UTF8.GetString(f.Content)))]);
        var bundle = new BrunoImporter().Import(input, new ImportContext(SequentialIds.Create(), ImportOptions.Default));

        Assert.Equal("My API", bundle.Collection!.Folder.Name);
        var folder = Assert.IsType<ImportFolder>(bundle.Collection.Children[0]);
        Assert.Equal("Sub", folder.Folder.Name);
        var imported = Assert.IsType<ImportRequest>(Assert.Single(folder.Children)).Request;
        Assert.Equal(post.Name, imported.Name);
        Assert.Equal(post.Method, imported.Method);
        Assert.Equal(post.Url, imported.Url);
        Assert.Equal(post.Body, imported.Body);
        Assert.Equal("Top", Assert.IsType<ImportRequest>(bundle.Collection.Children[1]).Request.Name);
        Assert.Contains("\"base\"", bundle.Collection.Folder.Scripts.PreRequest.Source, StringComparison.Ordinal);
        var environment = Assert.Single(bundle.Environments);
        Assert.Equal("dev", environment.Name);
        Assert.Equal([new KeyValueEntry("host", "h"), new KeyValueEntry("token", "", true, true)], environment.Variables);
    }
}
