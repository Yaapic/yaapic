using Yaapic.Domain.Collections;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Postman;

/// <summary>Reads a Postman collection (v2.0 and v2.1, or the API export wrapper) into folders and requests.</summary>
public sealed class PostmanCollectionImporter : IImporter
{
    private const int SniffLength = 4096;

    /// <inheritdoc />
    public string Id => "postman";

    /// <inheritdoc />
    public string DisplayName => Strings.ImportPostmanDisplayName;

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
        if (head.Contains("getpostman.com/json/collection", StringComparison.Ordinal))
        {
            return ImportConfidence.Certain;
        }

        return head.Contains("\"_postman_id\"", StringComparison.Ordinal) && head.Contains("\"item\"", StringComparison.Ordinal)
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

        if (root["collection"] is { IsObject: true } wrapped && root["info"] is null)
        {
            root = wrapped;
        }

        if (root["info"] is not { IsObject: true } info || root["item"] is not { IsArray: true } items)
        {
            throw new ImportException(Strings.ImportPostmanNotACollection);
        }

        var name = ImportHelpers.SafeName(info.GetString("name"), input.Name);
        var walk = new PostmanItems(context);
        var folder = new FolderDefinition(context.NewId(), name, PostmanItems.Text(info["description"]),
            walk.ReadVariables(root["variable"], name, true), walk.ReadScripts(root["event"], name));
        var children = walk.ReadItems(items, name, 1, PostmanAuth.Map(root["auth"], name, context.Report));
        walk.Finish();
        return new ImportBundle(new ImportFolder(folder, children), [], [], [], context.Report);
    }
}
