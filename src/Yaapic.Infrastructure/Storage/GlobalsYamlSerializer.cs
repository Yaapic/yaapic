using System.Text;
using VYaml.Parser;
using VYaml.Serialization;
using Yaapic.Core.Storage;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Reads and writes the globals file in the format described in <c>docs/file-format.md</c>.</summary>
public static class GlobalsYamlSerializer
{
    private const int CurrentVersion = 1;

    /// <summary>Serializes the global variables to YAML with <c>\n</c> line endings and a single trailing newline.</summary>
    /// <param name="variables">The variables to serialize.</param>
    /// <returns>The YAML document.</returns>
    public static string Serialize(IReadOnlyList<KeyValueEntry> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        var dto = new GlobalsFileDto
        {
            Version = CurrentVersion,
            Variables = [.. variables.Select(v => new KeyValueDto { Key = v.Key, Value = v.Value, Enabled = v.Enabled })],
        };
        var yaml = YamlSerializer.SerializeToString(dto).Replace("\r\n", "\n");
        return yaml.TrimEnd('\n') + "\n";
    }

    /// <summary>Parses a globals file.</summary>
    /// <param name="yaml">The YAML document.</param>
    /// <returns>The variables in order.</returns>
    /// <exception cref="StorageException">The document is malformed or has an unsupported version.</exception>
    public static IReadOnlyList<KeyValueEntry> Deserialize(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        GlobalsFileDto? dto;
        try
        {
            dto = YamlSerializer.Deserialize<GlobalsFileDto>(Encoding.UTF8.GetBytes(yaml));
        }
        catch (Exception ex) when (ex is YamlSerializerException or YamlParserException or YamlTokenizerException)
        {
            throw new StorageException($"Invalid globals file: {ex.Message}", ex);
        }

        if (dto is null)
        {
            throw new StorageException("Invalid globals file: empty document");
        }

        if (dto.Version != CurrentVersion)
        {
            throw new StorageException($"Unsupported globals file version: {dto.Version}");
        }

        return [.. (dto.Variables ?? []).Select(v => new KeyValueEntry(v.Key ?? "", v.Value ?? "", v.Enabled ?? true))];
    }
}
