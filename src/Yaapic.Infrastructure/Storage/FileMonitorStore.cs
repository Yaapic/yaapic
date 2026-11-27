using Yaapic.Core.Storage;
using Yaapic.Domain.Monitors;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Stores monitors as <c>monitors/*.monitor.yaml</c> files in a workspace.</summary>
/// <param name="workspaceRoot">The workspace root directory.</param>
public sealed class FileMonitorStore(string workspaceRoot) : IMonitorStore
{
    private const string Extension = ".monitor.yaml";
    private readonly string _directory = Path.Combine(workspaceRoot, "monitors");
    private readonly Dictionary<Guid, string> _paths = [];

    /// <summary>Gets the names of the files skipped by the last <see cref="ListAsync"/> call (corrupt or duplicate id) or loaded with a normalised interval.</summary>
    public IReadOnlyList<string> LastListErrors { get; private set; } = [];

    /// <inheritdoc />
    public async Task<IReadOnlyList<MonitorDefinition>> ListAsync(CancellationToken ct)
    {
        var errors = new List<string>();
        var found = new Dictionary<Guid, (MonitorDefinition Monitor, string Path)>();
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
                        var monitor = MonitorYamlSerializer.Deserialize(await File.ReadAllTextAsync(file, ct), out var adjusted);
                        if (adjusted)
                        {
                            errors.Add(Path.GetFileName(file));
                        }

                        if (!found.TryAdd(monitor.Id, (monitor, file)))
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

        return [.. found.Values.Select(e => e.Monitor)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id)];
    }

    /// <inheritdoc />
    public async Task SaveAsync(MonitorDefinition monitor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        if (_paths.Count == 0)
        {
            await ListAsync(ct);
        }

        try
        {
            var slug = WorkspaceFiles.Slugify(monitor.Name, "monitor");
            var path = Path.Combine(_directory, slug + Extension);
            for (var n = 2; _paths.Any(p => p.Key != monitor.Id && IsSamePath(p.Value, path)); n++)
            {
                path = Path.Combine(_directory, $"{slug}-{n}{Extension}");
            }

            await WorkspaceFiles.WriteAtomicAsync(path, MonitorYamlSerializer.Serialize(monitor), ct);
            if (_paths.TryGetValue(monitor.Id, out var old) && !IsSamePath(old, path))
            {
                File.Delete(old);
            }

            _paths[monitor.Id] = path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot write monitor '{monitor.Name}': {ex.Message}", ex);
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
