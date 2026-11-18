using System.Security.Cryptography;
using System.Text;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Auth.OAuth2;

/// <summary>The stable cache key of an OAuth 2.0 configuration.</summary>
/// <param name="Value">The lowercase hex SHA-256 digest.</param>
public readonly record struct OAuth2TokenKey(string Value)
{
    /// <summary>Builds the key from resolved settings; the client secret and the password are not part of it.</summary>
    /// <param name="settings">The resolved settings.</param>
    /// <returns>The key.</returns>
    public static OAuth2TokenKey For(OAuth2Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var text = $"{settings.Grant}|{settings.TokenUrl}|{settings.ClientId}|{settings.Scope}|{settings.Audience}|{settings.Username}";
        return new(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
    }
}
