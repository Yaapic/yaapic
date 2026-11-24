using Yaapic.Localization;

namespace Yaapic.Core.Runner;

/// <summary>A parsed iteration data file.</summary>
/// <param name="FileName">The file name without its directory.</param>
/// <param name="Rows">The data rows.</param>
/// <param name="Columns">The column names over all rows, in first-seen order.</param>
public sealed record IterationData(string FileName, IReadOnlyList<IterationDataRow> Rows, IReadOnlyList<string> Columns);

/// <summary>Reads a CSV or JSON iteration data file from its decoded text.</summary>
public static class IterationDataReader
{
    /// <summary>Parses a data file, choosing CSV or JSON from the extension, else from the content.</summary>
    /// <param name="fileName">The file name or path.</param>
    /// <param name="text">The decoded text (a leading U+FEFF is removed).</param>
    /// <returns>The rows and the column names.</returns>
    /// <exception cref="IterationDataException">The text is too large or malformed.</exception>
    public static IterationData Read(string fileName, string text)
    {
        if (text.Length > IterationDataLimits.MaxBytes)
        {
            throw new IterationDataException(Strings.RunnerDataTooLarge);
        }

        text = text.TrimStart('\uFEFF');
        string extension = Path.GetExtension(fileName);
        bool json = extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || (!extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) && text.AsSpan().TrimStart().StartsWith("["));
        IReadOnlyList<IterationDataRow> rows = json ? JsonIterationData.Parse(text) : CsvIterationData.Parse(text);

        List<string> columns = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (IterationDataRow row in rows)
        {
            foreach (Yaapic.Domain.Http.KeyValueEntry entry in row.Values)
            {
                if (seen.Add(entry.Key))
                {
                    columns.Add(entry.Key);
                }
            }
        }

        return new IterationData(Path.GetFileName(fileName), rows, columns);
    }
}
