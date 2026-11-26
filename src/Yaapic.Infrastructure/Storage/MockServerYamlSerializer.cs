using System.Text;
using VYaml.Parser;
using VYaml.Serialization;
using Yaapic.Core.Storage;
using Yaapic.Domain.Mocks;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Reads and writes mock server files in the format described in <c>docs/file-format.md</c>.</summary>
public static class MockServerYamlSerializer
{
    private const int CurrentVersion = 1;

    /// <summary>Serializes a mock server to YAML with <c>\n</c> line endings and a single trailing newline.</summary>
    /// <param name="server">The server to serialize.</param>
    /// <returns>The YAML document.</returns>
    public static string Serialize(MockServerDefinition server)
    {
        ArgumentNullException.ThrowIfNull(server);
        var dto = new MockServerFileDto
        {
            Version = CurrentVersion,
            Id = server.Id.ToString("D"),
            Name = server.Name,
            Port = server.Port,
            Cors = server.Cors,
            Routes = [.. server.Routes.Select(r => new MockRouteFileDto
            {
                Id = r.Id.ToString("D"),
                Name = r.Name,
                Enabled = r.Enabled,
                Method = r.Method,
                Path = r.Path,
                Status = r.Status,
                Headers = [.. r.Headers.Select(h => KeyValueMapper.ToDto(h, false))],
                Body = new ScriptSourceText(r.Body),
                DelayMs = r.DelayMs,
            })],
        };
        var yaml = YamlSerializer.SerializeToString(dto, ScriptSourceFormatter.Options).Replace("\r\n", "\n");
        return yaml.Replace("\n    secret: null", "").TrimEnd('\n') + "\n";
    }

    /// <summary>Parses a mock server file.</summary>
    /// <param name="yaml">The YAML document.</param>
    /// <returns>The server.</returns>
    /// <exception cref="StorageException">The document is malformed, has an unsupported version or invalid values.</exception>
    public static MockServerDefinition Deserialize(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        MockServerFileDto? dto;
        try
        {
            dto = YamlSerializer.Deserialize<MockServerFileDto>(Encoding.UTF8.GetBytes(yaml), ScriptSourceFormatter.Options);
        }
        catch (Exception ex) when (ex is YamlSerializerException or YamlParserException or YamlTokenizerException)
        {
            throw new StorageException($"Invalid mock server file: {ex.Message}", ex);
        }

        if (dto is null)
        {
            throw new StorageException("Invalid mock server file: empty document");
        }

        if (dto.Version != CurrentVersion)
        {
            throw new StorageException($"Unsupported mock server file version: {dto.Version}");
        }

        return new MockServerDefinition(
            ParseId(dto.Id, "mock server"),
            string.IsNullOrWhiteSpace(dto.Name) ? "Untitled" : dto.Name,
            dto.Port ?? 3000,
            dto.Cors ?? true,
            [.. (dto.Routes ?? []).Select(r => new MockRoute(
                ParseId(r.Id, "mock route"),
                r.Name ?? "",
                r.Enabled ?? true,
                r.Method ?? "GET",
                r.Path ?? "/",
                r.Status ?? 200,
                [.. (r.Headers ?? []).Select(h => KeyValueMapper.FromDto(h, false))],
                r.Body?.Value ?? "",
                r.DelayMs ?? 0))]);
    }

    private static Guid ParseId(string? text, string what) =>
        Guid.TryParseExact(text, "D", out var id) ? id : throw new StorageException($"Invalid {what} id: '{text}'");
}
