namespace Yaapic.Core.Import;

/// <summary>The known importers, in registration order, and the detection of the format of an input.</summary>
/// <param name="importers">The registered importers.</param>
public sealed class ImporterRegistry(IEnumerable<IImporter> importers)
{
    /// <summary>Gets the importers in registration order.</summary>
    public IReadOnlyList<IImporter> Importers { get; } = [.. importers];

    /// <summary>Finds an importer by identifier.</summary>
    /// <param name="id">The importer identifier.</param>
    /// <returns>The importer, or <see langword="null"/> when none has this identifier.</returns>
    public IImporter? Find(string id) => Importers.FirstOrDefault(i => i.Id == id);

    /// <summary>Lists the importers that may read the input, the most confident first.</summary>
    /// <param name="input">The input to inspect.</param>
    /// <returns>The matching importers; a <see cref="IImporter.Sniff"/> that throws counts as no match.</returns>
    public IReadOnlyList<IImporter> Detect(ImportInput input) =>
        [.. Importers
            .Select(i => (Importer: i, Confidence: SafeSniff(i, input)))
            .Where(x => x.Confidence != ImportConfidence.None)
            .OrderByDescending(x => x.Confidence)
            .Select(x => x.Importer)];

    private static ImportConfidence SafeSniff(IImporter importer, ImportInput input)
    {
        try
        {
            return importer.Sniff(input);
        }
        catch (Exception)
        {
            return ImportConfidence.None;
        }
    }
}
