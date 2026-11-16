using Yaapic.Core.Http;

namespace Yaapic.Core.Auth;

/// <summary>Applies the pending authentication of a resolved request.</summary>
public interface IRequestAuthenticator
{
    /// <summary>Authenticates the request.</summary>
    /// <param name="request">The resolved request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The request unchanged when it has no pending authentication; otherwise the authenticated request or a failure.</returns>
    Task<AuthenticationResult> AuthenticateAsync(ResolvedRequest request, CancellationToken ct);
}
