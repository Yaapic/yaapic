using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Yaapic.Core.Export;

namespace Yaapic.Core.LoadTesting;

/// <summary>Writes a <see cref="LoadTestResult"/> as a deterministic JSON report (ADR 0019).</summary>
public static class LoadTestReportJson
{
    /// <summary>Writes the report as UTF-8 JSON without a BOM, two-space indentation and <c>\n</c> line ends.</summary>
    /// <param name="result">The load-test result.</param>
    /// <returns>The JSON bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public static byte[] Write(LoadTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            WriteRoot(w, result);
        }

        return stream.ToArray();
    }

    /// <summary>Builds the suggested file name: <c>{slug}-load-{yyyyMMdd-HHmmss}.json</c> (UTC start).</summary>
    /// <param name="result">The load-test result.</param>
    /// <returns>The file name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public static string SuggestedFileName(LoadTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var stamp = result.StartedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"{ExportPackager.SafeFileName(result.Plan.Name)}-load-{stamp}.json";
    }

    private static void WriteRoot(Utf8JsonWriter w, LoadTestResult r)
    {
        var plan = r.Plan;
        w.WriteStartObject();
        w.WriteString("schema", "yaapic-loadtest/1");
        w.WriteString("name", plan.Name);
        w.WriteString("startedAt", r.StartedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
        w.WriteNumber("durationMs", Ms(r.Duration));
        w.WriteBoolean("canceled", r.Canceled);
        w.WriteStartObject("plan");
        w.WriteNumber("virtualUsers", plan.VirtualUsers);
        w.WriteNumber("durationSeconds", Ms(plan.Duration) / 1000);
        w.WriteString("profile", plan.Profile == LoadProfile.Fixed ? "fixed" : "rampUp");
        w.WriteNumber("rampUpSeconds", Ms(plan.RampUp) / 1000);
        w.WriteNumber("thinkTimeMs", plan.ThinkTimeMs);
        w.WriteNumber("dataRows", plan.Data.Count);
        w.WriteStartArray("requests");
        foreach (var item in plan.Items)
        {
            w.WriteStartObject();
            w.WriteString("name", item.Request.Name);
            w.WriteString("path", item.Path);
            w.WriteString("method", item.Request.Method.ToString().ToUpperInvariant());
            w.WriteString("url", item.Request.Url);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
        w.WritePropertyName("overall");
        WriteStats(w, r.Overall, null);
        w.WriteStartArray("requests");
        for (int i = 0; i < r.PerRequest.Count; i++)
        {
            WriteStats(w, r.PerRequest[i], i < plan.Items.Count ? plan.Items[i].Request.Name : string.Empty);
        }

        w.WriteEndArray();
        w.WriteStartArray("buckets");
        foreach (var b in r.Buckets)
        {
            w.WriteStartObject();
            w.WriteNumber("second", b.Second);
            w.WriteNumber("requests", b.Requests);
            w.WriteNumber("errors", b.Errors);
            w.WriteNumber("averageMs", Math.Round(b.AverageMs, 1, MidpointRounding.AwayFromZero));
            w.WriteNumber("activeUsers", b.ActiveUsers);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteStartArray("errors");
        foreach (var e in r.ErrorMessages) w.WriteStringValue(e);
        w.WriteEndArray();
        if (r.SamplesTruncated) w.WriteString("note", "percentiles computed on the first 2000000 samples");
        w.WriteEndObject();
    }

    private static void WriteStats(Utf8JsonWriter w, LoadStats s, string? name)
    {
        w.WriteStartObject();
        if (name is not null) w.WriteString("name", name);
        w.WriteNumber("count", s.Count);
        w.WriteNumber("errors", s.Errors);
        w.WriteNumber("errorRate", Math.Round(s.ErrorRate, 4, MidpointRounding.AwayFromZero));
        w.WriteNumber("minMs", s.MinMs);
        w.WriteNumber("averageMs", Math.Round(s.AverageMs, 1, MidpointRounding.AwayFromZero));
        w.WriteNumber("maxMs", s.MaxMs);
        w.WriteNumber("p50Ms", s.P50Ms);
        w.WriteNumber("p90Ms", s.P90Ms);
        w.WriteNumber("p95Ms", s.P95Ms);
        w.WriteNumber("p99Ms", s.P99Ms);
        w.WriteNumber("throughput", Math.Round(s.Throughput, 2, MidpointRounding.AwayFromZero));
        w.WriteEndObject();
    }

    private static long Ms(TimeSpan span) => (long)Math.Round(span.TotalMilliseconds, MidpointRounding.AwayFromZero);
}
