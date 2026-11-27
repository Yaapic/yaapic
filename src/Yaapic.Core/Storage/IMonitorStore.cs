using Yaapic.Domain.Monitors;

namespace Yaapic.Core.Storage;

/// <summary>Persists monitors.</summary>
public interface IMonitorStore
{
    /// <summary>Lists every saved monitor.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The monitors sorted by name (ordinal, ignoring case), then by identifier.</returns>
    /// <exception cref="StorageException">The store cannot be read.</exception>
    Task<IReadOnlyList<MonitorDefinition>> ListAsync(CancellationToken ct);

    /// <summary>Inserts the monitor, or replaces the one with the same identifier.</summary>
    /// <param name="monitor">The monitor to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task SaveAsync(MonitorDefinition monitor, CancellationToken ct);

    /// <summary>Deletes a monitor; an unknown identifier is a no-op.</summary>
    /// <param name="id">The monitor identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
