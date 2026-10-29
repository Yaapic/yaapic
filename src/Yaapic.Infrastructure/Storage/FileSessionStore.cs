using System.Text;
using VYaml.Parser;
using VYaml.Serialization;
using Yaapic.Core.Storage;
using Yaapic.Domain.Workspace;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Stores the UI session in <c>.yaapic/session.yaml</c> of a workspace.</summary>
/// <param name="workspaceRoot">The workspace root directory.</param>
public sealed class FileSessionStore(string workspaceRoot) : ISessionStore
{
    private const int CurrentVersion = 1;
    private readonly string _path = Path.Combine(workspaceRoot, ".yaapic", "session.yaml");

    /// <inheritdoc />
    public async Task<SessionState> LoadAsync(CancellationToken ct)
    {
        SessionFileDto? dto;
        try
        {
            if (!File.Exists(_path))
            {
                return SessionState.Empty;
            }

            dto = YamlSerializer.Deserialize<SessionFileDto>(Encoding.UTF8.GetBytes(await File.ReadAllTextAsync(_path, ct)));
        }
        catch (Exception ex) when (ex is YamlSerializerException or YamlParserException or IOException or UnauthorizedAccessException)
        {
            return SessionState.Empty;
        }

        if (dto is null || dto.Version != CurrentVersion)
        {
            return SessionState.Empty;
        }

        return new SessionState(
            [.. (dto.OpenRequests ?? []).Select(ParseGuid).OfType<Guid>()],
            ParseGuid(dto.ActiveRequest),
            ParseGuid(dto.ActiveEnvironment));
    }

    /// <inheritdoc />
    public async Task SaveAsync(SessionState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        var dto = new SessionFileDto
        {
            Version = CurrentVersion,
            OpenRequests = [.. state.OpenRequestIds.Select(g => g.ToString("D"))],
            ActiveRequest = state.ActiveRequestId?.ToString("D") ?? "",
            ActiveEnvironment = state.ActiveEnvironmentId?.ToString("D") ?? "",
        };
        var yaml = YamlSerializer.SerializeToString(dto).Replace("\r\n", "\n").TrimEnd('\n') + "\n";
        try
        {
            await WorkspaceFiles.WriteAtomicAsync(_path, yaml, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot write session: {ex.Message}", ex);
        }
    }

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParseExact(value, "D", out var id) ? id : null;
}
