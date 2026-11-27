using Yaapic.Core.Runner;
using Yaapic.Core.Scripting;
using Yaapic.Core.Storage;
using Yaapic.Core.Variables;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Monitors;
using Yaapic.Localization;

namespace Yaapic.Core.Monitors;

/// <summary>Runs the due monitors one at a time while the app is open and records each run (ADR 0021).</summary>
/// <param name="monitors">The monitor store.</param>
/// <param name="runs">The run history store.</param>
/// <param name="collections">The collection store; holds the monitored folders.</param>
/// <param name="environments">The environment store; holds the monitor environments.</param>
/// <param name="scopes">The scope source monitors read the collection and global variables from.</param>
/// <param name="step">The step executor; monitors reuse its sender and script chains.</param>
/// <param name="time">The clock.</param>
public sealed class MonitorScheduler(
    IMonitorStore monitors,
    IMonitorRunStore runs,
    ICollectionStore collections,
    IEnvironmentStore environments,
    IRunScopeSource scopes,
    RunStepExecutor step,
    TimeProvider time) : IAsyncDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _gate = new();
    private readonly List<Guid> _queue = [];
    private Task? _loop;
    private Task _worker = Task.CompletedTask;
    private bool _workerActive;
    private Guid? _running;

    /// <summary>Raised when <see cref="Running"/> or <see cref="Queued"/> changed, from a background thread.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Raised with the recorded run after each run, from a background thread.</summary>
    public event EventHandler<MonitorRun>? RunCompleted;

    /// <summary>Gets the monitor being run, or <see langword="null"/>.</summary>
    public Guid? Running
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    /// <summary>Gets the monitors waiting for their turn, in run order.</summary>
    public IReadOnlyList<Guid> Queued
    {
        get
        {
            lock (_gate)
            {
                return [.. _queue];
            }
        }
    }

    /// <summary>Computes when a monitor runs next.</summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="latest">Its newest run, or <see langword="null"/> when it never ran.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The next run time, or <see langword="null"/> when the monitor is disabled.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="monitor"/> is null.</exception>
    public static DateTimeOffset? NextRun(MonitorDefinition monitor, MonitorRun? latest, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return !monitor.Enabled ? null : latest is null ? now : latest.At + TimeSpan.FromMinutes(monitor.IntervalMinutes);
    }

    /// <summary>Starts the background loop: a check at once, then every 30 seconds; does nothing when already started.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_loop is not null || _cts.IsCancellationRequested)
            {
                return;
            }

            // Created here, not in the loop, so a test clock sees the timer before it advances.
            var timer = new PeriodicTimer(TickInterval, time);
            _loop = Task.Run(() => LoopAsync(timer));
        }
    }

    /// <summary>Queues a monitor to run as soon as possible, even when it is disabled; a monitor already queued or running is not queued twice.</summary>
    /// <param name="monitorId">The monitor identifier.</param>
    public void RunNow(Guid monitorId) => Enqueue([monitorId]);

    /// <summary>Stops the loop, cancels the current run and waits for both to end.</summary>
    /// <returns>A task that completes when everything stopped.</returns>
    public async Task StopAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        Task? loop;
        Task worker;
        lock (_gate)
        {
            loop = _loop;
            worker = _worker;
        }

        if (loop is not null)
        {
            await loop.ConfigureAwait(false);
        }

        await worker.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _cts.Dispose();
    }

    private async Task LoopAsync(PeriodicTimer timer)
    {
        using (timer)
        {
            CancellationToken ct = _cts.Token;
            try
            {
                do
                {
                    await TickAsync(ct).ConfigureAwait(false);
                }
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                // Stopped: the loop ends.
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        try
        {
            DateTimeOffset now = time.GetUtcNow();
            List<(Guid Id, DateTimeOffset Due)> due = [];
            foreach (MonitorDefinition monitor in await monitors.ListAsync(ct).ConfigureAwait(false))
            {
                if (!monitor.Enabled)
                {
                    continue;
                }

                MonitorRun? latest = await runs.LatestAsync(monitor.Id, ct).ConfigureAwait(false);
                if (latest is null)
                {
                    due.Add((monitor.Id, DateTimeOffset.MinValue));
                }
                else if (NextRun(monitor, latest, now) is { } next && now >= next)
                {
                    due.Add((monitor.Id, next));
                }
            }

            Enqueue([.. due.OrderBy(static d => d.Due).Select(static d => d.Id)]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A store failure skips this tick; the next one retries.
        }
    }

    private void Enqueue(IReadOnlyList<Guid> ids)
    {
        bool changed = false;
        lock (_gate)
        {
            if (_cts.IsCancellationRequested)
            {
                return;
            }

            foreach (Guid id in ids)
            {
                if (_running != id && !_queue.Contains(id))
                {
                    _queue.Add(id);
                    changed = true;
                }
            }

            if (_queue.Count > 0 && !_workerActive)
            {
                _workerActive = true;
                _worker = Task.Run(WorkAsync);
            }
        }

        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task WorkAsync()
    {
        CancellationToken ct = _cts.Token;
        while (true)
        {
            Guid id;
            lock (_gate)
            {
                if (_queue.Count == 0 || ct.IsCancellationRequested)
                {
                    _queue.Clear();
                    _running = null;
                    _workerActive = false;
                    break;
                }

                id = _queue[0];
                _queue.RemoveAt(0);
                _running = id;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
            await RunOneAsync(id, ct).ConfigureAwait(false);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunOneAsync(Guid id, CancellationToken ct)
    {
        try
        {
            MonitorRun? run = await BuildRunAsync(id, ct).ConfigureAwait(false);
            if (run is not null && !ct.IsCancellationRequested)
            {
                await RecordAsync(run, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped: a canceled run is not recorded.
        }
        catch (Exception ex)
        {
            await TryRecordErrorAsync(id, ex.Message, ct).ConfigureAwait(false);
        }
    }

    private async Task<MonitorRun?> BuildRunAsync(Guid id, CancellationToken ct)
    {
        MonitorDefinition? monitor = (await monitors.ListAsync(ct).ConfigureAwait(false)).FirstOrDefault(m => m.Id == id);
        if (monitor is null)
        {
            return null;
        }

        CollectionNode? folder = CollectionNode.Flatten(await collections.LoadTreeAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(n => n.Id == monitor.FolderId && n.Kind == CollectionNodeKind.Folder);
        if (folder is null)
        {
            return MonitorRuns.Error(id, time.GetUtcNow(), Strings.MonitorFolderMissing);
        }

        EnvironmentDefinition? environment = null;
        if (monitor.EnvironmentId is { } environmentId)
        {
            environment = (await environments.ListAsync(ct).ConfigureAwait(false)).FirstOrDefault(e => e.Id == environmentId);
            if (environment is null)
            {
                return MonitorRuns.Error(id, time.GetUtcNow(), Strings.MonitorEnvironmentMissing);
            }
        }

        var plan = new RunPlan(monitor.Name, RunPlanBuilder.ListRequests(folder), new RunOptions(1, 0, false, PersistVariables: false), []);
        var runner = new CollectionRunner(step.WithScopes(new MonitorScopeSource(scopes, environment)), time);
        RunResult result = await runner.RunAsync(plan, null, ct).ConfigureAwait(false);
        return MonitorRuns.From(id, result);
    }

    private async Task RecordAsync(MonitorRun run, CancellationToken ct)
    {
        await runs.AppendAsync(run, ct).ConfigureAwait(false);
        RunCompleted?.Invoke(this, run);
    }

    private async Task TryRecordErrorAsync(Guid id, string message, CancellationToken ct)
    {
        try
        {
            await RecordAsync(MonitorRuns.Error(id, time.GetUtcNow(), message), ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Even the history is unavailable: nothing more can be done, the loop goes on.
        }
    }

    private sealed class MonitorScopeSource(IRunScopeSource inner, EnvironmentDefinition? environment) : IRunScopeSource
    {
        public VariableScopes GetScopes(Guid requestId) => inner.GetScopes(requestId) with { Environment = environment };

        public IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId) => [];

        public Task<IReadOnlyList<string>> PersistAsync(Guid requestId, IReadOnlyList<VariableChange> changes, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
