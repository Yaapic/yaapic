using System.Globalization;
using Yaapic.Core.Responses;
using Yaapic.Core.Runner;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Runner;

/// <summary>One request result of a run, as a row of the results list.</summary>
public sealed class RunnerResultRowViewModel
{
    /// <summary>Initializes a new instance of the <see cref="RunnerResultRowViewModel"/> class.</summary>
    /// <param name="iteration">The zero-based iteration index.</param>
    /// <param name="result">The request result.</param>
    /// <param name="showIteration">Whether <see cref="IterationText"/> carries the iteration number (a run of several iterations).</param>
    public RunnerResultRowViewModel(int iteration, RunRequestResult result, bool showIteration = false)
    {
        Iteration = iteration + 1;
        Result = result;
        IterationText = showIteration ? string.Create(CultureInfo.InvariantCulture, $"#{Iteration}") : "";
    }

    /// <summary>Gets the one-based iteration number.</summary>
    public int Iteration { get; }

    /// <summary>Gets "#n" when the run has several iterations, else an empty text.</summary>
    public string IterationText { get; }

    /// <summary>Gets the request result.</summary>
    public RunRequestResult Result { get; }

    /// <summary>Gets the request name.</summary>
    public string Name => Result.Name;

    /// <summary>Gets the folder path of the request.</summary>
    public string Path => Result.Path;

    /// <summary>Gets the HTTP method text.</summary>
    public string Method => Result.Method.ToText();

    /// <summary>Gets how the request ended.</summary>
    public RunRequestOutcome Outcome => Result.Outcome;

    /// <summary>Gets the outcome text.</summary>
    public string OutcomeText => Outcome switch
    {
        RunRequestOutcome.Passed => Strings.RunnerOutcomePassed,
        RunRequestOutcome.Failed => Strings.RunnerOutcomeFailed,
        RunRequestOutcome.Error => Strings.RunnerOutcomeError,
        _ => Strings.RunnerOutcomeSkipped,
    };

    /// <summary>Gets the status code, or "-" when there was no response.</summary>
    public string StatusText => Result.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "-";

    /// <summary>Gets the response time text.</summary>
    public string DurationText => ResponseText.FormatDuration(Result.Duration);

    /// <summary>Gets the response size text.</summary>
    public string SizeText => ResponseText.FormatSize(Result.SizeBytes);

    /// <summary>Gets the "passed/total" tests text, or empty without tests.</summary>
    public string TestsText => TestSummary.Format(Result.Tests);

    internal bool Matches(RunnerFilter filter) => filter switch
    {
        RunnerFilter.Passed => Outcome == RunRequestOutcome.Passed,
        RunnerFilter.Failed => Outcome is RunRequestOutcome.Failed or RunRequestOutcome.Error,
        RunnerFilter.Skipped => Outcome == RunRequestOutcome.Skipped,
        _ => true,
    };
}
