using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Yaapic.Core.LoadTesting;
using Yaapic.Core.Runner;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;
using Yaapic.ViewModels.Responses;
using Yaapic.ViewModels.Runner;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>One figure of a summary row (Runner, load test).</summary>
/// <param name="Label">The caption.</param>
/// <param name="Value">The figure text.</param>
public sealed record SummaryTile(string Label, string Value);

/// <summary>Converters of the Runner results pane.</summary>
public static class RunnerMap
{
    /// <summary>Builds the summary tiles from sent, errors, failed, skipped, tests text, average text and duration text.</summary>
    public static FuncMultiValueConverter<object?, IReadOnlyList<SummaryTile>> Tiles { get; } = new(values =>
        values.ToArray() is [int sent, int errors, int failed, int skipped, string tests, string average, string duration]
            ?
            [
                new(Strings.RunnerTileSent, sent.ToString(CultureInfo.CurrentCulture)),
                new(Strings.RunnerTileErrors, errors.ToString(CultureInfo.CurrentCulture)),
                new(Strings.RunnerFilterFailed, failed.ToString(CultureInfo.CurrentCulture)),
                new(Strings.RunnerFilterSkipped, skipped.ToString(CultureInfo.CurrentCulture)),
                new(Strings.RunnerTileTests, tests),
                new(Strings.LoadAverage, average),
                new(Strings.RunnerTileDuration, duration),
            ]
            : []);

    /// <summary>Builds the load test tiles from requests, throughput, average, p95, error rate and active users.</summary>
    public static FuncMultiValueConverter<object?, IReadOnlyList<SummaryTile>> LoadTiles { get; } = new(values =>
        values.ToArray() is [long requests, string throughput, string average, string p95, string errorRate, int users]
            ?
            [
                new(Strings.LoadRequests, requests.ToString(CultureInfo.CurrentCulture)),
                new(Strings.LoadThroughput, throughput),
                new(Strings.LoadAverage, average),
                new(Strings.LoadP95, p95),
                new(Strings.LoadErrorRate, errorRate),
                new(Strings.LoadActiveUsers, users.ToString(CultureInfo.CurrentCulture)),
            ]
            : []);

    /// <summary>Converts a load profile to the index of its option in the profile box.</summary>
    public static FuncValueConverter<LoadProfile, int> ProfileIndex { get; } = new(p => p == LoadProfile.RampUp ? 1 : 0);

    /// <summary>Wraps the tests of the selected row in display view models.</summary>
    public static FuncValueConverter<IReadOnlyList<TestResult>?, IReadOnlyList<TestResultItemViewModel>> Tests { get; } =
        new(tests => tests is null ? [] : [.. tests.Select(static t => new TestResultItemViewModel(t))]);

    /// <summary>Converts a filter to the index of its option in the filter control.</summary>
    public static FuncValueConverter<RunnerFilter, int> FilterIndex { get; } = new(f => (int)f);

    /// <summary>Converts how a request ended to the brush of its badge.</summary>
    public static FuncValueConverter<RunRequestOutcome, IBrush?> OutcomeBrush { get; } = new(o => BrushMap.Get(o switch
    {
        RunRequestOutcome.Passed => "YaapicSuccessBrush",
        RunRequestOutcome.Failed or RunRequestOutcome.Error => "YaapicDangerBrush",
        _ => "YaapicTextSecondaryBrush",
    }));
}
