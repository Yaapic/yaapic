using System.Text.Json.Serialization;

namespace Yaapic.Scripting;

/// <summary>Arguments of the <c>var.*</c> ops; <c>scope</c> is <c>layered|local|environment|collection|globals</c>. For <c>var.replace</c>, <c>value</c> is the template.</summary>
internal sealed record VarArgs(string Scope, string? Name, string? Value);

/// <summary>A header or form row; <c>enabled</c> is an addition to the card so disabled rows survive a get/set round trip.</summary>
internal sealed record KeyValueDto(string Key, string Value, bool Enabled = true);

/// <summary>The request as exchanged by <c>req.get</c>, <c>req.set</c> and <c>send</c>.</summary>
internal sealed record RequestDto(
    string Method,
    string Url,
    List<KeyValueDto> Headers,
    string BodyMode,
    string BodyText,
    string BodyContentType,
    List<KeyValueDto> UrlEncoded);

/// <summary>A cookie of the current response.</summary>
internal sealed record CookieDto(
    string Name,
    string Value,
    string? Domain,
    string? Path,
    DateTimeOffset? Expires,
    bool HttpOnly,
    bool Secure,
    string? SameSite);

/// <summary>The response as returned by <c>res.get</c> and <c>send</c>.</summary>
internal sealed record ResponseDto(
    int Code,
    string Status,
    List<KeyValueDto> Headers,
    string Text,
    long Size,
    long Time,
    string Url,
    List<CookieDto> Cookies);

/// <summary>The data behind <c>pm.info</c>.</summary>
internal sealed record InfoDto(string EventName, string RequestName, string RequestId, string Origin);

/// <summary>Arguments of the <c>console</c> op; <c>level</c> is <c>log|info|warn|error|debug</c>.</summary>
internal sealed record ConsoleArgs(string Level, string Text);

/// <summary>Arguments of the <c>test</c> op.</summary>
internal sealed record TestArgs(string Name, bool Passed, string? Message, double Ms);

/// <summary>The result of the <c>send</c> op: exactly one of the two members is set.</summary>
internal sealed record SendResultDto(ResponseDto? Response, string? Error);

/// <summary>Source-generated JSON metadata for the bridge (no reflection, Native AOT).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(VarArgs))]
[JsonSerializable(typeof(RequestDto))]
[JsonSerializable(typeof(ResponseDto))]
[JsonSerializable(typeof(InfoDto))]
[JsonSerializable(typeof(ConsoleArgs))]
[JsonSerializable(typeof(TestArgs))]
[JsonSerializable(typeof(SendResultDto))]
[JsonSerializable(typeof(List<KeyValueDto>))]
[JsonSerializable(typeof(List<CookieDto>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
internal sealed partial class HostJsonContext : JsonSerializerContext;
