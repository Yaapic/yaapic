namespace Yaapic.Core.Import;

/// <summary>One line of the import report.</summary>
/// <param name="Severity">How serious the entry is.</param>
/// <param name="Path">Where in the source the entry applies, joined with <see cref="ImportPath.Join"/>.</param>
/// <param name="Message">The human-readable message.</param>
public sealed record ImportEntry(ImportSeverity Severity, string Path, string Message);
