using Yaapic.Domain.Monitors;

namespace Yaapic.Core.Storage;

/// <summary>Persists the run history of monitors.</summary>
public interface IMonitorRunStore
{
    /// <summary>The number of runs kept per monitor.</summary>
    public const int MaxRunsPerMonitor = 200;

    /// <summary>Appends a run, then keeps at most <see cref="MaxRunsPerMonitor"/> runs of that monitor.</summary>
    /// <param name="run">The run to append.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task AppendAsync(MonitorRun run, CancellationToken ct);

    /// <summary>Lists the runs of a monitor.</summary>
    /// <param name="monitorId">The monitor identifier.</param>
    /// <param name="max">The maximum number of runs to return.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The runs, newest first.</returns>
    /// <exception cref="StorageException">The store cannot be read.</exception>
    Task<IReadOnlyList<MonitorRun>> ListAsync(Guid monitorId, int max, CancellationToken ct);

    /// <summary>Gets the newest run of a monitor.</summary>
    /// <param name="monitorId">The monitor identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The run, or <see langword="null"/> when there is none.</returns>
    /// <exception cref="StorageException">The store cannot be read.</exception>
    Task<MonitorRun?> LatestAsync(Guid monitorId, CancellationToken ct);

    /// <summary>Deletes the history of a monitor; an unknown identifier is a no-op.</summary>
    /// <param name="monitorId">The monitor identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task DeleteAsync(Guid monitorId, CancellationToken ct);
}
