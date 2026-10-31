using Yaapic.Core.Storage;
using Yaapic.Domain.Environments;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Stores environments as <c>environments/*.env.yaml</c> files in a workspace.</summary>
/// <param name="workspaceRoot">The workspace root directory.</param>
public sealed class FileEnvironmentStore(string workspaceRoot) : IEnvironmentStore
{
    private const string Extension = ".env.yaml";
    private readonly string _directory = Path.Combine(workspaceRoot, "environments");
    private readonly Dictionary<Guid, string> _paths = [];

    /// <summary>Gets the names of the files skipped by the last <see cref="ListAsync"/> call (corrupt or duplicate id).</summary>
    public IReadOnlyList<string> LastListErrors { get; private set; } = [];

    /// <inheritdoc />
    public async Task<IReadOnlyList<EnvironmentDefinition>> ListAsync(CancellationToken ct)
    {
        var errors = new List<string>();
        var found = new Dictionary<Guid, (EnvironmentDefinition Environment, string Path)>();
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
                        var environment = EnvironmentYamlSerializer.Deserialize(await File.ReadAllTextAsync(file, ct));
                        if (!found.TryAdd(environment.Id, (environment, file)))
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

        return [.. found.Values.Select(e => e.Environment)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id)];
    }

    /// <inheritdoc />
    public async Task SaveAsync(EnvironmentDefinition environment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (_paths.Count == 0)
        {
            await ListAsync(ct);
        }

        try
        {
            var slug = WorkspaceFiles.Slugify(environment.Name, "environment");
            var path = Path.Combine(_directory, slug + Extension);
            for (var n = 2; _paths.Any(p => p.Key != environment.Id && IsSamePath(p.Value, path)); n++)
            {
                path = Path.Combine(_directory, $"{slug}-{n}{Extension}");
            }

            await WorkspaceFiles.WriteAtomicAsync(path, EnvironmentYamlSerializer.Serialize(environment), ct);
            if (_paths.TryGetValue(environment.Id, out var old) && !IsSamePath(old, path))
            {
                File.Delete(old);
            }

            _paths[environment.Id] = path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot write environment '{environment.Name}': {ex.Message}", ex);
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
