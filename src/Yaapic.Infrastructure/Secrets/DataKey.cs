using System.Security.Cryptography;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>Creates and clears the 256-bit vault data key.</summary>
internal static class DataKey
{
    /// <summary>The key length in bytes.</summary>
    internal const int Length = 32;

    /// <summary>Generates a new random data key.</summary>
    /// <returns>32 random bytes.</returns>
    internal static byte[] Generate() => RandomNumberGenerator.GetBytes(Length);

    /// <summary>Zeroes a key in memory.</summary>
    /// <param name="key">The key to clear.</param>
    internal static void Clear(byte[] key) => CryptographicOperations.ZeroMemory(key);
}
