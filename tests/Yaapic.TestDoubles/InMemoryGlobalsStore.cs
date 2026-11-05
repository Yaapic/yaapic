using Yaapic.Core.Storage;
using Yaapic.Domain.Http;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IGlobalsStore"/> kept in memory.</summary>
public sealed class InMemoryGlobalsStore : IGlobalsStore
{
    private IReadOnlyList<KeyValueEntry> _items = [];

    /// <summary>Gets or sets a message that makes every call throw a <see cref="StorageException"/>; <see langword="null"/> for none.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>Gets the number of <see cref="SaveAsync"/> calls.</summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc/>
    public Task<IReadOnlyList<KeyValueEntry>> LoadAsync(CancellationToken ct)
    {
        ThrowIfFailing();
        return Task.FromResult(_items);
    }

    /// <inheritdoc/>
    public Task SaveAsync(IReadOnlyList<KeyValueEntry> variables, CancellationToken ct)
    {
        ThrowIfFailing();
        _items = [.. variables];
        SaveCount++;
        return Task.CompletedTask;
    }

    private void ThrowIfFailing()
    {
        if (FailureMessage is not null)
        {
            throw new StorageException(FailureMessage);
        }
    }
}
