using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Responses;

/// <summary>The tests and script errors of the last send, for the response "Tests" tab.</summary>
public sealed partial class TestResultsViewModel : ObservableObject
{
    private IReadOnlyList<TestResultItemViewModel> _all = [];

    [ObservableProperty]
    private TestFilter _filter = TestFilter.All;

    /// <summary>Gets the tests that match <see cref="Filter"/>.</summary>
    public IReadOnlyList<TestResultItemViewModel> Items { get; private set; } = [];

    /// <summary>Gets the script runs that did not succeed.</summary>
    public IReadOnlyList<ScriptErrorItemViewModel> Errors { get; private set; } = [];

    /// <summary>Gets the number of passed tests.</summary>
    public int PassedCount { get; private set; }

    /// <summary>Gets the number of failed tests.</summary>
    public int FailedCount { get; private set; }

    /// <summary>Gets the number of tests.</summary>
    public int TotalCount => PassedCount + FailedCount;

    /// <summary>Gets a value indicating whether any test ran.</summary>
    public bool HasTests => TotalCount > 0;

    /// <summary>Gets a value indicating whether any script run did not succeed.</summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>Gets a value indicating whether there are tests and all passed.</summary>
    public bool AllPassed => HasTests && FailedCount == 0;

    /// <summary>Gets the summary, such as <c>6/6</c>; empty without tests.</summary>
    public string Summary { get; private set; } = "";

    /// <summary>Gets the summary sentence, such as <c>4 of 6 passed</c>; empty without tests.</summary>
    public string SummaryText => HasTests ? Strings.TestsSummary(PassedCount, TotalCount) : "";

    /// <summary>Gets the tab badge: the summary, or <c>!</c> when there are script errors and no tests.</summary>
    public string BadgeText => HasTests ? Summary : HasErrors ? "!" : "";

    /// <summary>Replaces the content with the tests and errors of the given runs.</summary>
    /// <param name="runs">The script runs of a send, in order.</param>
    public void Show(IReadOnlyList<ScriptRunResult> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var tests = runs.SelectMany(r => r.Tests).ToList();
        (PassedCount, FailedCount) = TestSummary.Count(tests);
        Summary = TestSummary.Format(tests);
        _all = [.. tests.Select(t => new TestResultItemViewModel(t))];
        Errors = [.. runs.Where(r => !r.Succeeded).Select(r => new ScriptErrorItemViewModel(r))];
        ApplyFilter();
        OnPropertyChanged(string.Empty);
    }

    /// <summary>Removes every test and error.</summary>
    public void Clear() => Show([]);

    partial void OnFilterChanged(TestFilter value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(Items));
    }

    private void ApplyFilter() =>
        Items = Filter == TestFilter.Failed ? [.. _all.Where(t => !t.Passed)] : _all;
}
