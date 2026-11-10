using Yaapic.Core.Storage;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Secrets;

/// <summary>Moves the values of secret variable rows between a store and the vault.</summary>
internal static class SecretRows
{
    /// <summary>Fills the value of each secret row from the vault; empty when locked, missing or never set.</summary>
    internal static IReadOnlyList<KeyValueEntry> Fill(ISecretVault vault, SecretOwnerKind kind, Guid ownerId, IReadOnlyList<KeyValueEntry> rows) =>
        [.. rows.Select(r => r.IsSecret
            ? r with { Value = vault.TryGet(new SecretRef(kind, ownerId, r.Key), out var v) ? v : "" }
            : r)];

    /// <summary>Writes the secret values to the vault and returns the rows to store, with secret values blanked.</summary>
    /// <param name="vault">The vault.</param>
    /// <param name="kind">The owner kind.</param>
    /// <param name="ownerId">The owner identifier.</param>
    /// <param name="rows">The rows to save.</param>
    /// <param name="previous">The rows currently stored for this owner.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The rows to hand to the inner store.</returns>
    /// <exception cref="StorageException">A secret cannot be stored, or a row is newly flagged while the vault is not unlocked.</exception>
    internal static async Task<IReadOnlyList<KeyValueEntry>> PrepareAsync(
        ISecretVault vault, SecretOwnerKind kind, Guid ownerId, IReadOnlyList<KeyValueEntry> rows,
        IReadOnlyList<KeyValueEntry> previous, CancellationToken ct)
    {
        var previousNames = previous.Where(r => r.IsSecret).Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        var currentNames = rows.Where(r => r.IsSecret).Select(r => r.Key).ToHashSet(StringComparer.Ordinal);

        if (vault.State != VaultState.Unlocked)
        {
            var added = rows.FirstOrDefault(r => r.IsSecret && !previousNames.Contains(r.Key));
            if (added is not null)
            {
                throw new StorageException($"Unlock the vault to mark '{added.Key}' as secret");
            }
        }
        else
        {
            var current = string.Empty;
            try
            {
                foreach (var row in rows.Where(r => r.IsSecret))
                {
                    current = row.Key;
                    await vault.SetAsync(new SecretRef(kind, ownerId, row.Key), row.Value, ct);
                }

                foreach (var name in previousNames.Where(n => !currentNames.Contains(n)))
                {
                    current = name;
                    await vault.RemoveAsync(new SecretRef(kind, ownerId, name), ct);
                }
            }
            catch (VaultException ex)
            {
                throw new StorageException($"Cannot save secret '{current}': {ex.Message}", ex);
            }
        }

        return [.. rows.Select(r => r.IsSecret ? r with { Value = "" } : r)];
    }
}
