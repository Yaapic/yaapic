using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Stores the collection tree as folders of <c>requests/</c> in a workspace.</summary>
/// <param name="workspaceRoot">The workspace root directory.</param>
public sealed class FileCollectionStore(string workspaceRoot) : ICollectionStore
{
    private const string FolderFileName = "folder.yaml";
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

    /// <inheritdoc />
    public Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct) =>
        throw new NotSupportedException("Implemented by M2-31");
}
