using Yaapic.Core.Storage;
using Yaapic.Domain.Monitors;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IMonitorStore"/> kept in memory.</summary>
public sealed class InMemoryMonitorStore : IMonitorStore
{
    private readonly Dictionary<Guid, MonitorDefinition> _items = [];

    /// <summary>Gets or sets a message that makes <see cref="SaveAsync"/> throw a <see cref="StorageException"/>; <see langword="null"/> for none.</summary>
    public string? FailureMessage { get; set; }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MonitorDefinition>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<MonitorDefinition>>(
            [.. _items.Values.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Id)]);

    /// <inheritdoc/>
    public Task SaveAsync(MonitorDefinition monitor, CancellationToken ct)
    {
        if (FailureMessage is not null)
        {
            throw new StorageException(FailureMessage);
        }

        _items[monitor.Id] = monitor;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(Guid id, CancellationToken ct)
    {
        _items.Remove(id);
        return Task.CompletedTask;
    }
}
