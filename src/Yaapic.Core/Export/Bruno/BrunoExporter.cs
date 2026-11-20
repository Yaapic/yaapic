using System.Text;
using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Export.Bruno;

/// <summary>Writes a collection as a Bruno collection folder: <c>bruno.json</c>, <c>.bru</c> files and environments.</summary>
public sealed class BrunoExporter : IExporter
{
    /// <inheritdoc />
    public string Id => "bruno";

    /// <inheritdoc />
    public string DisplayName => Strings.ExportBrunoDisplayName;

    /// <inheritdoc />
    public string FileExtension => ".zip";

    /// <inheritdoc />
    public bool RequiresCollection => true;

    /// <inheritdoc />
    public ExportResult Export(ExportSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var collection = source.Collection ?? throw new ExportException(Strings.ExportCollectionNotFound);
        var report = new ImportReport();
        var root = ExportPackager.SafeFileName(collection.Name);
        var folder = collection.Folder!;

        // Reserved names: files Bruno reads itself, so no request or folder may take them.
        HashSet<string> used = [];
        List<ExportFile> files = [];
        var scripts = 0;
        Add($"{root}/bruno.json", DataNodeJson.Write(DataNodeJson.Obj(
            ("version", DataNodeJson.Str("1")),
            ("name", DataNodeJson.Str(collection.Name)),
            ("type", DataNodeJson.Str("collection")),
            ("ignore", DataNodeJson.Arr(DataNodeJson.Str("node_modules"), DataNodeJson.Str(".git")))), indented: true) + "\n");
        used.Add($"{root}/collection.bru");
        used.Add($"{root}/environments");
        used.Add($"{root}/folder.bru");

        scripts += Count(folder.Scripts);
        if (BruWriter.WriteCollection(folder.Variables, folder.Scripts, collection.Name, report) is { } collectionText)
        {
            Add($"{root}/collection.bru", collectionText);
        }

        Walk(collection.Children, root, collection.Name);

        HashSet<string> envNames = [];
        foreach (var environment in source.Environments)
        {
            var path = ExportPackager.UniquePath($"{root}/environments/{ExportPackager.SafeFileName(environment.Name)}.bru", envNames);
            Add(path, BruWriter.WriteEnvironment(environment.Variables));
        }

        if (scripts > 0) report.Warning("", Strings.ExportBrunoScripts(scripts));
        if (source.Globals.Count > 0) report.Info("", Strings.ExportBrunoGlobalsIgnored);
        return new ExportResult(files, report);

        void Add(string path, string text) => files.Add(new ExportFile(path, new UTF8Encoding(false).GetBytes(text)));

        void Walk(IReadOnlyList<CollectionNode> nodes, string dir, string reportPath)
        {
            var folders = 0;
            var requests = 0;
            foreach (var node in nodes)
            {
                var itemPath = ImportPath.Join(reportPath, node.Name);
                if (node.Kind == CollectionNodeKind.Folder)
                {
                    var sub = ExportPackager.UniquePath($"{dir}/{ExportPackager.SafeFileName(node.Name)}", used);
                    used.Add($"{sub}/folder.bru");
                    scripts += Count(node.Folder!.Scripts);
                    Add($"{sub}/folder.bru", BruWriter.WriteFolder(node.Name, ++folders, node.Folder.Scripts, itemPath, report));
                    Walk(node.Children, sub, itemPath);
                }
                else
                {
                    var path = ExportPackager.UniquePath($"{dir}/{ExportPackager.SafeFileName(node.Name)}.bru", used);
                    scripts += Count(node.Request!.Scripts);
                    Add(path, BruWriter.WriteRequest(node.Request, ++requests, itemPath, report));
                }
            }
        }
    }

    // Lua scripts are left out by the writer, so only JavaScript blocks count as exported.
    private static int Count(ScriptSet s) =>
        (s.PreRequest is { IsEmpty: false, Language: ScriptLanguage.JavaScript } ? 1 : 0)
        + (s.PostResponse is { IsEmpty: false, Language: ScriptLanguage.JavaScript } ? 1 : 0);
}
