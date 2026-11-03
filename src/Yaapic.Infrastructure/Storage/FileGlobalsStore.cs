using Yaapic.Core.Storage;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Stores the global variables in <c>globals.yaml</c> at the root of a workspace.</summary>
/// <param name="workspaceRoot">The workspace root directory.</param>
public sealed class FileGlobalsStore(string workspaceRoot) : IGlobalsStore
{
    private readonly string _path = Path.Combine(workspaceRoot, "globals.yaml");

    /// <inheritdoc />
    public async Task<IReadOnlyList<KeyValueEntry>> LoadAsync(CancellationToken ct)
    {
        string yaml;
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            yaml = await File.ReadAllTextAsync(_path, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot read '{_path}': {ex.Message}", ex);
        }

        return GlobalsYamlSerializer.Deserialize(yaml);
    }

    /// <inheritdoc />
    public async Task SaveAsync(IReadOnlyList<KeyValueEntry> variables, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(variables);
        try
        {
            await WorkspaceFiles.WriteAtomicAsync(_path, GlobalsYamlSerializer.Serialize(variables), ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot write '{_path}': {ex.Message}", ex);
        }
    }
}
