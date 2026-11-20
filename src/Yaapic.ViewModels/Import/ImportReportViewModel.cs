using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Import;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;

namespace Yaapic.ViewModels.Import;

/// <summary>Presents an <see cref="ImportReport"/>: counts, a filter per severity and a copy-as-text command.</summary>
/// <param name="dialogs">The dialog service, used for the clipboard.</param>
public sealed partial class ImportReportViewModel(IDialogService dialogs) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleRows))]
    private bool _showInfo = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleRows))]
    private bool _showWarnings = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleRows))]
    private bool _showSkipped = true;

    /// <summary>Gets every row, in source order.</summary>
    public IReadOnlyList<ImportReportRow> Rows { get; private set; } = [];

    /// <summary>Gets the rows whose severity filter is on.</summary>
    public IReadOnlyList<ImportReportRow> VisibleRows =>
        [.. Rows.Where(r => r.Severity switch
        {
            ImportSeverity.Info => ShowInfo,
            ImportSeverity.Warning => ShowWarnings,
            _ => ShowSkipped,
        })];

    /// <summary>Gets the number of notes.</summary>
    public int InfoCount { get; private set; }

    /// <summary>Gets the number of warnings.</summary>
    public int WarningCount { get; private set; }

    /// <summary>Gets the number of skipped items.</summary>
    public int SkippedCount { get; private set; }

    /// <summary>Gets a value indicating whether the report has any row.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>Gets a value indicating whether the report has a warning or a skipped item.</summary>
    public bool HasProblems => WarningCount + SkippedCount > 0;

    /// <summary>Gets the one-line summary of the report.</summary>
    public string Summary => HasRows ? Strings.ImportReportSummary(WarningCount, SkippedCount, InfoCount) : Strings.ImportReportEmpty;

    /// <summary>Replaces the rows with those of a report.</summary>
    /// <param name="report">The report to show, or <see langword="null"/> to clear.</param>
    public void Load(ImportReport? report)
    {
        Rows = [.. (report?.Entries ?? []).Select(e => new ImportReportRow(e.Severity, LabelOf(e.Severity), e.Path, e.Message))];
        InfoCount = Rows.Count(r => r.Severity == ImportSeverity.Info);
        WarningCount = Rows.Count(r => r.Severity == ImportSeverity.Warning);
        SkippedCount = Rows.Count(r => r.Severity == ImportSeverity.Skipped);
        OnPropertyChanged(string.Empty);
        CopyCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Formats the summary and every row (the filters do not apply) as plain text.</summary>
    /// <returns>The text copied by the copy command.</returns>
    public string AsText() =>
        Summary + "\n\n" + string.Join('\n', Rows.Select(r =>
            r.Path.Length == 0 ? $"[{r.SeverityLabel}] {r.Message}" : $"[{r.SeverityLabel}] {r.Path}: {r.Message}"));

    private static string LabelOf(ImportSeverity severity) => severity switch
    {
        ImportSeverity.Info => Strings.ImportSeverityInfo,
        ImportSeverity.Warning => Strings.ImportSeverityWarning,
        _ => Strings.ImportSeveritySkipped,
    };

    [RelayCommand(CanExecute = nameof(HasRows))]
    private Task CopyAsync() => dialogs.SetClipboardTextAsync(AsText());
}
