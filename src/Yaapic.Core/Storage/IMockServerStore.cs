using Yaapic.Domain.Mocks;

namespace Yaapic.Core.Storage;

/// <summary>Persists mock servers.</summary>
public interface IMockServerStore
{
    /// <summary>Lists every saved mock server.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The servers sorted by name (ordinal, ignoring case), then by identifier.</returns>
    /// <exception cref="StorageException">The store cannot be read.</exception>
    Task<IReadOnlyList<MockServerDefinition>> ListAsync(CancellationToken ct);

    /// <summary>Inserts the server, or replaces the one with the same identifier.</summary>
    /// <param name="server">The server to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task SaveAsync(MockServerDefinition server, CancellationToken ct);

    /// <summary>Deletes a server; an unknown identifier is a no-op.</summary>
    /// <param name="id">The server identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
