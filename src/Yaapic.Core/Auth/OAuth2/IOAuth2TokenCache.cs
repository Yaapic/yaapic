namespace Yaapic.Core.Auth.OAuth2;

/// <summary>A store of OAuth 2.0 tokens by configuration key.</summary>
public interface IOAuth2TokenCache
{
    /// <summary>Raised when <see cref="Set"/>, <see cref="Remove"/> or <see cref="Clear"/> changed something.</summary>
    event EventHandler? Changed;

    /// <summary>Looks a token up; an expired token is returned too, the caller decides about refreshing.</summary>
    /// <param name="key">The key.</param>
    /// <param name="token">The cached token when found.</param>
    /// <returns><see langword="true"/> when a token is cached.</returns>
    bool TryGet(OAuth2TokenKey key, out OAuth2Token token);

    /// <summary>Stores or replaces a token.</summary>
    /// <param name="key">The key.</param>
    /// <param name="token">The token.</param>
    void Set(OAuth2TokenKey key, OAuth2Token token);

    /// <summary>Removes a token.</summary>
    /// <param name="key">The key.</param>
    void Remove(OAuth2TokenKey key);

    /// <summary>Removes every token.</summary>
    void Clear();
}
