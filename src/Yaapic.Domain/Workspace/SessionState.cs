namespace Yaapic.Domain.Workspace;

/// <summary>The restorable UI session: open tabs and the active request and environment.</summary>
/// <param name="OpenRequestIds">The identifiers of the open requests, in tab order.</param>
/// <param name="ActiveRequestId">The active request, or <see langword="null"/> when none.</param>
/// <param name="ActiveEnvironmentId">The active environment, or <see langword="null"/> when none.</param>
/// <param name="Layout">How the request and response panels are laid out.</param>
public sealed record SessionState(
    IReadOnlyList<Guid> OpenRequestIds,
    Guid? ActiveRequestId,
    Guid? ActiveEnvironmentId,
    PaneLayout Layout = PaneLayout.Vertical)
{
    /// <summary>A session with no open request and no active environment.</summary>
    public static SessionState Empty { get; } = new([], null, null);
}
