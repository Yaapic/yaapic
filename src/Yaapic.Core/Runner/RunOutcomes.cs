using Yaapic.Core.Scripting;

namespace Yaapic.Core.Runner;

/// <summary>Maps a scripted send to a run outcome.</summary>
public static class RunOutcomes
{
    /// <summary>Classifies a scripted send.</summary>
    /// <param name="report">The send report.</param>
    /// <returns><see cref="RunRequestOutcome.Skipped"/> when skipped, <see cref="RunRequestOutcome.Error"/> when aborted or the send failed, <see cref="RunRequestOutcome.Failed"/> when a test failed, else <see cref="RunRequestOutcome.Passed"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is null.</exception>
    public static RunRequestOutcome From(ScriptedSendReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Skipped)
        {
            return RunRequestOutcome.Skipped;
        }

        if (report.AbortedBy is not null || report.Report is null || !report.Report.Result.IsSuccess)
        {
            return RunRequestOutcome.Error;
        }

        return report.Tests.Any(static t => !t.Passed) ? RunRequestOutcome.Failed : RunRequestOutcome.Passed;
    }
}
