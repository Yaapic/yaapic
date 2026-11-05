namespace Yaapic.Core.Scripting;

/// <summary>A cookie read from a <c>Set-Cookie</c> header of the current response.</summary>
/// <param name="Name">The cookie name.</param>
/// <param name="Value">The cookie value, unquoted.</param>
/// <param name="Domain">The <c>Domain</c> attribute, or <see langword="null"/>.</param>
/// <param name="Path">The <c>Path</c> attribute, or <see langword="null"/>.</param>
/// <param name="Expires">The <c>Expires</c> attribute; <c>Max-Age</c> is ignored in v1.</param>
/// <param name="HttpOnly">Whether the <c>HttpOnly</c> flag is present.</param>
/// <param name="Secure">Whether the <c>Secure</c> flag is present.</param>
/// <param name="SameSite">The <c>SameSite</c> attribute, or <see langword="null"/>.</param>
public sealed record ScriptCookie(
    string Name,
    string Value,
    string? Domain,
    string? Path,
    DateTimeOffset? Expires,
    bool HttpOnly,
    bool Secure,
    string? SameSite);
