using System.Security.Cryptography;
using System.Text;
using Yaapic.Core.Secrets;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>
/// Wraps the data key with a key derived from a passphrase (PBKDF2-HMAC-SHA512 + AES-256-GCM).
/// Both operations are synchronous and CPU-bound (about 0.3 s): call them from a background thread.
/// </summary>
internal static class PassphraseKdf
{
    internal const string Algorithm = "pbkdf2-sha512";
    internal const int DefaultIterations = 600_000;
    internal const int MinIterations = 100_000;
    internal const int MaxIterations = 10_000_000;
    private const int SaltSize = 16;

    private static readonly byte[] WrapAad = Encoding.UTF8.GetBytes("yaapic-vault-v1|wrap");

    /// <summary>Wraps a data key.</summary>
    /// <param name="dataKey">The key to wrap.</param>
    /// <param name="passphrase">The non-empty passphrase.</param>
    /// <param name="iterations">The PBKDF2 iteration count; production code keeps the default, tests lower it.</param>
    /// <returns>The wrapped key: <c>nonce | ciphertext | tag</c> plus the derivation parameters.</returns>
    /// <exception cref="ArgumentException">The passphrase is empty.</exception>
    internal static WrappedKey Wrap(ReadOnlySpan<byte> dataKey, string passphrase, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Derive(passphrase, salt, iterations);
        try
        {
            var bytes = new byte[SecretCipher.NonceSize + dataKey.Length + SecretCipher.TagSize];
            var nonce = bytes.AsSpan(0, SecretCipher.NonceSize);
            RandomNumberGenerator.Fill(nonce);
            using var aes = new AesGcm(key, SecretCipher.TagSize);
            aes.Encrypt(nonce, dataKey, bytes.AsSpan(SecretCipher.NonceSize, dataKey.Length), bytes.AsSpan(^SecretCipher.TagSize), WrapAad);
            return new WrappedKey(bytes, new VaultKdf(Algorithm, iterations, salt));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Unwraps a data key.</summary>
    /// <param name="wrapped">The wrapped key.</param>
    /// <param name="passphrase">The non-empty passphrase.</param>
    /// <returns>The data key.</returns>
    /// <exception cref="ArgumentException">The passphrase is empty.</exception>
    /// <exception cref="VaultException">Wrong passphrase, or unsupported derivation parameters.</exception>
    internal static byte[] Unwrap(WrappedKey wrapped, string passphrase)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);
        var kdf = wrapped.Kdf;
        if (kdf is null || kdf.Algorithm != Algorithm || kdf.Iterations is < MinIterations or > MaxIterations)
        {
            throw new VaultException("Unsupported vault key derivation");
        }

        var bytes = wrapped.Bytes;
        if (bytes.Length < SecretCipher.NonceSize + SecretCipher.TagSize)
        {
            throw new VaultException("Wrong passphrase");
        }

        var key = Derive(passphrase, kdf.Salt, kdf.Iterations);
        var dataKey = new byte[bytes.Length - SecretCipher.NonceSize - SecretCipher.TagSize];
        try
        {
            using var aes = new AesGcm(key, SecretCipher.TagSize);
            aes.Decrypt(
                bytes.AsSpan(0, SecretCipher.NonceSize),
                bytes.AsSpan(SecretCipher.NonceSize, dataKey.Length),
                bytes.AsSpan(^SecretCipher.TagSize),
                dataKey,
                WrapAad);
            return dataKey;
        }
        catch (CryptographicException e)
        {
            CryptographicOperations.ZeroMemory(dataKey);
            throw new VaultException("Wrong passphrase", e);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] Derive(string passphrase, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), salt, iterations, HashAlgorithmName.SHA512, DataKey.Length);
}
