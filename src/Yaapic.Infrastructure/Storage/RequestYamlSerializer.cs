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
            Method = request.Method.ToText(),
            Url = request.Url,
            Params = [.. request.QueryParams.Select(ToDto)],
            Headers = [.. request.Headers.Select(ToDto)],
            Body = new BodyDto
            {
                Type = BodyTypeText(request.Body.Kind),
                ContentType = ContentTypeText(request.Body.ContentType),
                Text = request.Body.Text,
                Fields = [.. request.Body.Fields.Select(f => new FormFieldDto
                {
                    Key = f.Key,
                    Value = f.Value,
                    Enabled = f.Enabled,
                    Kind = f.Kind == FormFieldKind.File ? "file" : "text",
                })],
                File = request.Body.FilePath,
            },
            Auth = new AuthDto
            {
                Type = AuthTypeText(request.Auth.Kind),
                Username = request.Auth.Username,
                Password = request.Auth.Password,
                Token = request.Auth.Token,
                Key = request.Auth.Key,
                Value = request.Auth.Value,
                Location = request.Auth.Location == ApiKeyLocation.Query ? "query" : "header",
            },
            Settings = new SettingsDto
            {
                FollowRedirects = request.Settings.FollowRedirects,
                TimeoutSeconds = request.Settings.TimeoutSeconds,
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

        if (!HttpMethodKindExtensions.TryParse(dto.Method, out var method))
        {
            throw new StorageException($"Unknown method: '{dto.Method}'");
        }

        return new HttpRequestDefinition(
            id,
            string.IsNullOrWhiteSpace(dto.Name) ? "Untitled" : dto.Name,
            method,
            dto.Url ?? "",
            [.. (dto.Params ?? []).Select(FromDto)],
            [.. (dto.Headers ?? []).Select(FromDto)],
            ReadBody(dto.Body),
            ReadAuth(dto.Auth),
            ReadSettings(dto.Settings));
    }

    private static string BodyTypeText(BodyKind kind) => kind switch
    {
        BodyKind.Raw => "raw",
        BodyKind.UrlEncoded => "urlencoded",
        BodyKind.FormData => "form-data",
        BodyKind.Binary => "binary",
        _ => "none",
    };

    private static string ContentTypeText(RawContentType type) => type switch
    {
        RawContentType.Json => "json",
        RawContentType.Xml => "xml",
        RawContentType.Html => "html",
        RawContentType.JavaScript => "javascript",
        _ => "text",
    };

    private static string AuthTypeText(AuthKind kind) => kind switch
    {
        AuthKind.Basic => "basic",
        AuthKind.Bearer => "bearer",
        AuthKind.ApiKey => "apikey",
        _ => "none",
    };

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
            "urlencoded" => BodyKind.UrlEncoded,
            "form-data" => BodyKind.FormData,
            "binary" => BodyKind.Binary,
            _ => throw new StorageException($"Unknown body type: '{dto.Type}'"),
        };
        var contentType = dto.ContentType switch
        {
            null or "text" => RawContentType.Text,
            "json" => RawContentType.Json,
            "xml" => RawContentType.Xml,
            "html" => RawContentType.Html,
            "javascript" => RawContentType.JavaScript,
            _ => throw new StorageException($"Unknown body content type: '{dto.ContentType}'"),
        };
        var fields = (dto.Fields ?? []).Select(f => new FormField(
            f.Key ?? "",
            f.Value ?? "",
            f.Enabled ?? true,
            f.Kind switch
            {
                null or "text" => FormFieldKind.Text,
                "file" => FormFieldKind.File,
                _ => throw new StorageException($"Unknown field kind: '{f.Kind}'"),
            }));
        return new RequestBody(kind, contentType, dto.Text ?? "", [.. fields], dto.File ?? "");
    }

    private static RequestAuth ReadAuth(AuthDto? dto)
    {
        if (dto is null)
        {
            return RequestAuth.None;
        }

        var kind = dto.Type switch
        {
            null or "none" => AuthKind.None,
            "basic" => AuthKind.Basic,
            "bearer" => AuthKind.Bearer,
            "apikey" => AuthKind.ApiKey,
            _ => throw new StorageException($"Unknown auth type: '{dto.Type}'"),
        };
        var location = dto.Location switch
        {
            null or "header" => ApiKeyLocation.Header,
            "query" => ApiKeyLocation.Query,
            _ => throw new StorageException($"Unknown auth location: '{dto.Location}'"),
        };
        return new RequestAuth(
            kind, dto.Username ?? "", dto.Password ?? "", dto.Token ?? "", dto.Key ?? "", dto.Value ?? "", location);
    }

    private static RequestSettings ReadSettings(SettingsDto? dto)
    {
        var timeout = dto?.TimeoutSeconds ?? 0;
        if (timeout < 0)
        {
            throw new StorageException($"Invalid timeoutSeconds: {timeout}");
        }

        return new RequestSettings(dto?.FollowRedirects ?? true, timeout);
    }
}
