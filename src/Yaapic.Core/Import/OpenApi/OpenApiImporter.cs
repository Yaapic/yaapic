using System.Text.RegularExpressions;
using Yaapic.Domain.Collections;
using Yaapic.Localization;

namespace Yaapic.Core.Import.OpenApi;

/// <summary>Reads an OpenAPI 3.x document, JSON or YAML (Swagger 2.0 is normalized by a later step).</summary>
/// <param name="reader">The JSON or YAML reader.</param>
public sealed partial class OpenApiImporter(StructuredTextReader reader) : IImporter
{
    private const int SniffLength = 4096;

    /// <inheritdoc />
    public string Id => "openapi";

    /// <inheritdoc />
    public string DisplayName => Strings.ImportOpenapiDisplayName;

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
        if (VersionLine().IsMatch(head))
        {
            return ImportConfidence.Certain;
        }

        return head.Contains("\"paths\"", StringComparison.Ordinal) || PathsLine().IsMatch(head)
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
            root = reader.Parse(input.Single.Text);
        }
        catch (FormatException ex)
        {
            throw new ImportException(ex.Message, ex);
        }

        var version = root["openapi"]?.AsString() ?? root["swagger"]?.AsString();
        if (!root.IsObject || version is null || root["paths"] is not { IsObject: true })
        {
            throw new ImportException(Strings.ImportOpenapiNotADocument);
        }

        CheckVersion(version, root["openapi"] is null, context.Report);

        var info = root["info"];
        var name = ImportHelpers.SafeName(info?.GetString("title"), input.Name);
        var folder = new FolderDefinition(context.NewId(), name, info?.GetString("description") ?? string.Empty, [], null!);
        return new ImportBundle(new ImportFolder(folder, []), [], [], [], context.Report);
    }

    private static void CheckVersion(string version, bool swagger, ImportReport report)
    {
        if (swagger ? version == "2.0" : version.StartsWith("3.0.", StringComparison.Ordinal) || version == "3.0"
            || version.StartsWith("3.1.", StringComparison.Ordinal) || version == "3.1")
        {
            return;
        }

        if (!swagger && version.StartsWith("3.", StringComparison.Ordinal))
        {
            report.Info(string.Empty, Strings.ImportOpenapiVersionUntested(version));
            return;
        }

        throw new ImportException(Strings.ImportOpenapiVersion(version));
    }

    [GeneratedRegex("""^\s*\{?\s*["']?(openapi|swagger)["']?\s*:\s*["']?(3\.[0-9]|2\.0)""", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex VersionLine();

    [GeneratedRegex("^paths:", RegexOptions.Multiline)]
    private static partial Regex PathsLine();
}
