using Yaapic.Core.Import;

namespace Yaapic.ViewModels.Import;

/// <summary>One displayed line of an import report.</summary>
/// <param name="Severity">How serious the entry is.</param>
/// <param name="SeverityLabel">The localized label of <paramref name="Severity"/>.</param>
/// <param name="Path">Where in the source the entry applies (may be empty).</param>
/// <param name="Message">The message.</param>
public sealed record ImportReportRow(ImportSeverity Severity, string SeverityLabel, string Path, string Message);
