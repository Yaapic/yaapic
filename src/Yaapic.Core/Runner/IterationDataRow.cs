using Yaapic.Domain.Http;

namespace Yaapic.Core.Runner;

/// <summary>One row of iteration data: the named values a run exposes to a single iteration.</summary>
/// <param name="Values">The column values, in file order.</param>
public sealed record IterationDataRow(IReadOnlyList<KeyValueEntry> Values)
{
    /// <summary>A row with no value, used when an iteration has no data.</summary>
    public static IterationDataRow Empty { get; } = new([]);

    /// <summary>Finds a value by column name.</summary>
    /// <param name="name">The column name, compared ordinally.</param>
    /// <param name="value">The first matching value when the result is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a column has that name.</returns>
    public bool TryGet(string name, out string value)
    {
        foreach (KeyValueEntry entry in Values)
        {
            if (string.Equals(entry.Key, name, StringComparison.Ordinal))
            {
                value = entry.Value;
                return true;
            }
        }

        value = "";
        return false;
    }
}
