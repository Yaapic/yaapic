using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Stores the collection tree as folders of <c>requests/</c> in a workspace.</summary>
/// <param name="workspaceRoot">The workspace root directory.</param>
public sealed class FileCollectionStore(string workspaceRoot) : ICollectionStore
{
    private const string FolderFileName = "folder.yaml";
    private const string RequestSuffix = ".request.yaml";
    private readonly string _directory = Path.Combine(workspaceRoot, "requests");

    /// <summary>Gets the entries skipped by the last <see cref="LoadTreeAsync"/> call, as relative paths followed by the reason.</summary>
    public IReadOnlyList<string> LastLoadErrors { get; private set; } = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<CollectionNode>> LoadTreeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var index = WorkspaceIndex.Scan(_directory);
            LastLoadErrors = index.Errors;
            return Task.FromResult(index.BuildTree());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot read '{_directory}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task SaveFolderAsync(FolderDefinition folder, Guid? parentId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(folder);
        try
        {
            var index = WorkspaceIndex.Scan(_directory);
            var slug = WorkspaceFiles.Slugify(folder.Name, "folder");
            string directory;
            if (index.TryGetFolder(folder.Id, out var existing))
            {
                directory = existing.DirectoryPath;
                if (folder.Name != existing.Folder.Name)
                {
                    var parent = Path.GetDirectoryName(directory)!;
                    var target = Path.Combine(parent, WorkspaceFiles.UniqueName(parent, slug, "", directory));
                    if (!string.Equals(target, directory, StringComparison.Ordinal))
                    {
                        Directory.Move(directory, target);
                        directory = target;
                    }
                }
            }
            else
            {
                var parent = _directory;
                if (parentId is { } pid)
                {
                    parent = index.TryGetFolder(pid, out var p)
                        ? p.DirectoryPath
                        : throw new StorageException($"Unknown parent folder '{pid}'.");
                }

                directory = Path.Combine(parent, WorkspaceFiles.UniqueName(parent, slug, "", null));
            }

            await WorkspaceFiles.WriteAtomicAsync(
                Path.Combine(directory, FolderFileName), FolderYamlSerializer.Serialize(folder), ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot write folder '{folder.Name}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public Task DeleteFolderAsync(Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            if (WorkspaceIndex.Scan(_directory).TryGetFolder(id, out var folder))
            {
                Directory.Delete(folder.DirectoryPath, true);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot delete folder '{id}': {ex.Message}", ex);
        }
    }

    /// <remarks>The id to path map of <see cref="FileRequestStore"/> is not shared: it rescans on its next list.</remarks>
    /// <inheritdoc />
    public Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var index = WorkspaceIndex.Scan(_directory);
            var isFolder = index.TryGetFolder(id, out var folder);
            IndexedRequest? request = null;
            if (!isFolder && !index.TryGetRequest(id, out request))
            {
                throw new StorageException("Unknown item");
            }

            var targetDirectory = _directory;
            if (newParentId is { } pid)
            {
                targetDirectory = index.TryGetFolder(pid, out var parent)
                    ? parent.DirectoryPath
                    : throw new StorageException($"Unknown target folder '{pid}'.");
            }

            var target = Path.GetFullPath(targetDirectory);
            if (isFolder)
            {
                var own = Path.GetFullPath(folder!.DirectoryPath);
                if (string.Equals(target, own, StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith(own + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new StorageException("Cannot move a folder into itself or one of its descendants.");
                }

                if (!SameDirectory(Path.GetDirectoryName(own)!, target))
                {
                    var name = WorkspaceFiles.UniqueName(target, Path.GetFileName(own), "", own);
                    Directory.Move(own, Path.Combine(target, name));
                }
            }
            else
            {
                var file = Path.GetFullPath(request!.FilePath);
                if (!SameDirectory(Path.GetDirectoryName(file)!, target))
                {
                    var slug = Path.GetFileName(file)[..^RequestSuffix.Length];
                    var name = WorkspaceFiles.UniqueName(target, slug, RequestSuffix, file);
                    File.Move(file, Path.Combine(target, name));
                }
            }

            return Task.CompletedTask;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot move '{id}': {ex.Message}", ex);
        }
    }

    private static bool SameDirectory(string a, string b) =>
        string.Equals(a.TrimEnd(Path.DirectorySeparatorChar), b.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
}
