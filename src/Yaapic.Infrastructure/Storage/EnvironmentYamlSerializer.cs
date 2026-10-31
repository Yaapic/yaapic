using System.Text;
using VYaml.Parser;
using VYaml.Serialization;
using Yaapic.Core.Storage;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Reads and writes environment files in the format described in <c>docs/file-format.md</c>.</summary>
public static class EnvironmentYamlSerializer
{
    private const int CurrentVersion = 1;

    /// <summary>Serializes an environment to YAML with <c>\n</c> line endings and a single trailing newline.</summary>
    /// <param name="environment">The environment to serialize.</param>
    /// <returns>The YAML document.</returns>
    public static string Serialize(EnvironmentDefinition environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var dto = new EnvironmentFileDto
        {
            Version = CurrentVersion,
            Id = environment.Id.ToString("D"),
            Name = environment.Name,
            Variables = [.. environment.Variables.Select(v =>
                new KeyValueDto { Key = v.Key, Value = v.Value, Enabled = v.Enabled })],
        };
        var yaml = YamlSerializer.SerializeToString(dto).Replace("\r\n", "\n");
        return yaml.TrimEnd('\n') + "\n";
    }

    /// <summary>Parses an environment file.</summary>
    /// <param name="yaml">The YAML document.</param>
    /// <returns>The environment.</returns>
    /// <exception cref="StorageException">The document is malformed, has an unsupported version or invalid values.</exception>
    public static EnvironmentDefinition Deserialize(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        EnvironmentFileDto? dto;
        try
        {
            dto = YamlSerializer.Deserialize<EnvironmentFileDto>(Encoding.UTF8.GetBytes(yaml));
        }
        catch (Exception ex) when (ex is YamlSerializerException or YamlParserException)
        {
            throw new StorageException($"Invalid environment file: {ex.Message}", ex);
        }

        if (dto is null)
        {
            throw new StorageException("Invalid environment file: empty document");
        }

        if (dto.Version != CurrentVersion)
        {
            throw new StorageException($"Unsupported environment file version: {dto.Version}");
        }

        if (!Guid.TryParseExact(dto.Id, "D", out var id))
        {
            throw new StorageException($"Invalid environment id: '{dto.Id}'");
        }

        return new EnvironmentDefinition(
            id,
            string.IsNullOrWhiteSpace(dto.Name) ? "Untitled" : dto.Name,
            [.. (dto.Variables ?? []).Select(v => new KeyValueEntry(v.Key ?? "", v.Value ?? "", v.Enabled ?? true))]);
    }
}
