using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="ICollectionStore"/> kept in memory, on top of an <see cref="InMemoryRequestStore"/>.</summary>
/// <param name="requests">The request store holding the request content.</param>
public sealed class InMemoryCollectionStore(InMemoryRequestStore requests) : ICollectionStore
{
    private readonly Dictionary<Guid, FolderDefinition> _folders = [];

    // Parent of each folder and of each moved request; an absent request is unfiled.
    private readonly Dictionary<Guid, Guid?> _parents = [];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CollectionNode>> LoadTreeAsync(CancellationToken ct)
    {
        var all = await requests.ListAsync(ct);
        return Build(null);

        IReadOnlyList<CollectionNode> Build(Guid? parent) =>
            CollectionNode.Sort(
                _folders.Values.Where(f => _parents[f.Id] == parent)
                    .Select(f => CollectionNode.ForFolder(f, Build(f.Id)))
                    .Concat(all.Where(r => ParentOfRequest(r.Id) == parent).Select(CollectionNode.ForRequest)));
    }

    /// <summary>Gets the number of <see cref="SaveFolderAsync"/> calls.</summary>
    public int SaveFolderCount { get; private set; }

    /// <summary>Sets <see cref="SaveFolderCount"/> back to zero.</summary>
    public void ResetSaveCount() => SaveFolderCount = 0;

    /// <inheritdoc/>
    public Task SaveFolderAsync(FolderDefinition folder, Guid? parentId, CancellationToken ct)
    {
        SaveFolderCount++;
        if (_folders.ContainsKey(folder.Id))
            _folders[folder.Id] = folder;
        else
        {
            RequireFolder(parentId);
            _folders[folder.Id] = folder;
            _parents[folder.Id] = parentId;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task DeleteFolderAsync(Guid id, CancellationToken ct)
    {
        if (!_folders.ContainsKey(id)) return;
        var doomed = new HashSet<Guid>(Subtree(id)) { id };
        foreach (var r in await requests.ListAsync(ct))
            if (ParentOfRequest(r.Id) is { } p && doomed.Contains(p))
            {
                await requests.DeleteAsync(r.Id, ct);
                _parents.Remove(r.Id);
            }
        foreach (var f in doomed)
        {
            _folders.Remove(f);
            _parents.Remove(f);
        }
    }

    /// <inheritdoc/>
    public async Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct)
    {
        var isFolder = _folders.ContainsKey(id);
        if (!isFolder && !(await requests.ListAsync(ct)).Any(r => r.Id == id))
            throw new StorageException($"Unknown folder or request {id}.");
        RequireFolder(newParentId);
        if (isFolder && newParentId is { } t && (t == id || Subtree(id).Contains(t)))
            throw new StorageException("A folder cannot move into itself or one of its descendants.");
        _parents[id] = newParentId;
    }

    private Guid? ParentOfRequest(Guid id) =>
        _parents.TryGetValue(id, out var p) && p is { } f && _folders.ContainsKey(f) ? p : null;

    private void RequireFolder(Guid? id)
    {
        if (id is { } f && !_folders.ContainsKey(f)) throw new StorageException($"Unknown folder {f}.");
    }

    private IEnumerable<Guid> Subtree(Guid id)
    {
        foreach (var child in _folders.Keys.Where(f => _parents[f] == id).ToList())
        {
            yield return child;
            foreach (var d in Subtree(child)) yield return d;
        }
    }
}
