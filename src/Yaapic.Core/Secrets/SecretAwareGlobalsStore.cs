using Yaapic.Core.Storage;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Secrets;

/// <summary>An <see cref="IGlobalsStore"/> that keeps the values of secret variables in the vault, never in the files.</summary>
/// <param name="inner">The store that persists the globals without secret values.</param>
/// <param name="vault">The secret vault.</param>
public sealed class SecretAwareGlobalsStore(IGlobalsStore inner, ISecretVault vault) : IGlobalsStore
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<KeyValueEntry>> LoadAsync(CancellationToken ct) =>
        SecretRows.Fill(vault, SecretOwnerKind.Globals, Guid.Empty, await inner.LoadAsync(ct));

    /// <inheritdoc/>
    public async Task SaveAsync(IReadOnlyList<KeyValueEntry> variables, CancellationToken ct)
    {
        var previous = await inner.LoadAsync(ct);
        var rows = await SecretRows.PrepareAsync(vault, SecretOwnerKind.Globals, Guid.Empty, variables, previous, ct);
        await inner.SaveAsync(rows, ct);
    }
}
