using Yaapic.Core.Storage;
using Yaapic.Domain.Environments;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IEnvironmentStore"/> kept in memory.</summary>
public sealed class InMemoryEnvironmentStore : IEnvironmentStore
{
    private readonly Dictionary<Guid, EnvironmentDefinition> _items = [];

    /// <summary>Gets or sets a message that makes <see cref="SaveAsync"/> throw a <see cref="StorageException"/>; <see langword="null"/> for none.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>Gets the number of <see cref="SaveAsync"/> calls.</summary>
    public int SaveCount { get; private set; }

    /// <summary>Sets <see cref="SaveCount"/> back to zero.</summary>
    public void ResetSaveCount() => SaveCount = 0;

    /// <inheritdoc/>
    public Task<IReadOnlyList<EnvironmentDefinition>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EnvironmentDefinition>>(
            [.. _items.Values.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id)]);

    /// <inheritdoc/>
    public Task SaveAsync(EnvironmentDefinition environment, CancellationToken ct)
    {
        if (FailureMessage is not null)
        {
            throw new StorageException(FailureMessage);
        }

        _items[environment.Id] = environment;
        SaveCount++;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(Guid id, CancellationToken ct)
    {
        _items.Remove(id);
        return Task.CompletedTask;
    }
}
