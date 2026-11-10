using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;

namespace Yaapic.Core.Secrets;

/// <summary>
/// An <see cref="ICollectionStore"/> that keeps the values of secret variables of top-level folders (collections) in the
/// vault, never in the files. Variables of nested folders are unused (ADR 0008) and pass through unchanged.
/// </summary>
/// <param name="inner">The store that persists the tree without secret values.</param>
/// <param name="vault">The secret vault.</param>
public sealed class SecretAwareCollectionStore(ICollectionStore inner, ISecretVault vault) : ICollectionStore
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<CollectionNode>> LoadTreeAsync(CancellationToken ct) =>
        [.. (await inner.LoadTreeAsync(ct)).Select(Fill)];

    /// <inheritdoc/>
    public async Task SaveFolderAsync(FolderDefinition folder, Guid? parentId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var roots = await inner.LoadTreeAsync(ct);
        var existing = CollectionNode.Flatten(roots).FirstOrDefault(n => n.Id == folder.Id && n.Folder is not null);
        var topLevel = existing is null ? parentId is null : roots.Any(r => r.Id == folder.Id);
        if (topLevel)
        {
            var previous = existing?.Folder!.Variables ?? [];
            var rows = await SecretRows.PrepareAsync(vault, SecretOwnerKind.Collection, folder.Id, folder.Variables, previous, ct);
            folder = folder with { Variables = rows };
        }

        await inner.SaveFolderAsync(folder, parentId, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteFolderAsync(Guid id, CancellationToken ct)
    {
        var topLevel = (await inner.LoadTreeAsync(ct)).Any(r => r.Id == id && r.Folder is not null);
        await inner.DeleteFolderAsync(id, ct);
        if (!topLevel || vault.State != VaultState.Unlocked)
        {
            return;
        }

        try
        {
            await vault.RemoveOwnerAsync(SecretOwnerKind.Collection, id, ct);
        }
        catch (VaultException ex)
        {
            throw new StorageException($"Cannot remove the secrets of the collection: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="StorageException">A folder with secret variables would become nested or top-level.</exception>
    public async Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct)
    {
        var roots = await inner.LoadTreeAsync(ct);
        var node = CollectionNode.Flatten(roots).FirstOrDefault(n => n.Id == id && n.Folder is not null);
        if (node is not null
            && roots.Any(r => r.Id == id) != (newParentId is null)
            && node.Folder!.Variables.Any(v => v.IsSecret))
        {
            throw new StorageException("Cannot move a collection that has secret variables");
        }

        await inner.MoveAsync(id, newParentId, ct);
    }

    private CollectionNode Fill(CollectionNode root) =>
        root.Folder is { } f
            ? CollectionNode.ForFolder(
                f with { Variables = SecretRows.Fill(vault, SecretOwnerKind.Collection, f.Id, f.Variables) }, root.Children)
            : root;
}
