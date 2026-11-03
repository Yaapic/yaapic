using Yaapic.Core.Storage;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Stores requests as <c>requests/*.request.yaml</c> files in a workspace.</summary>
/// <param name="workspaceRoot">The workspace root directory.</param>
public sealed class FileRequestStore(string workspaceRoot) : IRequestStore
{
    private const string Extension = ".request.yaml";
    private readonly string _directory = Path.Combine(workspaceRoot, "requests");
    private readonly Dictionary<Guid, string> _paths = [];

    /// <summary>Gets the entries skipped by the last <see cref="ListAsync"/> call, as relative paths followed by the reason.</summary>
    public IReadOnlyList<string> LastListErrors { get; private set; } = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<HttpRequestDefinition>> ListAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var index = WorkspaceIndex.Scan(_directory);
            LastListErrors = index.Errors;
            _paths.Clear();
            foreach (var r in index.Requests)
            {
                _paths[r.Request.Id] = r.FilePath;
            }

            IReadOnlyList<HttpRequestDefinition> list = [.. index.Requests.Select(r => r.Request)
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Id)];
            return Task.FromResult(list);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot read '{_directory}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(HttpRequestDefinition request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_paths.Count == 0)
        {
            await ListAsync(ct);
        }

        try
        {
            _paths.TryGetValue(request.Id, out var old);
            var directory = old is null ? _directory : Path.GetDirectoryName(old)!;
            var name = WorkspaceFiles.UniqueName(directory, WorkspaceFiles.Slugify(request.Name, "request"), Extension, old);
            var path = Path.Combine(directory, name);

            await WorkspaceFiles.WriteAtomicAsync(path, RequestYamlSerializer.Serialize(request), ct);
            if (old is not null && !IsSamePath(old, path))
            {
                File.Delete(old);
            }

            _paths[request.Id] = path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot write request '{request.Name}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        if (_paths.Count == 0)
        {
            await ListAsync(ct);
        }

        if (!_paths.Remove(id, out var path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot delete '{path}': {ex.Message}", ex);
        }
    }

    private static bool IsSamePath(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
