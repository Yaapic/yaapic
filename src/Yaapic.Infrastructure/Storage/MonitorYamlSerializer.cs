using System.Text;
using VYaml.Parser;
using VYaml.Serialization;
using Yaapic.Core.Storage;
using Yaapic.Domain.Monitors;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Reads and writes monitor files in the format described in <c>docs/file-format.md</c>.</summary>
public static class MonitorYamlSerializer
{
    private const int CurrentVersion = 1;

    /// <summary>Serializes a monitor to YAML with <c>\n</c> line endings and a single trailing newline.</summary>
    /// <param name="monitor">The monitor to serialize.</param>
    /// <returns>The YAML document.</returns>
    public static string Serialize(MonitorDefinition monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        var dto = new MonitorFileDto
        {
            Version = CurrentVersion,
            Id = monitor.Id.ToString("D"),
            Name = monitor.Name,
            FolderId = monitor.FolderId.ToString("D"),
            EnvironmentId = monitor.EnvironmentId?.ToString("D"),
            IntervalMinutes = monitor.IntervalMinutes,
            Enabled = monitor.Enabled,
        };
        var yaml = YamlSerializer.SerializeToString(dto).Replace("\r\n", "\n");
        return yaml.Replace("\nenvironmentId: null", "").TrimEnd('\n') + "\n";
    }

    /// <summary>Parses a monitor file.</summary>
    /// <param name="yaml">The YAML document.</param>
    /// <param name="intervalAdjusted">Whether the stored interval was not allowed and was raised to the nearest allowed value.</param>
    /// <returns>The monitor.</returns>
    /// <exception cref="StorageException">The document is malformed, has an unsupported version or invalid ids.</exception>
    public static MonitorDefinition Deserialize(string yaml, out bool intervalAdjusted)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        MonitorFileDto? dto;
        try
        {
            dto = YamlSerializer.Deserialize<MonitorFileDto>(Encoding.UTF8.GetBytes(yaml));
        }
        catch (Exception ex) when (ex is YamlSerializerException or YamlParserException or YamlTokenizerException)
        {
            throw new StorageException($"Invalid monitor file: {ex.Message}", ex);
        }

        if (dto is null)
        {
            throw new StorageException("Invalid monitor file: empty document");
        }

        if (dto.Version != CurrentVersion)
        {
            throw new StorageException($"Unsupported monitor file version: {dto.Version}");
        }

        var stored = dto.IntervalMinutes ?? 60;
        var interval = MonitorDefinition.AllowedIntervals.FirstOrDefault(i => i >= stored, 1440);
        intervalAdjusted = interval != stored;
        return new MonitorDefinition(
            ParseId(dto.Id, "monitor"),
            string.IsNullOrWhiteSpace(dto.Name) ? "Untitled" : dto.Name,
            ParseId(dto.FolderId, "monitor folder"),
            string.IsNullOrEmpty(dto.EnvironmentId) ? null : ParseId(dto.EnvironmentId, "monitor environment"),
            interval,
            dto.Enabled ?? true);
    }

    private static Guid ParseId(string? text, string what) =>
        Guid.TryParseExact(text, "D", out var id) ? id : throw new StorageException($"Invalid {what} id: '{text}'");
}
