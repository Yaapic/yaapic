using System.Globalization;
using System.Text;
using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Export.Insomnia;

/// <summary>Writes a collection as an Insomnia v4 JSON export.</summary>
public sealed class InsomniaExporter : IExporter
{
    /// <inheritdoc />
    public string Id => "insomnia";

    /// <inheritdoc />
    public string DisplayName => Strings.ExportInsomniaDisplayName;

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
        var requests = new InsomniaRequestWriter(report);
        var workspaceId = "wrk_" + Hex(root.Id);
        var baseId = "env_" + Hex(root.Id);

        List<DataNode> resources =
        [
            DataNodeJson.Obj(
                ("_id", DataNodeJson.Str(workspaceId)),
                ("_type", DataNodeJson.Str("workspace")),
                ("parentId", DataNode.Null),
                ("name", DataNodeJson.Str(root.Name)),
                ("description", DataNodeJson.Str(root.Folder!.Description)),
                ("scope", DataNodeJson.Str("collection"))),
            Environment(baseId, workspaceId, "Base Environment", root.Folder.Variables, 0, root.Name, report),
        ];
        for (var i = 0; i < source.Environments.Count; i++)
        {
            var e = source.Environments[i];
            resources.Add(Environment("env_" + Hex(e.Id), baseId, e.Name, e.Variables, i + 1, e.Name, report));
        }

        var scripts = Count(root.Folder.Scripts);
        Walk(root.Children, workspaceId, root.Name);
        if (scripts > 0) report.Warning("", Strings.ExportInsomniaScripts(scripts));

        var tree = DataNodeJson.Obj(
            ("_type", DataNodeJson.Str("export")),
            ("__export_format", DataNodeJson.Num(4)),
            ("__export_source", DataNodeJson.Str("yaapic")),
            ("resources", DataNodeJson.Arr([.. resources])));
        var text = DataNodeJson.Write(tree, indented: true) + "\n";
        var name = ExportPackager.SafeFileName(source.Name) + ".insomnia.json";
        return new ExportResult([new ExportFile(name, new UTF8Encoding(false).GetBytes(text))], report);

        void Walk(IReadOnlyList<CollectionNode> nodes, string parentId, string path)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var itemPath = ImportPath.Join(path, node.Name);
                if (node.Kind == CollectionNodeKind.Folder)
                {
                    var id = "fld_" + Hex(node.Id);
                    scripts += Count(node.Folder!.Scripts);
                    resources.Add(DataNodeJson.Obj(
                        ("_id", DataNodeJson.Str(id)),
                        ("_type", DataNodeJson.Str("request_group")),
                        ("parentId", DataNodeJson.Str(parentId)),
                        ("name", DataNodeJson.Str(node.Name)),
                        ("description", DataNodeJson.Str(node.Folder.Description)),
                        ("metaSortKey", DataNodeJson.Num(i))));
                    Walk(node.Children, id, itemPath);
                }
                else
                {
                    scripts += Count(node.Request!.Scripts);
                    resources.Add(requests.Write("req_" + Hex(node.Id), parentId, node.Request, i, itemPath));
                }
            }
        }
    }

    private static int Count(ScriptSet s) => (s.PreRequest.IsEmpty ? 0 : 1) + (s.PostResponse.IsEmpty ? 0 : 1);

    private static string Hex(Guid id) => id.ToString("N", CultureInfo.InvariantCulture);

    private static DataNode Environment(string id, string parentId, string name, IReadOnlyList<KeyValueEntry> rows, int position,
        string path, ImportReport report) =>
        DataNodeJson.Obj(
            ("_id", DataNodeJson.Str(id)),
            ("_type", DataNodeJson.Str("environment")),
            ("parentId", DataNodeJson.Str(parentId)),
            ("name", DataNodeJson.Str(name)),
            ("data", DataNodeJson.Obj([.. rows.Where(r => r.Enabled)
                .Select(r => (r.Key, DataNodeJson.Str(InsomniaTemplateWriter.Translate(r.Value, path, report))))])),
            ("metaSortKey", DataNodeJson.Num(position)));
}
