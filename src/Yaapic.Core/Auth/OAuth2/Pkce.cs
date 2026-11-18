using System.Security.Cryptography;
using System.Text;
using Yaapic.Core.Auth.Jwt;

namespace Yaapic.Core.Auth.OAuth2;

/// <summary>A PKCE code verifier and its S256 challenge.</summary>
/// <param name="Verifier">The code verifier.</param>
/// <param name="Challenge">The S256 code challenge.</param>
public sealed record PkcePair(string Verifier, string Challenge);

/// <summary>Proof Key for Code Exchange helpers (RFC 7636).</summary>
public static class Pkce
{
    /// <summary>Creates a pair from 32 random bytes.</summary>
    /// <returns>A fresh verifier and its challenge.</returns>
    public static PkcePair Create() => FromVerifier(Base64Url.Encode(RandomNumberGenerator.GetBytes(32)));

    /// <summary>Derives the S256 challenge of a verifier.</summary>
    /// <param name="verifier">The code verifier.</param>
    /// <returns>The pair.</returns>
    public static PkcePair FromVerifier(string verifier)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        return new(verifier, Base64Url.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }
}
