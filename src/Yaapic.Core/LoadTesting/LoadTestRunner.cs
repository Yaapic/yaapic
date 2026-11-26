using Yaapic.Core.Runner;
using Yaapic.Core.Scripting;

namespace Yaapic.Core.LoadTesting;

/// <summary>Runs the virtual users of a <see cref="LoadTestPlan"/> with the runner step executor and feeds <see cref="LoadMetrics"/> (ADR 0019).</summary>
/// <param name="step">The step executor shared with the Runner.</param>
/// <param name="scopes">The variable scope source the step executor reads; load tests never persist variable changes.</param>
/// <param name="time">The clock; times the test, the start offsets, the think times and the snapshots.</param>
public sealed class LoadTestRunner(RunStepExecutor step, IRunScopeSource scopes, TimeProvider time)
{
    /// <summary>Gets the scope source the runner was built with.</summary>
    public IRunScopeSource Scopes { get; } = scopes;

    /// <summary>Runs the plan until its duration is over or the token is canceled.</summary>
    /// <param name="plan">The plan to run.</param>
    /// <param name="progress">Receives a snapshot every second and a final one; may be <see langword="null"/>.</param>
    /// <param name="ct">The cancellation token; cancelling it ends the test with <see cref="LoadTestResult.Canceled"/>, never throws.</param>
    /// <returns>The result, with everything measured before the end.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The plan is out of range.</exception>
    public async Task<LoadTestResult> RunAsync(LoadTestPlan plan, IProgress<LoadSnapshot>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.Validate();
        DateTimeOffset startedAt = time.GetUtcNow();
        using var session = new Session(plan, new LoadMetrics(plan.Items.Count), time.GetTimestamp(), ct, time);
        using var progressStop = new CancellationTokenSource();
        Task reporter = ReportAsync(session, progress, progressStop.Token);
        var users = new Task[plan.VirtualUsers];
        for (int n = 0; n < users.Length; n++)
        {
            int index = n;
            users[n] = Task.Run(() => RunUserAsync(session, index), CancellationToken.None);
        }

        await Task.WhenAll(users).ConfigureAwait(false);
        await progressStop.CancelAsync().ConfigureAwait(false);
        await reporter.ConfigureAwait(false);

        TimeSpan elapsed = time.GetElapsedTime(session.Start);
        LoadMetrics metrics = session.Metrics;
        LoadStats[] perRequest = [.. Enumerable.Range(0, plan.Items.Count).Select(i => metrics.ForRequest(i, elapsed))];
        return new LoadTestResult(
            plan,
            startedAt,
            elapsed,
            ct.IsCancellationRequested,
            metrics.Overall(elapsed),
            perRequest,
            metrics.Snapshot(elapsed).Buckets,
            metrics.ErrorMessages,
            metrics.SamplesTruncated);
    }

    private async Task ReportAsync(Session session, IProgress<LoadSnapshot>? progress, CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), time);
        try
        {
            while (await timer.WaitForNextTickAsync(stop).ConfigureAwait(false))
            {
                progress?.Report(session.Snapshot(time));
            }
        }
        catch (OperationCanceledException)
        {
            // The test is over; fall through to the final snapshot.
        }

        progress?.Report(session.Snapshot(time));
    }

    private async Task RunUserAsync(Session s, int userIndex)
    {
        try
        {
            TimeSpan offset = VirtualUserSchedule.StartOffset(s.Plan, userIndex);
            if (offset > TimeSpan.Zero)
            {
                await Task.Delay(offset, time, s.Wait).ConfigureAwait(false);
            }

            s.UserStarted();
            try
            {
                await LoopAsync(s).ConfigureAwait(false);
            }
            finally
            {
                s.UserStopped();
            }
        }
        catch (OperationCanceledException)
        {
            // The duration is over or the caller canceled while this user was waiting.
        }
        catch (Exception ex)
        {
            s.Metrics.AddError(ex.Message);
        }
    }

    private async Task LoopAsync(Session s)
    {
        LoadTestPlan plan = s.Plan;
        var state = new RunVariableState();
        int userIteration = 0;
        while (!s.Over)
        {
            IterationDataRow row = plan.Data.Count == 0 ? IterationDataRow.Empty : plan.Data[s.NextRowIndex(plan.Data.Count)];
            var context = new ScriptRunContext(userIteration++, 0, row.Values, InRunner: true);
            for (int i = 0; i < plan.Items.Count; i++)
            {
                if (s.Over)
                {
                    return;
                }

                RunStepResult executed = await step.ExecuteAsync(plan.Items[i], state, context, false, plan.Limits, s.Ct).ConfigureAwait(false);
                if (executed.IsCanceled || s.Ct.IsCancellationRequested)
                {
                    return;
                }

                Record(s, i, executed.Result);
                if (plan.ThinkTimeMs > 0 && !s.Over)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(plan.ThinkTimeMs), time, s.Wait).ConfigureAwait(false);
                }
            }
        }
    }

    private void Record(Session s, int index, RunRequestResult result)
    {
        if (result.Outcome == RunRequestOutcome.Skipped)
        {
            return;
        }

        int status = result.StatusCode ?? 0;
        bool isError = result.Outcome is RunRequestOutcome.Error or RunRequestOutcome.Failed || status >= 400;
        s.Metrics.Add(new LoadSample((long)time.GetElapsedTime(s.Start).TotalMilliseconds, index, (int)result.Duration.TotalMilliseconds, status, isError));
        if (isError && result.Error is { } message)
        {
            s.Metrics.AddError(message);
        }
    }

    private sealed class Session : IDisposable
    {
        private readonly CancellationTokenSource _end;
        private readonly CancellationTokenSource _wait;
        private int _running;
        private long _nextRow;

        public Session(LoadTestPlan plan, LoadMetrics metrics, long start, CancellationToken ct, TimeProvider clock)
        {
            Plan = plan;
            Metrics = metrics;
            Start = start;
            Ct = ct;
            _end = new CancellationTokenSource(plan.Duration, clock);
            _wait = CancellationTokenSource.CreateLinkedTokenSource(ct, _end.Token);
        }

        public LoadTestPlan Plan { get; }

        public LoadMetrics Metrics { get; }

        public long Start { get; }

        /// <summary>Gets the caller token; in-flight requests use it only, so they finish after the duration is over.</summary>
        public CancellationToken Ct { get; }

        /// <summary>Gets a token canceled at the end of the duration or by the caller; used for waits.</summary>
        public CancellationToken Wait => _wait.Token;

        public bool Over => _wait.IsCancellationRequested;

        public void UserStarted() => Interlocked.Increment(ref _running);

        public void UserStopped() => Interlocked.Decrement(ref _running);

        public int NextRowIndex(int count) => (int)((Interlocked.Increment(ref _nextRow) - 1) % count);

        public LoadSnapshot Snapshot(TimeProvider clock)
        {
            TimeSpan elapsed = clock.GetElapsedTime(Start);
            Metrics.SetActiveUsers((long)elapsed.TotalMilliseconds, Volatile.Read(ref _running));
            return Metrics.Snapshot(elapsed);
        }

        public void Dispose()
        {
            _wait.Dispose();
            _end.Dispose();
        }
    }
}
