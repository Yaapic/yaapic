using Yaapic.Domain.Collections;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Insomnia;

/// <summary>Reads an Insomnia v4 JSON export (<c>__export_format</c> 3 or 4) into folders and requests.</summary>
public sealed class InsomniaImporter : IImporter
{
    private const int SniffLength = 8192;

    /// <inheritdoc />
    public string Id => "insomnia";

    /// <inheritdoc />
    public string DisplayName => Strings.ImportInsomniaDisplayName;

    /// <inheritdoc />
    public ImportConfidence Sniff(ImportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Files.Count == 0)
        {
            return ImportConfidence.None;
        }

        var text = input.Files[0].Text;
        var head = text.Length > SniffLength ? text[..SniffLength] : text;
        if (head.Contains("\"__export_format\"", StringComparison.Ordinal))
        {
            return ImportConfidence.Certain;
        }

        return head.Contains("\"_type\"", StringComparison.Ordinal) && head.Contains("\"resources\"", StringComparison.Ordinal)
            ? ImportConfidence.Possible
            : ImportConfidence.None;
    }

    /// <inheritdoc />
    public ImportBundle Import(ImportInput input, ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        DataNode root;
        try
        {
            root = DataNodeJson.Parse(input.Single.Text);
        }
        catch (FormatException ex)
        {
            throw new ImportException(ex.Message, ex);
        }

        if (root["resources"] is not { IsArray: true } resources)
        {
            throw new ImportException(Strings.ImportInsomniaNotAnExport);
        }

        if (root.GetString("__export_format") is { } format && format is not ("3" or "4"))
        {
            throw new ImportException(Strings.ImportInsomniaVersion(format));
        }

        var workspaces = resources.Items.Where(r => r.GetString("_type") == "workspace").ToList();
        var workspace = workspaces.Count > 0 ? workspaces[0] : null;
        if (workspaces.Count > 1)
        {
            context.Report.Skipped("", Strings.ImportInsomniaOtherWorkspaces(workspaces.Count - 1));
        }

        var name = ImportHelpers.SafeName(workspace?.GetString("name"), input.Name);
        var walk = new InsomniaRequests(context, resources.Items, workspace?.GetString("_id"));
        var children = walk.ReadRoot(name);
        walk.Finish();

        var folder = new FolderDefinition(context.NewId(), name, workspace?.GetString("description") ?? "", [], ScriptSet.Empty);
        return new ImportBundle(new ImportFolder(folder, children), [], [], [], context.Report);
    }
}
