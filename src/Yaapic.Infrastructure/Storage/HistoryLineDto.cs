using System.Text.Json.Serialization;

namespace Yaapic.Infrastructure.Storage;

/// <summary>One line of <c>history.jsonl</c>.</summary>
internal sealed class HistoryLineDto
{
    public string? Id { get; set; }

    public string? At { get; set; }

    public string? Request { get; set; }

    public int? StatusCode { get; set; }

    public string? StatusText { get; set; }

    public long DurationMs { get; set; }

    public long SizeBytes { get; set; }

    public string? FailureKind { get; set; }

    public string? FailureMessage { get; set; }
}

/// <summary>Source-generated JSON metadata for <see cref="HistoryLineDto"/> (Native AOT, no reflection).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(HistoryLineDto))]
internal partial class HistoryJsonContext : JsonSerializerContext;
