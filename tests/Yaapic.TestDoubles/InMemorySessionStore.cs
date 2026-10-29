using Yaapic.Core.Storage;
using Yaapic.Domain.Workspace;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="ISessionStore"/> kept in memory.</summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private SessionState _state = SessionState.Empty;

    /// <summary>Gets the number of <see cref="SaveAsync"/> calls.</summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc/>
    public Task<SessionState> LoadAsync(CancellationToken ct) => Task.FromResult(_state);

    /// <inheritdoc/>
    public Task SaveAsync(SessionState state, CancellationToken ct)
    {
        _state = state;
        SaveCount++;
        return Task.CompletedTask;
    }
}
