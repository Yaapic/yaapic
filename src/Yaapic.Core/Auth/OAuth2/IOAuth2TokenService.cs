using Yaapic.Domain.Http;

namespace Yaapic.Core.Auth.OAuth2;

/// <summary>The outcome of asking for a token.</summary>
/// <param name="Token">The token, or null on failure.</param>
/// <param name="Error">The error message, or null on success.</param>
public sealed record OAuth2TokenResult(OAuth2Token? Token, string? Error)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The result.</returns>
    public static OAuth2TokenResult Ok(OAuth2Token token) => new(token, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">The error message.</param>
    /// <returns>The result.</returns>
    public static OAuth2TokenResult Fail(string error) => new(null, error);
}

/// <summary>The state of the cached token of a configuration.</summary>
public enum OAuth2TokenState
{
    /// <summary>Nothing is cached.</summary>
    NoToken,

    /// <summary>A token is cached and usable.</summary>
    Valid,

    /// <summary>A token is cached but expired.</summary>
    Expired,
}

/// <summary>The cached token state with its expiry.</summary>
/// <param name="State">The state.</param>
/// <param name="ExpiresAt">The expiry instant of a valid token; null when none or when it never expires.</param>
public readonly record struct OAuth2TokenStatus(OAuth2TokenState State, DateTimeOffset? ExpiresAt = null);

/// <summary>Returns OAuth 2.0 tokens, using the cache and refreshing when possible.</summary>
public interface IOAuth2TokenService
{
    /// <summary>Raised when the cached tokens changed (set, removed or cleared).</summary>
    event EventHandler? TokensChanged;

    /// <summary>Returns a valid token without any user interaction.</summary>
    /// <param name="resolved">The settings with variables resolved.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The token or an error.</returns>
    /// <exception cref="OperationCanceledException">The caller cancelled.</exception>
    Task<OAuth2TokenResult> GetTokenAsync(OAuth2Settings resolved, CancellationToken ct);

    /// <summary>Ignores the cache and fetches a new token.</summary>
    /// <param name="resolved">The settings with variables resolved.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The token or an error.</returns>
    /// <exception cref="OperationCanceledException">The caller cancelled.</exception>
    Task<OAuth2TokenResult> GetNewTokenAsync(OAuth2Settings resolved, CancellationToken ct);

    /// <summary>Reports the cached token state.</summary>
    /// <param name="resolved">The settings with variables resolved.</param>
    /// <returns>The status.</returns>
    OAuth2TokenStatus GetStatus(OAuth2Settings resolved);

    /// <summary>Removes the cached token.</summary>
    /// <param name="resolved">The settings with variables resolved.</param>
    void Clear(OAuth2Settings resolved);

    /// <summary>Removes every cached token.</summary>
    void ClearAllTokens();
}
