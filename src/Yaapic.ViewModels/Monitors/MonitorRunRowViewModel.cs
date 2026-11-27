using Yaapic.Core.Responses;
using Yaapic.Domain.Monitors;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Monitors;

/// <summary>One run of the history of a monitor.</summary>
/// <param name="Run">The run shown.</param>
public sealed record MonitorRunRowViewModel(MonitorRun Run)
{
    /// <summary>Gets the local start time, as <c>yyyy-MM-dd HH:mm</c>.</summary>
    public string TimeText => MonitorItemViewModel.TimeText(Run.At);

    /// <summary>Gets how the run ended.</summary>
    public MonitorRunStatus Status => Run.Status;

    /// <summary>Gets the localized status.</summary>
    public string StatusText => MonitorItemViewModel.StatusText(Run.Status);

    /// <summary>Gets the formatted duration.</summary>
    public string DurationText => ResponseText.FormatDuration(TimeSpan.FromMilliseconds(Run.DurationMs));

    /// <summary>Gets the failed and total request counts.</summary>
    public string RequestsText => Strings.MonitorRequestsFailed(Run.FailedRequests, Run.Requests);

    /// <summary>Gets the one-line summary of the problem, or an empty text.</summary>
    public string Message => Run.Message ?? "";

    /// <summary>Gets the failing requests, one line each: request, outcome, status and detail.</summary>
    public IReadOnlyList<string> Failures =>
        [.. Run.Failures.Select(static f => $"{f.RequestName} - {f.Outcome} - {f.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"} - {f.Detail}")];
}
