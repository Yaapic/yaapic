using Yaapic.Core.Auth.OAuth2;
using Yaapic.Domain.Http;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IOAuth2TokenService"/> that returns scripted results and records its calls.</summary>
public sealed class FakeOAuth2TokenService : IOAuth2TokenService
{
    private readonly List<OAuth2Settings> _tokenCalls = [];
    private readonly List<OAuth2Settings> _newTokenCalls = [];

    /// <summary>Gets or sets the result of <see cref="GetTokenAsync"/>.</summary>
    public OAuth2TokenResult TokenResult { get; set; } = OAuth2TokenResult.Fail("no token scripted");

    /// <summary>Gets or sets the result of <see cref="GetNewTokenAsync"/>.</summary>
    public OAuth2TokenResult NewTokenResult { get; set; } = OAuth2TokenResult.Fail("no token scripted");

    /// <summary>Gets or sets the value returned by <see cref="GetStatus"/>.</summary>
    public OAuth2TokenStatus Status { get; set; } = new(OAuth2TokenState.NoToken);

    /// <summary>Gets the settings passed to <see cref="GetTokenAsync"/>, in order.</summary>
    public IReadOnlyList<OAuth2Settings> TokenCalls => _tokenCalls;

    /// <summary>Gets the settings passed to <see cref="GetNewTokenAsync"/>, in order.</summary>
    public IReadOnlyList<OAuth2Settings> NewTokenCalls => _newTokenCalls;

    /// <summary>Gets the number of <see cref="Clear"/> calls.</summary>
    public int ClearCount { get; private set; }

    /// <inheritdoc/>
    public Task<OAuth2TokenResult> GetTokenAsync(OAuth2Settings resolved, CancellationToken ct)
    {
        _tokenCalls.Add(resolved);
        return Task.FromResult(TokenResult);
    }

    /// <inheritdoc/>
    public Task<OAuth2TokenResult> GetNewTokenAsync(OAuth2Settings resolved, CancellationToken ct)
    {
        _newTokenCalls.Add(resolved);
        return Task.FromResult(NewTokenResult);
    }

    /// <inheritdoc/>
    public OAuth2TokenStatus GetStatus(OAuth2Settings resolved) => Status;

    /// <inheritdoc/>
    public void Clear(OAuth2Settings resolved) => ClearCount++;
}
