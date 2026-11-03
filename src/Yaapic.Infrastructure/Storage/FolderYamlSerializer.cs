using System.Text;
using VYaml.Parser;
using VYaml.Serialization;
using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Reads and writes folder files in the format described in <c>docs/file-format.md</c>.</summary>
public static class FolderYamlSerializer
{
    private const int CurrentVersion = 1;

    /// <summary>Serializes a folder to YAML with <c>\n</c> line endings and a single trailing newline.</summary>
    /// <param name="folder">The folder to serialize.</param>
    /// <returns>The YAML document.</returns>
    public static string Serialize(FolderDefinition folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var dto = new FolderFileDto
        {
            Version = CurrentVersion,
            Id = folder.Id.ToString("D"),
            Name = folder.Name,
            Description = folder.Description,
            Variables = [.. folder.Variables.Select(v =>
                new KeyValueDto { Key = v.Key, Value = v.Value, Enabled = v.Enabled })],
        };
        var yaml = YamlSerializer.SerializeToString(dto).Replace("\r\n", "\n");
        return yaml.TrimEnd('\n') + "\n";
    }

    /// <summary>Parses a folder file.</summary>
    /// <param name="yaml">The YAML document.</param>
    /// <returns>The folder.</returns>
    /// <exception cref="StorageException">The document is malformed, has an unsupported version or invalid values.</exception>
    public static FolderDefinition Deserialize(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        FolderFileDto? dto;
        try
        {
            dto = YamlSerializer.Deserialize<FolderFileDto>(Encoding.UTF8.GetBytes(yaml));
        }
        catch (Exception ex) when (ex is YamlSerializerException or YamlParserException)
        {
            throw new StorageException($"Invalid folder file: {ex.Message}", ex);
        }

        if (dto is null)
        {
            throw new StorageException("Invalid folder file: empty document");
        }

        if (dto.Version != CurrentVersion)
        {
            throw new StorageException($"Unsupported folder file version: {dto.Version}");
        }

        if (!Guid.TryParseExact(dto.Id, "D", out var id))
        {
            throw new StorageException($"Invalid folder id: '{dto.Id}'");
        }

        return new FolderDefinition(
            id,
            string.IsNullOrWhiteSpace(dto.Name) ? "Untitled" : dto.Name,
            dto.Description ?? "",
            [.. (dto.Variables ?? []).Select(v => new KeyValueEntry(v.Key ?? "", v.Value ?? "", v.Enabled ?? true))]);
    }
}
