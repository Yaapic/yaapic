using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Auth;

/// <summary>Applies one kind of advanced authentication to a resolved request.</summary>
public interface IAuthMethod
{
    /// <summary>Gets the authentication kind this method handles.</summary>
    AuthKind Kind { get; }

    /// <summary>Signs or decorates the request.</summary>
    /// <param name="request">The resolved request.</param>
    /// <param name="auth">The resolved authentication settings.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The authenticated request, or a failure with a user-facing message.</returns>
    Task<AuthenticationResult> ApplyAsync(ResolvedRequest request, ResolvedAuth auth, CancellationToken ct);
}
