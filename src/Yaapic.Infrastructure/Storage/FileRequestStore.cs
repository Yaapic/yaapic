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

    /// <summary>Gets the names of the files skipped by the last <see cref="ListAsync"/> call (corrupt or duplicate id).</summary>
    public IReadOnlyList<string> LastListErrors { get; private set; } = [];

    /// <inheritdoc />
    public async Task<IReadOnlyList<HttpRequestDefinition>> ListAsync(CancellationToken ct)
    {
        var errors = new List<string>();
        var found = new Dictionary<Guid, (HttpRequestDefinition Request, string Path)>();
        try
        {
            if (Directory.Exists(_directory))
            {
                var files = Directory.EnumerateFiles(_directory, "*" + Extension, SearchOption.TopDirectoryOnly)
                    .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal);
                foreach (var file in files)
                {
                    try
                    {
                        var request = RequestYamlSerializer.Deserialize(await File.ReadAllTextAsync(file, ct));
                        if (!found.TryAdd(request.Id, (request, file)))
                        {
                            errors.Add(Path.GetFileName(file));
                        }
                    }
                    catch (StorageException)
                    {
                        errors.Add(Path.GetFileName(file));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot read '{_directory}': {ex.Message}", ex);
        }

        LastListErrors = errors;
        _paths.Clear();
        foreach (var (id, entry) in found)
        {
            _paths[id] = entry.Path;
        }

        return [.. found.Values.Select(e => e.Request)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id)];
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
            var slug = WorkspaceFiles.Slugify(request.Name, "request");
            var path = Path.Combine(_directory, slug + Extension);
            for (var n = 2; _paths.Any(p => p.Key != request.Id && IsSamePath(p.Value, path)); n++)
            {
                path = Path.Combine(_directory, $"{slug}-{n}{Extension}");
            }

            await WorkspaceFiles.WriteAtomicAsync(path, RequestYamlSerializer.Serialize(request), ct);
            if (_paths.TryGetValue(request.Id, out var old) && !IsSamePath(old, path))
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
