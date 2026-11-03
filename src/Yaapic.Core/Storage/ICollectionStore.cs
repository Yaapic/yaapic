using Yaapic.Domain.Collections;

namespace Yaapic.Core.Storage;

/// <summary>Loads the collection tree and manages folders and the placement of requests.</summary>
/// <remarks>Request content is saved and deleted through <see cref="IRequestStore"/>.</remarks>
public interface ICollectionStore
{
    /// <summary>Loads the tree.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The roots (top-level folders and unfiled requests), ordered with <see cref="CollectionNode.Sort"/>.</returns>
    /// <exception cref="StorageException">The store cannot be read.</exception>
    Task<IReadOnlyList<CollectionNode>> LoadTreeAsync(CancellationToken ct);

    /// <summary>Creates a folder, or updates the name, description and variables of an existing one in place.</summary>
    /// <param name="folder">The folder to save.</param>
    /// <param name="parentId">The parent of a new folder (null = top level); ignored for an existing folder, use <see cref="MoveAsync"/> to relocate it.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written, or a new folder's parent is unknown.</exception>
    Task SaveFolderAsync(FolderDefinition folder, Guid? parentId, CancellationToken ct);

    /// <summary>Deletes a folder and everything below it, requests included; an unknown identifier is a no-op.</summary>
    /// <param name="id">The folder identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    Task DeleteFolderAsync(Guid id, CancellationToken ct);

    /// <summary>Moves a folder or a request under another folder; moving to the current parent is a no-op.</summary>
    /// <param name="id">The identifier of the folder or request to move.</param>
    /// <param name="newParentId">The target folder (null = root / unfiled).</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="StorageException">The id or the target is unknown, a folder would move into itself or a descendant, or the store cannot be written.</exception>
    Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct);
}
