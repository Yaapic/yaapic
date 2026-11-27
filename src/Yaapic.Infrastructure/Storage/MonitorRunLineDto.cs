using System.Text.Json.Serialization;

namespace Yaapic.Infrastructure.Storage;

/// <summary>One line of <c>monitor-runs.jsonl</c>.</summary>
internal sealed class MonitorRunLineDto
{
    public string? MonitorId { get; set; }

    public string? At { get; set; }

    public int DurationMs { get; set; }

    public string? Status { get; set; }

    public int Requests { get; set; }

    public int FailedRequests { get; set; }

    public int TestsPassed { get; set; }

    public int TestsFailed { get; set; }

    public string? Message { get; set; }

    public List<MonitorFailureDto>? Failures { get; set; }
}

/// <summary>One failing request inside a <see cref="MonitorRunLineDto"/>.</summary>
internal sealed class MonitorFailureDto
{
    public string? Request { get; set; }

    public string? Outcome { get; set; }

    public int? Status { get; set; }

    public string? Detail { get; set; }
}

/// <summary>Source-generated JSON metadata for <see cref="MonitorRunLineDto"/> (Native AOT, no reflection).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(MonitorRunLineDto))]
internal partial class MonitorRunJsonContext : JsonSerializerContext;
