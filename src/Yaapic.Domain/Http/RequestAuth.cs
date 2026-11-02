namespace Yaapic.Domain.Http;

/// <summary>The authentication of a request; only the members matching <paramref name="Kind"/> are used.</summary>
/// <param name="Kind">The authentication kind.</param>
/// <param name="Username">The Basic username.</param>
/// <param name="Password">The Basic password.</param>
/// <param name="Token">The bearer token.</param>
/// <param name="Key">The API key name.</param>
/// <param name="Value">The API key value.</param>
/// <param name="Location">Where the API key is sent.</param>
public sealed record RequestAuth(
    AuthKind Kind,
    string Username,
    string Password,
    string Token,
    string Key,
    string Value,
    ApiKeyLocation Location)
{
    /// <summary>No authentication: every string is empty.</summary>
    public static RequestAuth None { get; } = new(AuthKind.None, "", "", "", "", "", ApiKeyLocation.Header);
}
