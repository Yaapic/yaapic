using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Auth.OAuth2;

/// <summary>The default <see cref="IOAuth2TokenService"/> for the non-interactive grants.</summary>
/// <param name="transport">Sends the token endpoint requests.</param>
/// <param name="cache">Holds the tokens.</param>
/// <param name="time">The clock.</param>
public sealed class OAuth2TokenService(IHttpTransport transport, IOAuth2TokenCache cache, TimeProvider time) : IOAuth2TokenService
{
    private readonly Lock _gate = new();
    private readonly Dictionary<OAuth2TokenKey, Task<OAuth2TokenResult>> _inFlight = [];

    /// <inheritdoc/>
    public Task<OAuth2TokenResult> GetTokenAsync(OAuth2Settings resolved, CancellationToken ct) =>
        Shared(resolved, ct, GetCoreAsync);

    /// <inheritdoc/>
    public Task<OAuth2TokenResult> GetNewTokenAsync(OAuth2Settings resolved, CancellationToken ct) =>
        Shared(resolved, ct, FetchAsync);

    /// <inheritdoc/>
    public OAuth2TokenStatus GetStatus(OAuth2Settings resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        if (!cache.TryGet(OAuth2TokenKey.For(resolved), out var token))
        {
            return new(OAuth2TokenState.NoToken);
        }

        return token.IsExpired(time.GetUtcNow(), OAuth2Token.DefaultLeeway)
            ? new(OAuth2TokenState.Expired)
            : new(OAuth2TokenState.Valid, token.ExpiresAt);
    }

    /// <inheritdoc/>
    public void Clear(OAuth2Settings resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        cache.Remove(OAuth2TokenKey.For(resolved));
    }

    // The work runs once per key without the caller's token (a cancelled first caller must not fail the others);
    // each caller stops waiting on its own token. The transport has its own timeout.
    private Task<OAuth2TokenResult> Shared(
        OAuth2Settings s, CancellationToken ct, Func<OAuth2Settings, OAuth2TokenKey, Task<OAuth2TokenResult>> work)
    {
        ArgumentNullException.ThrowIfNull(s);
        var key = OAuth2TokenKey.For(s);
        Task<OAuth2TokenResult> task;
        lock (_gate)
        {
            if (!_inFlight.TryGetValue(key, out task!))
            {
                task = Task.Run(() => RunAsync(s, key, work), CancellationToken.None);
                _inFlight[key] = task;
            }
        }

        return task.WaitAsync(ct);
    }

    private async Task<OAuth2TokenResult> RunAsync(
        OAuth2Settings s, OAuth2TokenKey key, Func<OAuth2Settings, OAuth2TokenKey, Task<OAuth2TokenResult>> work)
    {
        try
        {
            return await work(s, key).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _inFlight.Remove(key);
            }
        }
    }

    private async Task<OAuth2TokenResult> GetCoreAsync(OAuth2Settings s, OAuth2TokenKey key)
    {
        if (Validate(s) is { } invalid)
        {
            return OAuth2TokenResult.Fail(invalid);
        }

        if (cache.TryGet(key, out var cached))
        {
            if (!cached.IsExpired(time.GetUtcNow(), OAuth2Token.DefaultLeeway))
            {
                return OAuth2TokenResult.Ok(cached);
            }

            if (cached.RefreshToken is { Length: > 0 } refresh)
            {
                var refreshed = await SendAsync(OAuth2TokenRequests.Refresh(s, refresh), cached).ConfigureAwait(false);
                if (refreshed.Token is { } t)
                {
                    cache.Set(key, t);
                    return refreshed;
                }
            }

            cache.Remove(key);
        }

        return s.Grant == OAuth2Grant.AuthorizationCode
            ? OAuth2TokenResult.Fail(Strings.Oauth2GetTokenFirst)
            : await FetchAsync(s, key).ConfigureAwait(false);
    }

    private async Task<OAuth2TokenResult> FetchAsync(OAuth2Settings s, OAuth2TokenKey key)
    {
        if (Validate(s) is { } invalid)
        {
            return OAuth2TokenResult.Fail(invalid);
        }

        if (s.Grant == OAuth2Grant.AuthorizationCode)
        {
            return OAuth2TokenResult.Fail(Strings.Oauth2NotAvailable); // the interactive flow arrives with M4-23
        }

        var request = s.Grant == OAuth2Grant.Password ? OAuth2TokenRequests.Password(s) : OAuth2TokenRequests.ClientCredentials(s);
        var result = await SendAsync(request, null).ConfigureAwait(false);
        if (result.Token is { } token)
        {
            cache.Set(key, token);
        }

        return result;
    }

    // A refresh answer without a refresh_token keeps the previous one.
    private async Task<OAuth2TokenResult> SendAsync(ResolvedRequest request, OAuth2Token? previous)
    {
        var sent = await transport.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
        if (sent.Response is not { } response)
        {
            return OAuth2TokenResult.Fail(sent.Failure!.Message);
        }

        var parsed = OAuth2TokenResponses.Parse(response, time.GetUtcNow());
        if (parsed.Token is not { } token)
        {
            return OAuth2TokenResult.Fail(parsed.Error!);
        }

        return OAuth2TokenResult.Ok(token.RefreshToken is null && previous?.RefreshToken is { } old ? token with { RefreshToken = old } : token);
    }

    private static string? Validate(OAuth2Settings s)
    {
        if (!Uri.TryCreate(s.TokenUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            return Strings.Oauth2TokenUrlMissing;
        }

        return s switch
        {
            { Grant: OAuth2Grant.ClientCredentials, ClientId.Length: 0 } => Strings.Oauth2ClientIdMissing,
            { Grant: OAuth2Grant.Password, Username.Length: 0 } => Strings.Oauth2UsernameMissing,
            _ => null,
        };
    }
}
