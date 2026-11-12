using Yaapic.Core.Storage;
using Yaapic.Domain.Environments;
using Yaapic.Localization;

namespace Yaapic.Core.Secrets;

/// <summary>An <see cref="IEnvironmentStore"/> that keeps the values of secret variables in the vault, never in the files.</summary>
/// <param name="inner">The store that persists the environments without secret values.</param>
/// <param name="vault">The secret vault.</param>
public sealed class SecretAwareEnvironmentStore(IEnvironmentStore inner, ISecretVault vault) : IEnvironmentStore
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<EnvironmentDefinition>> ListAsync(CancellationToken ct) =>
        [.. (await inner.ListAsync(ct)).Select(e =>
            e with { Variables = SecretRows.Fill(vault, SecretOwnerKind.Environment, e.Id, e.Variables) })];

    /// <inheritdoc/>
    public async Task SaveAsync(EnvironmentDefinition environment, CancellationToken ct)
    {
        var previous = (await inner.ListAsync(ct)).FirstOrDefault(e => e.Id == environment.Id)?.Variables ?? [];
        var rows = await SecretRows.PrepareAsync(vault, SecretOwnerKind.Environment, environment.Id, environment.Variables, previous, ct);
        await inner.SaveAsync(environment with { Variables = rows }, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await inner.DeleteAsync(id, ct);
        if (vault.State != VaultState.Unlocked)
        {
            return;
        }

        try
        {
            await vault.RemoveOwnerAsync(SecretOwnerKind.Environment, id, ct);
        }
        catch (VaultException ex)
        {
            throw new StorageException(Strings.SecretsRemoveEnvironmentFailed(ex.Message), ex);
        }
    }
}
