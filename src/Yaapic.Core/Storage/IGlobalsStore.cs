using Yaapic.Domain.Http;

namespace Yaapic.Core.Storage;

/// <summary>Persists the workspace-wide global variables.</summary>
public interface IGlobalsStore
{
    /// <summary>Loads the global variables.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The variables in order; empty when none were saved.</returns>
    /// <exception cref="StorageException">The store cannot be read.</exception>
    Task<IReadOnlyList<KeyValueEntry>> LoadAsync(CancellationToken ct);

    /// <summary>Replaces the global variables.</summary>
    /// <param name="variables">The variables to save, in order.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task SaveAsync(IReadOnlyList<KeyValueEntry> variables, CancellationToken ct);
}
