using Yaapic.Domain.Environments;

namespace Yaapic.Core.Storage;

/// <summary>Persists environments.</summary>
public interface IEnvironmentStore
{
    /// <summary>Lists every saved environment.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The environments sorted by name (ordinal, ignoring case), then by identifier.</returns>
    /// <exception cref="StorageException">The store cannot be read.</exception>
    Task<IReadOnlyList<EnvironmentDefinition>> ListAsync(CancellationToken ct);

    /// <summary>Inserts the environment, or replaces the one with the same identifier.</summary>
    /// <param name="environment">The environment to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task SaveAsync(EnvironmentDefinition environment, CancellationToken ct);

    /// <summary>Deletes an environment; an unknown identifier is a no-op.</summary>
    /// <param name="id">The environment identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
