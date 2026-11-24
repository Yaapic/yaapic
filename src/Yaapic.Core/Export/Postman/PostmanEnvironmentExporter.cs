using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Yaapic.Core.Import;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Export.Postman;

/// <summary>Writes environments as Postman environment files and the globals as a Postman globals file.</summary>
public sealed class PostmanEnvironmentExporter : IExporter
{
    private static readonly Guid GlobalsId = new(SHA256.HashData(Encoding.UTF8.GetBytes("yaapic-globals")).AsSpan(0, 16));

    /// <inheritdoc />
    public string Id => "postman-environment";

    /// <inheritdoc />
    public string DisplayName => Strings.ExportPostmanEnvironmentDisplayName;

    /// <inheritdoc />
    public string FileExtension => ".json";

    /// <inheritdoc />
    public bool RequiresCollection => false;

    /// <inheritdoc />
    public ExportResult Export(ExportSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Environments.Count == 0 && source.Globals.Count == 0)
        {
            throw new ExportException(Strings.ExportNothingToExport);
        }

        HashSet<string> used = [];
        List<ExportFile> files = [];
        foreach (var environment in source.Environments)
        {
            var path = ExportPackager.UniquePath(ExportPackager.SafeFileName(environment.Name) + ".postman_environment.json", used);
            files.Add(Write(path, environment.Id, environment.Name, environment.Variables, "environment"));
        }

        if (source.Globals.Count > 0)
        {
            files.Add(Write("globals.postman_globals.json", GlobalsId, "Globals", source.Globals, "globals"));
        }

        return new ExportResult(files, new ImportReport());
    }

    private static ExportFile Write(string path, Guid id, string name, IReadOnlyList<KeyValueEntry> rows, string scope)
    {
        var values = DataNodeJson.Arr([.. rows.Select(v => DataNodeJson.Obj(
            ("key", DataNodeJson.Str(v.Key)),
            ("value", DataNodeJson.Str(v.Value)),
            ("type", DataNodeJson.Str(v.IsSecret ? "secret" : "default")),
            ("enabled", DataNode.Bool(v.Enabled))))]);
        var tree = DataNodeJson.Obj(
            ("id", DataNodeJson.Str(id.ToString("D", CultureInfo.InvariantCulture))),
            ("name", DataNodeJson.Str(name)),
            ("values", values),
            ("_postman_variable_scope", DataNodeJson.Str(scope)));
        return new ExportFile(path, new UTF8Encoding(false).GetBytes(DataNodeJson.Write(tree, indented: true) + "\n"));
    }
}
