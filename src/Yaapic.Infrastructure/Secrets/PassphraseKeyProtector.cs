using Yaapic.Core.Secrets;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>Wraps the data key with a key derived from a passphrase (PBKDF2-HMAC-SHA512, AES-256-GCM).</summary>
/// <param name="passphrase">The passphrase.</param>
/// <param name="iterations">The iteration count used when wrapping.</param>
public sealed class PassphraseKeyProtector(string passphrase, int iterations) : IKeyProtector
{
    /// <summary>The protector identifier.</summary>
    public const string ProtectorId = "passphrase";

    /// <summary>Creates a protector with the production iteration count.</summary>
    /// <param name="passphrase">The passphrase.</param>
    public PassphraseKeyProtector(string passphrase)
        : this(passphrase, PassphraseKdf.DefaultIterations)
    {
    }

    /// <inheritdoc />
    public string Id => ProtectorId;

    /// <inheritdoc />
    public bool RequiresPassphrase => true;

    /// <inheritdoc />
    public bool IsAvailable(out string? reason)
    {
        reason = null;
        return true;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The passphrase is empty.</exception>
    public WrappedKey Wrap(ReadOnlySpan<byte> dataKey, string workspaceId) => PassphraseKdf.Wrap(dataKey, passphrase, iterations);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The passphrase is empty.</exception>
    public byte[] Unwrap(WrappedKey wrapped, string workspaceId) => PassphraseKdf.Unwrap(wrapped, passphrase);
}
