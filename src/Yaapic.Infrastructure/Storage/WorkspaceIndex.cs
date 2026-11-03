using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Storage;

/// <summary>A folder found by <see cref="WorkspaceIndex"/>.</summary>
/// <param name="Folder">The parsed folder.</param>
/// <param name="DirectoryPath">The absolute path of its directory.</param>
/// <param name="ParentId">The id of the parent folder; <see langword="null"/> at the root.</param>
internal sealed record IndexedFolder(FolderDefinition Folder, string DirectoryPath, Guid? ParentId);

/// <summary>A request found by <see cref="WorkspaceIndex"/>.</summary>
/// <param name="Request">The parsed request.</param>
/// <param name="FilePath">The absolute path of its file.</param>
/// <param name="ParentId">The id of the parent folder; <see langword="null"/> at the root.</param>
internal sealed record IndexedRequest(HttpRequestDefinition Request, string FilePath, Guid? ParentId);

/// <summary>A snapshot of the <c>requests/</c> tree, answering which folder or file holds an id.</summary>
internal sealed class WorkspaceIndex
{
    private const string FolderFileName = "folder.yaml";
    private const string RequestSuffix = ".request.yaml";

    private readonly List<IndexedFolder> _folders = [];
    private readonly List<IndexedRequest> _requests = [];
    private readonly List<string> _errors = [];
    private readonly Dictionary<Guid, IndexedFolder> _folderById = [];
    private readonly Dictionary<Guid, IndexedRequest> _requestById = [];
    private readonly string _root;

    private WorkspaceIndex(string root) => _root = root;

    /// <summary>Gets the folders, parents before children.</summary>
    public IReadOnlyList<IndexedFolder> Folders => _folders;

    /// <summary>Gets the requests.</summary>
    public IReadOnlyList<IndexedRequest> Requests => _requests;

    /// <summary>Gets the skipped entries, as relative paths followed by the reason.</summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>Walks the requests tree once.</summary>
    /// <param name="requestsRoot">The <c>requests</c> directory; a missing directory gives an empty index.</param>
    /// <returns>The index.</returns>
    public static WorkspaceIndex Scan(string requestsRoot)
    {
        var index = new WorkspaceIndex(requestsRoot);
        if (Directory.Exists(requestsRoot))
        {
            index.ScanDirectory(requestsRoot, null);
        }

        return index;
    }

    /// <summary>Finds a folder by id.</summary>
    /// <param name="id">The folder id.</param>
    /// <param name="folder">The folder, when found.</param>
    /// <returns><see langword="true"/> when found.</returns>
    public bool TryGetFolder(Guid id, out IndexedFolder folder) => _folderById.TryGetValue(id, out folder!);

    /// <summary>Finds a request by id.</summary>
    /// <param name="id">The request id.</param>
    /// <param name="request">The request, when found.</param>
    /// <returns><see langword="true"/> when found.</returns>
    public bool TryGetRequest(Guid id, out IndexedRequest request) => _requestById.TryGetValue(id, out request!);

    /// <summary>Builds the collection tree.</summary>
    /// <returns>The root nodes, sorted with <see cref="CollectionNode.Sort"/>.</returns>
    public IReadOnlyList<CollectionNode> BuildTree()
    {
        var foldersByParent = _folders.ToLookup(f => f.ParentId);
        var requestsByParent = _requests.ToLookup(r => r.ParentId);

        IEnumerable<CollectionNode> ChildrenOf(Guid? parentId) =>
            foldersByParent[parentId].Select(f => CollectionNode.ForFolder(f.Folder, [.. ChildrenOf(f.Folder.Id)]))
                .Concat(requestsByParent[parentId].Select(r => CollectionNode.ForRequest(r.Request)));

        return CollectionNode.Sort(ChildrenOf(null));
    }

    private void ScanDirectory(string directory, Guid? parentId)
    {
        var entries = Directory.EnumerateFileSystemEntries(directory)
            .Where(p => !Path.GetFileName(p).StartsWith('.'))
            .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal);
        foreach (var path in entries)
        {
            if (Directory.Exists(path))
            {
                ScanFolder(path, parentId);
            }
            else if (path.EndsWith(RequestSuffix, StringComparison.Ordinal))
            {
                ScanRequest(path, parentId);
            }
        }
    }

    private void ScanFolder(string directory, Guid? parentId)
    {
        var file = Path.Combine(directory, FolderFileName);
        if (!File.Exists(file))
        {
            Report(directory, "missing folder.yaml, subtree skipped");
            return;
        }

        FolderDefinition folder;
        try
        {
            folder = FolderYamlSerializer.Deserialize(File.ReadAllText(file));
        }
        catch (Exception ex) when (ex is StorageException or IOException or UnauthorizedAccessException)
        {
            Report(file, ex.Message + ", subtree skipped");
            return;
        }

        if (IsDuplicate(folder.Id))
        {
            Report(file, "duplicate id, subtree skipped");
            return;
        }

        var indexed = new IndexedFolder(folder, directory, parentId);
        _folders.Add(indexed);
        _folderById[folder.Id] = indexed;
        ScanDirectory(directory, folder.Id);
    }

    private void ScanRequest(string file, Guid? parentId)
    {
        HttpRequestDefinition request;
        try
        {
            request = RequestYamlSerializer.Deserialize(File.ReadAllText(file));
        }
        catch (Exception ex) when (ex is StorageException or IOException or UnauthorizedAccessException)
        {
            Report(file, ex.Message);
            return;
        }

        if (IsDuplicate(request.Id))
        {
            Report(file, "duplicate id");
            return;
        }

        var indexed = new IndexedRequest(request, file, parentId);
        _requests.Add(indexed);
        _requestById[request.Id] = indexed;
    }

    private bool IsDuplicate(Guid id) => _folderById.ContainsKey(id) || _requestById.ContainsKey(id);

    private void Report(string path, string reason) =>
        _errors.Add($"{Path.GetRelativePath(_root, path).Replace('\\', '/')}: {reason}");
}
