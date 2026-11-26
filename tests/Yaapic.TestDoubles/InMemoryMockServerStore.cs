using Yaapic.Core.Storage;
using Yaapic.Domain.Mocks;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IMockServerStore"/> kept in memory.</summary>
public sealed class InMemoryMockServerStore : IMockServerStore
{
    private readonly Dictionary<Guid, MockServerDefinition> _items = [];

    /// <summary>Gets or sets a message that makes <see cref="SaveAsync"/> throw a <see cref="StorageException"/>; <see langword="null"/> for none.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>Gets the number of <see cref="SaveAsync"/> calls.</summary>
    public int SaveCount { get; private set; }

    /// <summary>Sets <see cref="SaveCount"/> back to zero.</summary>
    public void ResetSaveCount() => SaveCount = 0;

    /// <inheritdoc/>
    public Task<IReadOnlyList<MockServerDefinition>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<MockServerDefinition>>(
            [.. _items.Values.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Id)]);

    /// <inheritdoc/>
    public Task SaveAsync(MockServerDefinition server, CancellationToken ct)
    {
        if (FailureMessage is not null)
        {
            throw new StorageException(FailureMessage);
        }

        _items[server.Id] = server;
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
