using Yaapic.Domain.Http;

namespace Yaapic.Core.Storage;

/// <summary>Persists saved requests.</summary>
public interface IRequestStore
{
    /// <summary>Lists every saved request.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The requests sorted by name (ordinal, ignoring case), then by identifier.</returns>
    /// <exception cref="StorageException">The store cannot be read.</exception>
    Task<IReadOnlyList<HttpRequestDefinition>> ListAsync(CancellationToken ct);

    /// <summary>Inserts the request, or replaces the one with the same identifier.</summary>
    /// <param name="request">The request to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task SaveAsync(HttpRequestDefinition request, CancellationToken ct);

    /// <summary>Deletes a request; an unknown identifier is a no-op.</summary>
    /// <param name="id">The request identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
