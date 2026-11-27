using Yaapic.Core.Storage;
using Yaapic.Domain.Monitors;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IMonitorRunStore"/> kept in memory.</summary>
public sealed class InMemoryMonitorRunStore : IMonitorRunStore
{
    private readonly Dictionary<Guid, List<MonitorRun>> _runs = [];

    /// <inheritdoc/>
    public Task AppendAsync(MonitorRun run, CancellationToken ct)
    {
        if (!_runs.TryGetValue(run.MonitorId, out List<MonitorRun>? list))
        {
            _runs[run.MonitorId] = list = [];
        }

        list.Add(run);
        if (list.Count > IMonitorRunStore.MaxRunsPerMonitor)
        {
            list.RemoveRange(0, list.Count - IMonitorRunStore.MaxRunsPerMonitor);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MonitorRun>> ListAsync(Guid monitorId, int max, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<MonitorRun>>(
            _runs.TryGetValue(monitorId, out List<MonitorRun>? list) ? [.. Enumerable.Reverse(list).Take(max)] : []);

    /// <inheritdoc/>
    public Task<MonitorRun?> LatestAsync(Guid monitorId, CancellationToken ct) =>
        Task.FromResult(_runs.TryGetValue(monitorId, out List<MonitorRun>? list) ? list.LastOrDefault() : null);

    /// <inheritdoc/>
    public Task DeleteAsync(Guid monitorId, CancellationToken ct)
    {
        _runs.Remove(monitorId);
        return Task.CompletedTask;
    }
}
