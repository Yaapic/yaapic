using Yaapic.Domain.History;

namespace Yaapic.Core.Storage;

/// <summary>Persists the request history, newest entries first.</summary>
public interface IHistoryStore
{
    /// <summary>The maximum number of entries kept; implementations drop the oldest beyond it.</summary>
    public const int MaxEntries = 500;

    /// <summary>Adds an entry, then drops the oldest ones beyond <see cref="MaxEntries"/>.</summary>
    /// <param name="entry">The entry to add.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task AppendAsync(HistoryEntry entry, CancellationToken ct);

    /// <summary>Lists every kept entry.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The entries, newest first by time, then by identifier descending for equal times.</returns>
    /// <exception cref="StorageException">The store cannot be read.</exception>
    Task<IReadOnlyList<HistoryEntry>> ListAsync(CancellationToken ct);

    /// <summary>Deletes one entry; an unknown identifier is a no-op.</summary>
    /// <param name="id">The identifier of the entry.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>Deletes every entry.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task ClearAsync(CancellationToken ct);
}
