using System.Text;
using VYaml.Parser;
using VYaml.Serialization;
using Yaapic.Core.Storage;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Reads and writes request files in the format described in <c>docs/file-format.md</c>.</summary>
public static class RequestYamlSerializer
{
    private const int CurrentVersion = 1;

    /// <summary>Serializes a request to YAML with <c>\n</c> line endings and a single trailing newline.</summary>
    /// <param name="request">The request to serialize.</param>
    /// <returns>The YAML document.</returns>
    public static string Serialize(HttpRequestDefinition request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dto = new RequestFileDto
        {
            Version = CurrentVersion,
            Id = request.Id.ToString("D"),
            Name = request.Name,
            Method = request.Method == HttpMethodKind.Post ? "POST" : "GET",
            Url = request.Url,
            Params = [.. request.QueryParams.Select(ToDto)],
            Headers = [.. request.Headers.Select(ToDto)],
            Body = new BodyDto
            {
                Type = request.Body.Kind == BodyKind.Raw ? "raw" : "none",
                ContentType = request.Body.ContentType switch
                {
                    RawContentType.Json => "json",
                    RawContentType.Xml => "xml",
                    _ => "text",
                },
                Text = request.Body.Text,
            },
        };
        var yaml = YamlSerializer.SerializeToString(dto).Replace("\r\n", "\n");
        return yaml.TrimEnd('\n') + "\n";
    }

    /// <summary>Parses a request file.</summary>
    /// <param name="yaml">The YAML document.</param>
    /// <returns>The request.</returns>
    /// <exception cref="StorageException">The document is malformed, has an unsupported version or invalid values.</exception>
    public static HttpRequestDefinition Deserialize(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        RequestFileDto? dto;
        try
        {
            dto = YamlSerializer.Deserialize<RequestFileDto>(Encoding.UTF8.GetBytes(yaml));
        }
        catch (Exception ex) when (ex is YamlSerializerException or YamlParserException)
        {
            throw new StorageException($"Invalid request file: {ex.Message}", ex);
        }

        if (dto is null)
        {
            throw new StorageException("Invalid request file: empty document");
        }

        if (dto.Version != CurrentVersion)
        {
            throw new StorageException($"Unsupported request file version: {dto.Version}");
        }

        if (!Guid.TryParseExact(dto.Id, "D", out var id))
        {
            throw new StorageException($"Invalid request id: '{dto.Id}'");
        }

        var method = dto.Method switch
        {
            "GET" => HttpMethodKind.Get,
            "POST" => HttpMethodKind.Post,
            _ => throw new StorageException($"Unknown method: '{dto.Method}'"),
        };

        return new HttpRequestDefinition(
            id,
            string.IsNullOrWhiteSpace(dto.Name) ? "Untitled" : dto.Name,
            method,
            dto.Url ?? "",
            [.. (dto.Params ?? []).Select(FromDto)],
            [.. (dto.Headers ?? []).Select(FromDto)],
            ReadBody(dto.Body));
    }

    private static KeyValueDto ToDto(KeyValueEntry entry) =>
        new() { Key = entry.Key, Value = entry.Value, Enabled = entry.Enabled };

    private static KeyValueEntry FromDto(KeyValueDto dto) => new(dto.Key ?? "", dto.Value ?? "", dto.Enabled ?? true);

    private static RequestBody ReadBody(BodyDto? dto)
    {
        if (dto is null)
        {
            return RequestBody.None;
        }

        var kind = dto.Type switch
        {
            null or "none" => BodyKind.None,
            "raw" => BodyKind.Raw,
            _ => throw new StorageException($"Unknown body type: '{dto.Type}'"),
        };
        var contentType = dto.ContentType switch
        {
            null or "text" => RawContentType.Text,
            "json" => RawContentType.Json,
            "xml" => RawContentType.Xml,
            _ => throw new StorageException($"Unknown body content type: '{dto.ContentType}'"),
        };
        return new RequestBody(kind, contentType, dto.Text ?? "");
    }
}
