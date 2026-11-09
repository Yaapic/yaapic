namespace Yaapic.Infrastructure.Secrets;

/// <summary>One AES-256-GCM encrypted secret value.</summary>
/// <param name="Nonce">The 12-byte random nonce.</param>
/// <param name="Ciphertext">The ciphertext, same length as the plaintext UTF-8 bytes.</param>
/// <param name="Tag">The 16-byte authentication tag.</param>
internal sealed record EncryptedSecret(byte[] Nonce, byte[] Ciphertext, byte[] Tag);
