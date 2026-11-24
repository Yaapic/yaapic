using System.Text;
using Yaapic.Core.Export;
using Yaapic.Core.Export.Postman;
using Yaapic.Core.Import;
using Yaapic.Core.Import.Postman;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Export.Postman;

public class PostmanScriptExportTests
{
    private static ScriptBlock Js(string source) => new(ScriptLanguage.JavaScript, source);

    private static DataNode? Events(ScriptSet set, ImportReport? report = null) =>
        PostmanScriptWriter.Events(set, "p", report ?? new ImportReport());

    private static string Compact(DataNode? node) => node is null ? "null" : DataNodeJson.Write(node, indented: false);

    [Fact]
    public void WritesBothPhasesPreRequestFirst() =>
        Assert.Equal(
            """[{"listen":"prerequest","script":{"type":"text/javascript","exec":["pm.a()"]}},{"listen":"test","script":{"type":"text/javascript","exec":["pm.b()","pm.c()"]}}]""",
            Compact(Events(new ScriptSet(Js("yaapic.a()"), Js("yaapic.b()\nyaapic.c()\n")))));

    [Fact]
    public void WritesOnlyTheNonEmptyPhase()
    {
        Assert.Equal("""[{"listen":"test","script":{"type":"text/javascript","exec":["x"]}}]""",
            Compact(Events(new ScriptSet(Js("  "), Js("x")))));
        Assert.Null(Events(ScriptSet.Empty));
    }

    [Fact]
    public void NormalizesCrlf() =>
        Assert.Equal("""[{"listen":"prerequest","script":{"type":"text/javascript","exec":["a","","b"]}}]""",
            Compact(Events(new ScriptSet(Js("a\r\n\r\nb\r\n"), ScriptBlock.Empty))));

    [Fact]
    public void SkipsLuaWithAWarning()
    {
        var report = new ImportReport();
        var events = Events(new ScriptSet(new ScriptBlock(ScriptLanguage.Lua, "print(1)"), Js("x")), report);

        Assert.Equal("""[{"listen":"test","script":{"type":"text/javascript","exec":["x"]}}]""", Compact(events));
        var entry = Assert.Single(report.Entries);
        Assert.Equal(ImportSeverity.Warning, entry.Severity);
        Assert.Contains("Lua", entry.Message);
    }

    [Fact]
    public void WritesTheThreeLevelsAndOneInfo()
    {
        var request = new HttpRequestDefinition(Guid.NewGuid(), "r", HttpMethodKind.Get, "https://x.test", [], [], RequestBody.None,
            RequestAuth.None, RequestSettings.Default, new ScriptSet(Js("yaapic.r()"), ScriptBlock.Empty));
        var sub = CollectionNode.ForFolder(
            new FolderDefinition(Guid.NewGuid(), "Sub", "", [], new ScriptSet(Js("yaapic.f()"), ScriptBlock.Empty)),
            [CollectionNode.ForRequest(request)]);
        var root = CollectionNode.ForFolder(
            new FolderDefinition(new Guid("00000000-0000-0000-0000-000000000001"), "API", "", [], new ScriptSet(ScriptBlock.Empty, Js("yaapic.c()"))),
            [sub]);

        var result = new PostmanCollectionExporter().Export(new ExportSource(root, [], [], "API"));
        var tree = DataNodeJson.Parse(Encoding.UTF8.GetString(result.Files[0].Content));

        Assert.Equal("""[{"listen":"test","script":{"type":"text/javascript","exec":["pm.c()"]}}]""", Compact(tree["event"]));
        var folder = tree["item"]![0]!;
        Assert.Equal(["name", "item", "event"], folder.Members.Select(m => m.Key));
        Assert.Equal("""[{"listen":"prerequest","script":{"type":"text/javascript","exec":["pm.f()"]}}]""", Compact(folder["event"]));
        var item = folder["item"]![0]!;
        Assert.Equal(["name", "request", "response", "event"], item.Members.Select(m => m.Key));
        Assert.Equal("""[{"listen":"prerequest","script":{"type":"text/javascript","exec":["pm.r()"]}}]""", Compact(item["event"]));
        Assert.Single(result.Report.Entries, e => e.Severity == ImportSeverity.Info);
    }

    [Fact]
    public void RoundTripsThroughTheImporter()
    {
        var request = new HttpRequestDefinition(Guid.NewGuid(), "r", HttpMethodKind.Get, "https://x.test", [], [], RequestBody.None,
            RequestAuth.None, RequestSettings.Default, new ScriptSet(Js("yaapic.environment.set(\"a\", 1);"), Js("yaapic.test(\"ok\", () => {});")));
        var root = CollectionNode.ForFolder(new FolderDefinition(Guid.NewGuid(), "API", "", []), [CollectionNode.ForRequest(request)]);
        var json = Encoding.UTF8.GetString(new PostmanCollectionExporter().Export(new ExportSource(root, [], [], "API")).Files[0].Content);

        var bundle = new PostmanCollectionImporter().Import(ImportInput.FromText("c.json", json),
            new ImportContext(SequentialIds.Create(), ImportOptions.Default));

        var imported = ((ImportRequest)Assert.Single(bundle.Collection!.Children)).Request;
        Assert.Equal(request.Scripts, imported.Scripts);
    }
}
