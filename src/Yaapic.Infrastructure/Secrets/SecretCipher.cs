using System.Security.Cryptography;
using System.Text;
using Yaapic.Core.Secrets;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>Encrypts and decrypts one secret with AES-256-GCM, binding its identity as associated data.</summary>
internal static class SecretCipher
{
    internal const int NonceSize = 12;
    internal const int TagSize = 16;

    /// <summary>Encrypts a secret.</summary>
    /// <param name="dataKey">The 32-byte data key.</param>
    /// <param name="secret">The identity of the secret, bound as associated data.</param>
    /// <param name="plaintext">The value to encrypt.</param>
    /// <returns>The encrypted value with a fresh random nonce.</returns>
    internal static EncryptedSecret Encrypt(ReadOnlySpan<byte> dataKey, SecretRef secret, string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var data = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[data.Length];
        var tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(dataKey, TagSize);
            aes.Encrypt(nonce, data, ciphertext, tag, Aad(secret));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(data);
        }

        return new EncryptedSecret(nonce, ciphertext, tag);
    }

    /// <summary>Decrypts a secret.</summary>
    /// <param name="dataKey">The 32-byte data key.</param>
    /// <param name="secret">The identity the value was encrypted for.</param>
    /// <param name="value">The encrypted value.</param>
    /// <returns>The plaintext.</returns>
    /// <exception cref="VaultException">The key, the identity or the data does not match.</exception>
    internal static string Decrypt(ReadOnlySpan<byte> dataKey, SecretRef secret, EncryptedSecret value)
    {
        var data = new byte[value.Ciphertext.Length];
        try
        {
            using var aes = new AesGcm(dataKey, TagSize);
            aes.Decrypt(value.Nonce, value.Ciphertext, value.Tag, data, Aad(secret));
            return Encoding.UTF8.GetString(data);
        }
        catch (Exception e) when (e is CryptographicException or ArgumentException)
        {
            throw new VaultException("The vault entry cannot be decrypted", e);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(data);
        }
    }

    private static byte[] Aad(SecretRef secret) =>
        Encoding.UTF8.GetBytes($"yaapic-vault-v1|{secret.Kind}|{secret.OwnerId:D}|{secret.Name}");
}
