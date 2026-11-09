namespace Yaapic.Core.Secrets;

/// <summary>A data key wrapped by a key protector.</summary>
/// <param name="Bytes">The wrapped key bytes.</param>
/// <param name="Kdf">The key derivation parameters, for passphrase protectors; otherwise <see langword="null"/>.</param>
public sealed record WrappedKey(byte[] Bytes, VaultKdf? Kdf);

/// <summary>The parameters of a passphrase key derivation.</summary>
/// <param name="Algorithm">The algorithm name.</param>
/// <param name="Iterations">The iteration count.</param>
/// <param name="Salt">The salt.</param>
public sealed record VaultKdf(string Algorithm, int Iterations, byte[] Salt);
