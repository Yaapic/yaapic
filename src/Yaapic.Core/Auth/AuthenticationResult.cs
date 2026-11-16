using Yaapic.Core.Http;

namespace Yaapic.Core.Auth;

/// <summary>The outcome of authenticating a request.</summary>
/// <param name="Request">The authenticated request, or null on failure.</param>
/// <param name="ErrorMessage">The failure message, or null on success.</param>
public sealed record AuthenticationResult(ResolvedRequest? Request, string? ErrorMessage)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="request">The authenticated request.</param>
    /// <returns>The result.</returns>
    public static AuthenticationResult Success(ResolvedRequest request) => new(request, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="message">The user-facing failure message.</param>
    /// <returns>The result.</returns>
    public static AuthenticationResult Fail(string message) => new(null, message);
}
