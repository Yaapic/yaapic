using System.Globalization;
using System.Text;
using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Export.Postman;

/// <summary>Writes a collection as a Postman Collection v2.1 JSON file.</summary>
public sealed class PostmanCollectionExporter : IExporter
{
    private const string Schema = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json";

    /// <inheritdoc />
    public string Id => "postman";

    /// <inheritdoc />
    public string DisplayName => Strings.ExportPostmanDisplayName;

    /// <inheritdoc />
    public string FileExtension => ".json";

    /// <inheritdoc />
    public bool RequiresCollection => true;

    /// <inheritdoc />
    public ExportResult Export(ExportSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var root = source.Collection ?? throw new ExportException(Strings.ExportCollectionNotFound);
        var report = new ImportReport();
        var fileRequests = 0;
        var scriptsWritten = false;

        List<(string, DataNode)> info =
        [
            ("_postman_id", DataNodeJson.Str(root.Id.ToString("D", CultureInfo.InvariantCulture))),
            ("name", DataNodeJson.Str(root.Name)),
        ];
        if (root.Folder!.Description.Length > 0) info.Add(("description", DataNodeJson.Str(root.Folder.Description)));
        info.Add(("schema", DataNodeJson.Str(Schema)));

        List<(string, DataNode)> top = [("info", DataNodeJson.Obj([.. info])), ("item", Items(root.Children, ""))];
        if (Events(root.Folder!.Scripts, root.Name) is { } rootEvents) top.Add(("event", rootEvents));
        if (root.Folder.Variables.Count > 0) top.Add(("variable", Variables(root.Folder.Variables)));
        var tree = DataNodeJson.Obj([.. top]);
        if (fileRequests > 0) report.Info("", Strings.ExportFileReference(fileRequests));
        if (scriptsWritten) report.Info("", Strings.ExportScriptsRewritten);

        var text = DataNodeJson.Write(tree, indented: true) + "\n";
        var name = ExportPackager.SafeFileName(source.Name) + ".postman_collection.json";
        return new ExportResult([new ExportFile(name, new UTF8Encoding(false).GetBytes(text))], report);

        static DataNode Variables(IReadOnlyList<KeyValueEntry> variables) => DataNodeJson.Arr([.. variables.Select(v =>
        {
            List<(string, DataNode)> members = [("key", DataNodeJson.Str(v.Key)), ("value", DataNodeJson.Str(v.Value))];
            members.Add(("type", DataNodeJson.Str(v.IsSecret ? "secret" : "string")));
            if (!v.Enabled) members.Add(("disabled", DataNode.Bool(true)));
            return DataNodeJson.Obj([.. members]);
        })]);

        DataNode? Events(ScriptSet scripts, string path)
        {
            var events = PostmanScriptWriter.Events(scripts, path, report);
            scriptsWritten |= events is not null;
            return events;
        }

        DataNode Items(IReadOnlyList<CollectionNode> nodes, string path) => DataNodeJson.Arr([.. nodes.Select(n => Item(n, ImportPath.Join(path, n.Name)))]);

        DataNode Item(CollectionNode node, string path)
        {
            if (node.Kind == CollectionNodeKind.Folder)
            {
                List<(string, DataNode)> folder = [("name", DataNodeJson.Str(node.Name))];
                if (node.Folder!.Description.Length > 0) folder.Add(("description", DataNodeJson.Str(node.Folder.Description)));
                folder.Add(("item", Items(node.Children, path)));
                if (Events(node.Folder.Scripts, path) is { } folderEvents) folder.Add(("event", folderEvents));
                return DataNodeJson.Obj([.. folder]);
            }

            var request = node.Request!;
            if (PostmanRequestWriter.ReferencesFile(request)) fileRequests++;
            List<(string, DataNode)> item =
            [
                ("name", DataNodeJson.Str(node.Name)),
                ("request", PostmanRequestWriter.Write(request)),
                ("response", DataNodeJson.Arr()),
            ];
            if (Events(request.Scripts, path) is { } events) item.Add(("event", events));
            return DataNodeJson.Obj([.. item]);
        }
    }
}
