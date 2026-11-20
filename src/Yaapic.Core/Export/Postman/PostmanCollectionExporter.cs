using System.Globalization;
using System.Text;
using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
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

        List<(string, DataNode)> info =
        [
            ("_postman_id", DataNodeJson.Str(root.Id.ToString("D", CultureInfo.InvariantCulture))),
            ("name", DataNodeJson.Str(root.Name)),
        ];
        if (root.Folder!.Description.Length > 0) info.Add(("description", DataNodeJson.Str(root.Folder.Description)));
        info.Add(("schema", DataNodeJson.Str(Schema)));

        List<(string, DataNode)> top = [("info", DataNodeJson.Obj([.. info])), ("item", Items(root.Children))];
        if (root.Folder.Variables.Count > 0) top.Add(("variable", Variables(root.Folder.Variables)));
        var tree = DataNodeJson.Obj([.. top]);
        if (fileRequests > 0) report.Info("", Strings.ExportFileReference(fileRequests));

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

        DataNode Items(IReadOnlyList<CollectionNode> nodes) => DataNodeJson.Arr([.. nodes.Select(Item)]);

        DataNode Item(CollectionNode node)
        {
            if (node.Kind == CollectionNodeKind.Folder)
            {
                List<(string, DataNode)> folder = [("name", DataNodeJson.Str(node.Name))];
                if (node.Folder!.Description.Length > 0) folder.Add(("description", DataNodeJson.Str(node.Folder.Description)));
                folder.Add(("item", Items(node.Children)));
                return DataNodeJson.Obj([.. folder]);
            }

            var request = node.Request!;
            if (PostmanRequestWriter.ReferencesFile(request)) fileRequests++;
            return DataNodeJson.Obj(
                ("name", DataNodeJson.Str(node.Name)),
                ("request", PostmanRequestWriter.Write(request)),
                ("response", DataNodeJson.Arr()));
        }
    }
}
