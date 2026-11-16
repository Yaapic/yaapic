namespace Yaapic.Core.Auth.OAuth2;

/// <summary>An OAuth 2.0 access token with its optional refresh token and expiry.</summary>
/// <param name="AccessToken">The access token.</param>
/// <param name="TokenType">The token type, for example <c>Bearer</c>.</param>
/// <param name="RefreshToken">The refresh token, when the server issued one.</param>
/// <param name="Scope">The granted scope, when the server returned one.</param>
/// <param name="ExpiresAt">The expiry instant; <see langword="null"/> means the token never expires.</param>
/// <param name="ObtainedAt">The instant the token was obtained.</param>
public sealed record OAuth2Token(
    string AccessToken,
    string TokenType,
    string? RefreshToken,
    string? Scope,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset ObtainedAt)
{
    /// <summary>The leeway applied before the real expiry (30 seconds).</summary>
    public static readonly TimeSpan DefaultLeeway = TimeSpan.FromSeconds(30);

    /// <summary>Tells whether the token is expired, counting a safety leeway.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="leeway">The leeway: the token counts as expired this long before <see cref="ExpiresAt"/>.</param>
    /// <returns><see langword="true"/> when <c>now + leeway &gt;= ExpiresAt</c>; never for a token without expiry.</returns>
    public bool IsExpired(DateTimeOffset now, TimeSpan leeway) => ExpiresAt is { } at && now + leeway >= at;

    /// <summary>Returns a description with the access and refresh tokens redacted.</summary>
    /// <returns>The redacted text.</returns>
    public override string ToString() =>
        $"OAuth2Token {{ AccessToken = ****, TokenType = {TokenType}, RefreshToken = {(RefreshToken is null ? "null" : "****")}, Scope = {Scope}, ExpiresAt = {ExpiresAt}, ObtainedAt = {ObtainedAt} }}";
}
