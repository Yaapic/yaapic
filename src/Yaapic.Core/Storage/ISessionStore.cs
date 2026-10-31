using Yaapic.Domain.Workspace;

namespace Yaapic.Core.Storage;

/// <summary>Persists the restorable UI session.</summary>
public interface ISessionStore
{
    /// <summary>Loads the session.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The saved session, or <see cref="SessionState.Empty"/> when it is missing or unreadable; never throws for those cases.</returns>
    Task<SessionState> LoadAsync(CancellationToken ct);

    /// <summary>Saves the session.</summary>
    /// <param name="state">The session to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task SaveAsync(SessionState state, CancellationToken ct);
}
