using System.Collections.ObjectModel;
using System.Globalization;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;
using Yaapic.ViewModels.Import;
using Yaapic.ViewModels.LoadTesting;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Responses;
using Yaapic.ViewModels.Runner;
using Yaapic.ViewModels.Tools;
using Yaapic.ViewModels.Vault;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Adapts view-model collections for <c>ItemsSource</c>; used through <c>x:Bind</c> function binding.</summary>
public static class ListMap
{
    /// <summary>Copies entries into an observable collection.</summary>
    /// <param name="entries">The entries to show.</param>
    /// <returns>
    /// An <see cref="ObservableCollection{T}"/>: under Native AOT, CsWinRT marshals it as a bindable vector, whereas a
    /// generic <c>IReadOnlyList&lt;T&gt;</c> or <c>List&lt;T&gt;</c> of a non-WinRT type kills the process with a
    /// stowed XAML exception once the list is not empty.
    /// </returns>
    public static ObservableCollection<KeyValueEntry> Entries(IReadOnlyList<KeyValueEntry> entries) => new(entries);
    /// <summary>Copies test results into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The test rows to show.</param>
    /// <returns>An observable collection of the rows.</returns>
    public static ObservableCollection<TestResultItemViewModel> TestItems(IReadOnlyList<TestResultItemViewModel> items) => new(items);

    /// <summary>Copies script errors into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The error blocks to show.</param>
    /// <returns>An observable collection of the errors.</returns>
    public static ObservableCollection<ScriptErrorItemViewModel> ScriptErrors(IReadOnlyList<ScriptErrorItemViewModel> items) => new(items);

    /// <summary>Copies timing rows into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The waterfall rows to show.</param>
    /// <returns>An observable collection of the rows.</returns>
    public static ObservableCollection<TimingRow> Timings(IReadOnlyList<TimingRow> items) => new(items);

    /// <summary>Copies diff body rows into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The rows to show.</param>
    /// <returns>An observable collection of the rows.</returns>
    public static ObservableCollection<DiffItem> DiffLines(IReadOnlyList<DiffItem> items) => new(items);

    /// <summary>Copies header diff rows into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The rows to show.</param>
    /// <returns>An observable collection of the rows.</returns>
    public static ObservableCollection<HeaderDiffItem> DiffHeaders(IReadOnlyList<HeaderDiffItem> items) => new(items);

    /// <summary>Copies vault protector choices into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The protectors to offer.</param>
    /// <returns>An observable collection of the choices.</returns>
    public static ObservableCollection<VaultProtectorChoice> Protectors(IReadOnlyList<VaultProtectorChoice> items) => new(items);

    /// <summary>Copies code generation targets into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The targets to offer.</param>
    /// <returns>An observable collection of the targets.</returns>
    public static ObservableCollection<CodeTarget> Targets(IReadOnlyList<CodeTarget> items) => new(items);

    /// <summary>Copies JWT claim rows into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The rows to show.</param>
    /// <returns>An observable collection of the rows.</returns>
    public static ObservableCollection<JwtClaimRow> Claims(IReadOnlyList<JwtClaimRow> items) => new(items);

    /// <summary>Copies text lines into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The lines to show, or <see langword="null"/> when nothing is selected.</param>
    /// <returns>An observable collection of the lines.</returns>
    public static ObservableCollection<string> Lines(IReadOnlyList<string>? items) => new(items ?? []);

    /// <summary>Copies import report rows into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The rows to show, or <see langword="null"/> before a view model is set.</param>
    /// <returns>An observable collection of the rows.</returns>
    public static ObservableCollection<ImportReportRow> ReportRows(IReadOnlyList<ImportReportRow>? items) => new(items ?? []);

    /// <summary>Copies import format choices into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The formats to offer.</param>
    /// <returns>An observable collection of the choices.</returns>
    public static ObservableCollection<ImportFormatChoice> ImportFormats(IReadOnlyList<ImportFormatChoice> items) => new(items);

    /// <summary>Copies exportable collections into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The collections to offer.</param>
    /// <returns>An observable collection of the choices.</returns>
    public static ObservableCollection<ExportChoice> ExportChoices(IReadOnlyList<ExportChoice> items) => new(items);

    /// <summary>Copies export formats into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The formats to offer.</param>
    /// <returns>An observable collection of the choices.</returns>
    public static ObservableCollection<ExportFormatChoice> ExportFormats(IReadOnlyList<ExportFormatChoice> items) => new(items);

    /// <summary>Copies export environment choices into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The environments to offer; the items are observable, so their check boxes bind two-way.</param>
    /// <returns>An observable collection of the choices.</returns>
    public static ObservableCollection<ExportEnvironmentChoice> ExportEnvironmentChoices(IReadOnlyList<ExportEnvironmentChoice> items) => new(items);

    /// <summary>Passes the runner request list through: it is already an observable collection and must stay live while requests move.</summary>
    /// <param name="items">The setup request list.</param>
    /// <returns>The same collection.</returns>
    public static ObservableCollection<RunnerItemViewModel> RunnerItems(ObservableCollection<RunnerItemViewModel> items) => items;

    /// <summary>Passes the visible Runner result rows through: the collection is observable and grows during a run.</summary>
    /// <param name="rows">The rows that match the filter.</param>
    /// <returns>The same collection.</returns>
    public static ObservableCollection<RunnerResultRowViewModel> RunnerRows(ObservableCollection<RunnerResultRowViewModel> rows) => rows;

    /// <summary>Wraps the tests of the selected Runner row in display view models (see <see cref="Entries"/> for why a copy).</summary>
    /// <param name="tests">The test results of the selected row.</param>
    /// <returns>An observable collection of the rows.</returns>
    public static ObservableCollection<TestResultItemViewModel> RunnerTests(IReadOnlyList<TestResult> tests) =>
        [.. tests.Select(static t => new TestResultItemViewModel(t))];

    /// <summary>Builds the Runner summary tiles (see <see cref="Entries"/> for why an observable collection).</summary>
    /// <param name="sent">Requests that got a response.</param>
    /// <param name="errors">Requests that ended in error.</param>
    /// <param name="failed">Requests with a failed test.</param>
    /// <param name="skipped">Skipped requests.</param>
    /// <param name="tests">The "passed/total" tests text.</param>
    /// <param name="average">The average response time text.</param>
    /// <param name="duration">The total duration text.</param>
    /// <returns>One tile per figure, in display order.</returns>
    public static ObservableCollection<SummaryTile> RunnerTiles(int sent, int errors, int failed, int skipped, string tests, string average, string duration) =>
    [
        new(Strings.RunnerTileSent, sent.ToString(CultureInfo.CurrentCulture)),
        new(Strings.RunnerTileErrors, errors.ToString(CultureInfo.CurrentCulture)),
        new(Strings.RunnerFilterFailed, failed.ToString(CultureInfo.CurrentCulture)),
        new(Strings.RunnerFilterSkipped, skipped.ToString(CultureInfo.CurrentCulture)),
        new(Strings.RunnerTileTests, tests),
        new(Strings.LoadAverage, average),
        new(Strings.RunnerTileDuration, duration),
    ];

    /// <summary>Builds the load test tiles (see <see cref="Entries"/> for why an observable collection).</summary>
    /// <param name="requests">The completed request count.</param>
    /// <param name="throughput">The throughput text.</param>
    /// <param name="average">The average response time text.</param>
    /// <param name="p95">The 95th percentile text.</param>
    /// <param name="errorRate">The error rate text.</param>
    /// <param name="users">The active virtual user count.</param>
    /// <returns>One tile per figure, in display order.</returns>
    public static ObservableCollection<SummaryTile> LoadTiles(long requests, string throughput, string average, string p95, string errorRate, int users) =>
    [
        new(Strings.LoadRequests, requests.ToString(CultureInfo.CurrentCulture)),
        new(Strings.LoadThroughput, throughput),
        new(Strings.LoadAverage, average),
        new(Strings.LoadP95, p95),
        new(Strings.LoadErrorRate, errorRate),
        new(Strings.LoadActiveUsers, users.ToString(CultureInfo.CurrentCulture)),
    ];

    /// <summary>Copies the per-request load test rows into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="rows">The result rows.</param>
    /// <returns>An observable collection of the rows.</returns>
    public static ObservableCollection<LoadRequestRow> LoadRows(IReadOnlyList<LoadRequestRow> rows) => new(rows);

    /// <summary>Copies the data preview rows into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="rows">The preview rows, each a list of cell texts.</param>
    /// <returns>An observable collection of the rows.</returns>
    public static ObservableCollection<DataPreviewRow> DataRows(IReadOnlyList<IReadOnlyList<string>> rows) =>
        [.. rows.Select(static r => new DataPreviewRow(r))];
}

/// <summary>One row of the data file preview.</summary>
/// <param name="Cells">The cell texts, in column order.</param>
public sealed record DataPreviewRow(IReadOnlyList<string> Cells);

/// <summary>One figure of the Runner summary.</summary>
/// <param name="Label">The caption.</param>
/// <param name="Value">The figure text.</param>
public sealed record SummaryTile(string Label, string Value);
