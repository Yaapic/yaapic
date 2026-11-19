using Yaapic.Domain.Collections;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Har;

/// <summary>Turns the <c>log.entries</c> of a HAR file into requests in one folder.</summary>
public sealed class HarImporter : IImporter
{
    private const int SniffLength = 8192;

    /// <inheritdoc />
    public string Id => "har";

    /// <inheritdoc />
    public string DisplayName => Strings.ImportHarDisplayName;

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
        if (head.Contains("\"entries\"", StringComparison.Ordinal))
        {
            if (head.Contains("\"log\"", StringComparison.Ordinal))
            {
                return ImportConfidence.Certain;
            }

            if (head.Contains("\"request\"", StringComparison.Ordinal))
            {
                return ImportConfidence.Possible;
            }
        }

        return ImportConfidence.None;
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

        if (root["log"]?["entries"] is not { IsArray: true } entries)
        {
            throw new ImportException(Strings.ImportHarNotAHar);
        }

        List<ImportItem> children = [];
        var withResponse = 0;
        for (var i = 0; i < entries.Items.Count; i++)
        {
            var entry = entries.Items[i];
            if (entry["response"] is { IsObject: true })
            {
                withResponse++;
            }

            if (HarEntries.Convert(entry, i, context) is { } request)
            {
                children.Add(new ImportRequest(request));
            }
        }

        if (withResponse > 0)
        {
            context.Report.Info(string.Empty, Strings.ImportHarResponsesIgnored(withResponse));
        }

        var folder = new FolderDefinition(context.NewId(), ImportHelpers.SafeName(input.Name, "HAR import"), "", [], ScriptSet.Empty);
        return new ImportBundle(new ImportFolder(folder, children), [], [], [], context.Report);
    }
}
