namespace Yaapic.Core.Auth.OAuth2;

/// <summary>A thread-safe, in-memory <see cref="IOAuth2TokenCache"/> that never persists anything.</summary>
public sealed class InMemoryOAuth2TokenCache : IOAuth2TokenCache
{
    readonly Lock _gate = new();
    readonly Dictionary<OAuth2TokenKey, OAuth2Token> _tokens = [];

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool TryGet(OAuth2TokenKey key, out OAuth2Token token)
    {
        lock (_gate)
        {
            return _tokens.TryGetValue(key, out token!);
        }
    }

    /// <inheritdoc />
    public void Set(OAuth2TokenKey key, OAuth2Token token)
    {
        ArgumentNullException.ThrowIfNull(token);
        lock (_gate)
        {
            _tokens[key] = token;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Remove(OAuth2TokenKey key)
    {
        bool removed;
        lock (_gate)
        {
            removed = _tokens.Remove(key);
        }

        if (removed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        bool had;
        lock (_gate)
        {
            had = _tokens.Count > 0;
            _tokens.Clear();
        }

        if (had)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
