using Yaapic.Core.Storage;
using Yaapic.Domain.History;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IHistoryStore"/> kept in memory.</summary>
public sealed class InMemoryHistoryStore : IHistoryStore
{
    private readonly List<HistoryEntry> _entries = [];

    /// <summary>Gets or sets a message that makes every call throw a <see cref="StorageException"/>; <see langword="null"/> for none.</summary>
    public string? FailureMessage { get; set; }

    /// <inheritdoc/>
    public Task AppendAsync(HistoryEntry entry, CancellationToken ct)
    {
        ThrowIfFailing();
        _entries.Add(entry);
        if (_entries.Count > IHistoryStore.MaxEntries)
        {
            var keep = Sorted().Take(IHistoryStore.MaxEntries).ToList();
            _entries.Clear();
            _entries.AddRange(keep);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<HistoryEntry>> ListAsync(CancellationToken ct)
    {
        ThrowIfFailing();
        return Task.FromResult<IReadOnlyList<HistoryEntry>>(Sorted().ToList());
    }

    /// <inheritdoc/>
    public Task DeleteAsync(Guid id, CancellationToken ct)
    {
        ThrowIfFailing();
        _entries.RemoveAll(e => e.Id == id);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken ct)
    {
        ThrowIfFailing();
        _entries.Clear();
        return Task.CompletedTask;
    }

    private IOrderedEnumerable<HistoryEntry> Sorted() =>
        _entries.OrderByDescending(e => e.At).ThenByDescending(e => e.Id);

    private void ThrowIfFailing()
    {
        if (FailureMessage is not null)
        {
            throw new StorageException(FailureMessage);
        }
    }
}
