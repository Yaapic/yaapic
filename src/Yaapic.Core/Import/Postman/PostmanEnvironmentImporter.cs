using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Postman;

/// <summary>Reads a Postman environment file into an environment, or a Postman globals file into the global variables.</summary>
public sealed class PostmanEnvironmentImporter : IImporter
{
    private const int SniffLength = 4096;

    /// <inheritdoc />
    public string Id => "postman-environment";

    /// <inheritdoc />
    public string DisplayName => Strings.ImportPostmanEnvironmentDisplayName;

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
        if (head.Contains("_postman_variable_scope", StringComparison.Ordinal))
        {
            return ImportConfidence.Certain;
        }

        return head.Contains("\"values\"", StringComparison.Ordinal) && head.Contains("\"name\"", StringComparison.Ordinal)
            && !head.Contains("\"item\"", StringComparison.Ordinal)
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

        if (root["values"] is not { IsArray: true } values)
        {
            throw new ImportException(Strings.ImportPostmanNotAnEnvironment);
        }

        List<KeyValueEntry> rows = [];
        foreach (var item in values.Items)
        {
            if (item.GetString("key") is { Length: > 0 } key)
            {
                rows.Add(new KeyValueEntry(key, item.GetString("value") ?? "", item.GetBool("enabled") != false,
                    item.GetString("type") == "secret"));
            }
        }

        if (root.GetString("_postman_variable_scope") == "globals")
        {
            return new ImportBundle(null, [], [], rows, context.Report);
        }

        var environment = new EnvironmentDefinition(context.NewId(), ImportHelpers.SafeName(root.GetString("name"), input.Name), rows, null);
        return new ImportBundle(null, [], [environment], [], context.Report);
    }
}
