using Yaapic.Core.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Runner;

/// <summary>Runs a <see cref="RunPlan"/> iteration after iteration, honouring <c>setNextRequest</c> and <c>skipRequest</c> (ADR 0018).</summary>
/// <param name="step">The step executor.</param>
/// <param name="time">The clock; times the run and the delays.</param>
public sealed class CollectionRunner(RunStepExecutor step, TimeProvider time)
{
    private const int MaxStepsPerIteration = 1000;

    /// <summary>Runs every item of every iteration in order.</summary>
    /// <param name="plan">The plan to run.</param>
    /// <param name="progress">Receives a notification after each step; may be <see langword="null"/>.</param>
    /// <param name="ct">The cancellation token; cancelling it ends the run as <see cref="RunStatus.Canceled"/>, never throws.</param>
    /// <returns>The result, with everything that ran before the run ended.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The plan options are out of range.</exception>
    public async Task<RunResult> RunAsync(RunPlan plan, IProgress<RunProgress>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.Options.Validate();
        DateTimeOffset startedAt = time.GetUtcNow();
        long started = time.GetTimestamp();
        List<RunIterationResult> iterations = [];
        (RunStatus status, string? error) = (RunStatus.Completed, null);
        if (plan.Items.Count > 0)
        {
            (status, error) = await RunIterationsAsync(plan, progress, iterations, ct).ConfigureAwait(false);
        }

        return new RunResult(plan.Name, startedAt, time.GetElapsedTime(started), status, error, plan.Options, plan.Data.Count, iterations);
    }

    private async Task<(RunStatus Status, string? Error)> RunIterationsAsync(RunPlan plan, IProgress<RunProgress>? progress, List<RunIterationResult> iterations, CancellationToken ct)
    {
        RunOptions options = plan.Options;
        var state = new RunVariableState();
        int total = options.Iterations * plan.Items.Count;
        int completed = 0;
        bool first = true;
        for (int i = 0; i < options.Iterations; i++)
        {
            var context = new ScriptRunContext(i, options.Iterations, plan.RowFor(i).Values, InRunner: true);
            List<RunRequestResult> results = [];
            iterations.Add(new RunIterationResult(i, results));
            int index = 0;
            int steps = 0;
            while (index < plan.Items.Count)
            {
                if (++steps > MaxStepsPerIteration)
                {
                    return (RunStatus.Failed, Strings.RunnerLoopGuard);
                }

                RunItem item = plan.Items[index];
                if (!first && options.DelayMs > 0 && !await DelayAsync(options.DelayMs, ct).ConfigureAwait(false))
                {
                    return (RunStatus.Canceled, null);
                }

                first = false;
                RunStepResult executed = await step.ExecuteAsync(item, state, context, options.PersistVariables, options.Limits, ct).ConfigureAwait(false);
                if (executed.IsCanceled || ct.IsCancellationRequested)
                {
                    return (RunStatus.Canceled, null);
                }

                results.Add(executed.Result);
                completed++;
                progress?.Report(new RunProgress(i, options.Iterations, Math.Min(completed, total), total, executed.Result, executed.Report));
                if (options.StopOnFailure && executed.Result.Outcome is RunRequestOutcome.Failed or RunRequestOutcome.Error)
                {
                    return (RunStatus.StoppedOnFailure, null);
                }

                if (!executed.Report.NextRequestSet)
                {
                    index++;
                }
                else if (executed.Report.NextRequest is not { } target)
                {
                    return (RunStatus.Stopped, null);
                }
                else if ((index = FindTarget(plan.Items, target)) < 0)
                {
                    return (RunStatus.Failed, Strings.RunnerNextRequestNotFound(target));
                }
            }
        }

        return (RunStatus.Completed, null);
    }

    private static int FindTarget(IReadOnlyList<RunItem> items, string target)
    {
        for (int n = 0; n < items.Count; n++)
        {
            if (string.Equals(items[n].Request.Id.ToString("D"), target, StringComparison.OrdinalIgnoreCase))
            {
                return n;
            }
        }

        for (int n = 0; n < items.Count; n++)
        {
            if (string.Equals(items[n].Request.Name, target, StringComparison.Ordinal))
            {
                return n;
            }
        }

        return -1;
    }

    private async Task<bool> DelayAsync(int ms, CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(ms), time, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
