using Yaapic.Core.Storage;
using Yaapic.Domain.Http;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IRequestStore"/> kept in memory.</summary>
public sealed class InMemoryRequestStore : IRequestStore
{
    private readonly Dictionary<Guid, HttpRequestDefinition> _items = [];

    /// <summary>Gets the number of <see cref="SaveAsync"/> calls.</summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc/>
    public Task<IReadOnlyList<HttpRequestDefinition>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HttpRequestDefinition>>(
            [.. _items.Values.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Id)]);

    /// <inheritdoc/>
    public Task SaveAsync(HttpRequestDefinition request, CancellationToken ct)
    {
        _items[request.Id] = request;
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
