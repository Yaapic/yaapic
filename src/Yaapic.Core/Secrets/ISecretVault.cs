namespace Yaapic.Core.Secrets;

/// <summary>The encrypted store of secret variable values.</summary>
public interface ISecretVault
{
    /// <summary>Gets the current state.</summary>
    VaultState State { get; }

    /// <summary>Gets the identifier of the protector of the vault; <see langword="null"/> when not created.</summary>
    string? ProtectorId { get; }

    /// <summary>Raised once each time <see cref="State"/> changes.</summary>
    event EventHandler? StateChanged;

    /// <summary>Reads the vault file and unlocks it automatically when its protector needs no passphrase.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the vault is read.</returns>
    /// <exception cref="VaultException">The file is corrupt.</exception>
    Task InitializeAsync(CancellationToken ct);

    /// <summary>Creates the vault and leaves it unlocked.</summary>
    /// <param name="protectorId">The protector identifier.</param>
    /// <param name="passphrase">The passphrase, when the protector requires one.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the vault is created.</returns>
    /// <exception cref="VaultException">The protector failed or the vault already exists.</exception>
    Task CreateAsync(string protectorId, string? passphrase, CancellationToken ct);

    /// <summary>Unlocks the vault with a passphrase.</summary>
    /// <param name="passphrase">The passphrase.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns><see langword="false"/> when the passphrase is wrong.</returns>
    Task<bool> UnlockAsync(string passphrase, CancellationToken ct);

    /// <summary>Locks the vault and drops the decrypted cache.</summary>
    void Lock();

    /// <summary>Reads a decrypted secret from the cache.</summary>
    /// <param name="secret">The secret reference.</param>
    /// <param name="value">The value; empty when not found.</param>
    /// <returns><see langword="false"/> when the vault is locked or the secret is missing.</returns>
    bool TryGet(SecretRef secret, out string value);

    /// <summary>Stores a secret.</summary>
    /// <param name="secret">The secret reference.</param>
    /// <param name="value">The plain value.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the secret is stored.</returns>
    /// <exception cref="VaultException">The vault is locked or missing.</exception>
    Task SetAsync(SecretRef secret, string value, CancellationToken ct);

    /// <summary>Removes a secret; a missing secret is ignored.</summary>
    /// <param name="secret">The secret reference.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the secret is removed.</returns>
    /// <exception cref="VaultException">The vault is locked or missing.</exception>
    Task RemoveAsync(SecretRef secret, CancellationToken ct);

    /// <summary>Removes every secret of an owner.</summary>
    /// <param name="kind">The owner kind.</param>
    /// <param name="ownerId">The owner identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the secrets are removed.</returns>
    /// <exception cref="VaultException">The vault is locked or missing.</exception>
    Task RemoveOwnerAsync(SecretOwnerKind kind, Guid ownerId, CancellationToken ct);

    /// <summary>Lists the decrypted values of at least four characters, longest first, for the redactor.</summary>
    /// <returns>The values; empty when the vault is locked.</returns>
    IReadOnlyList<string> GetSecretValues();
}
