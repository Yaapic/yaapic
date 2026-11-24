using Yaapic.Localization;

namespace Yaapic.Core.Import.Bruno;

/// <summary>Reads a Bruno collection folder (<c>bruno.json</c>, <c>folder.bru</c>, <c>*.bru</c>) into folders and requests.</summary>
public sealed class BrunoImporter : IImporter
{
    /// <inheritdoc />
    public string Id => "bruno";

    /// <inheritdoc />
    public string DisplayName => Strings.ImportBrunoDisplayName;

    /// <inheritdoc />
    public ImportConfidence Sniff(ImportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Files.Any(f => BrunoCollection.IsManifest(f.Path)))
        {
            return ImportConfidence.Certain;
        }

        return input.Files.Count(f => BrunoCollection.IsBru(f.Path)) switch
        {
            0 => ImportConfidence.None,
            1 => ImportConfidence.Possible,
            _ => ImportConfidence.Certain,
        };
    }

    /// <inheritdoc />
    public ImportBundle Import(ImportInput input, ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);
        return new BrunoCollection(input, context).Read();
    }
}
