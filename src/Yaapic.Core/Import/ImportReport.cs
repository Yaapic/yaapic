namespace Yaapic.Core.Import;

/// <summary>Collects what an import converted with a loss, approximated or skipped.</summary>
public sealed class ImportReport
{
    private readonly List<ImportEntry> _entries = [];

    /// <summary>Gets the entries, in the order they were added.</summary>
    public IReadOnlyList<ImportEntry> Entries => _entries;

    /// <summary>Gets a value indicating whether any entry is a <see cref="ImportSeverity.Warning"/> or <see cref="ImportSeverity.Skipped"/>.</summary>
    public bool HasProblems => _entries.Exists(e => e.Severity != ImportSeverity.Info);

    /// <summary>Adds an entry.</summary>
    /// <param name="severity">How serious the entry is.</param>
    /// <param name="path">Where in the source the entry applies.</param>
    /// <param name="message">The message.</param>
    public void Add(ImportSeverity severity, string path, string message) => _entries.Add(new(severity, path, message));

    /// <summary>Adds an <see cref="ImportSeverity.Info"/> entry.</summary>
    /// <param name="path">Where in the source the entry applies.</param>
    /// <param name="message">The message.</param>
    public void Info(string path, string message) => Add(ImportSeverity.Info, path, message);

    /// <summary>Adds a <see cref="ImportSeverity.Warning"/> entry.</summary>
    /// <param name="path">Where in the source the entry applies.</param>
    /// <param name="message">The message.</param>
    public void Warning(string path, string message) => Add(ImportSeverity.Warning, path, message);

    /// <summary>Adds a <see cref="ImportSeverity.Skipped"/> entry.</summary>
    /// <param name="path">Where in the source the entry applies.</param>
    /// <param name="message">The message.</param>
    public void Skipped(string path, string message) => Add(ImportSeverity.Skipped, path, message);

    /// <summary>Adds an entry unless one with the same severity and message already exists (for "once per distinct name" warnings).</summary>
    /// <param name="severity">How serious the entry is.</param>
    /// <param name="path">Where in the source the entry applies.</param>
    /// <param name="message">The message.</param>
    /// <returns><see langword="true"/> when the entry was added.</returns>
    public bool AddOnce(ImportSeverity severity, string path, string message)
    {
        if (_entries.Exists(e => e.Severity == severity && e.Message == message))
        {
            return false;
        }

        Add(severity, path, message);
        return true;
    }

    /// <summary>Adds several entries.</summary>
    /// <param name="entries">The entries to append.</param>
    public void AddRange(IEnumerable<ImportEntry> entries) => _entries.AddRange(entries);

    /// <summary>Counts the entries of a severity.</summary>
    /// <param name="severity">The severity to count.</param>
    /// <returns>The number of entries with that severity.</returns>
    public int Count(ImportSeverity severity) => _entries.Count(e => e.Severity == severity);
}
