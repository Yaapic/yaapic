using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Yaapic.Core.Export;

namespace Yaapic.Core.Runner;

/// <summary>Writes a <see cref="RunResult"/> as a deterministic JSON report (ADR 0018).</summary>
public static class RunReportJson
{
    /// <summary>Writes the report as UTF-8 JSON without a BOM, two-space indentation and <c>\n</c> line ends.</summary>
    /// <param name="result">The run result.</param>
    /// <returns>The JSON bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public static byte[] Write(RunResult result)
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

    /// <summary>Builds the suggested file name: <c>{slug}-run-{yyyyMMdd-HHmmss}.json</c> (UTC start).</summary>
    /// <param name="result">The run result.</param>
    /// <returns>The file name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public static string SuggestedFileName(RunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var stamp = result.StartedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"{ExportPackager.SafeFileName(result.Name)}-run-{stamp}.json";
    }

    private static void WriteRoot(Utf8JsonWriter w, RunResult r)
    {
        var s = RunSummary.From(r);
        w.WriteStartObject();
        w.WriteString("schema", "yaapic-run/1");
        w.WriteString("name", r.Name);
        w.WriteString("startedAt", Time(r.StartedAt));
        w.WriteNumber("durationMs", Ms(r.Duration));
        w.WriteString("status", Camel(r.Status.ToString()));
        Str(w, "error", r.Error);
        w.WriteStartObject("options");
        w.WriteNumber("iterations", r.Options.Iterations);
        w.WriteNumber("delayMs", r.Options.DelayMs);
        w.WriteBoolean("stopOnFailure", r.Options.StopOnFailure);
        w.WriteBoolean("persistVariables", r.Options.PersistVariables);
        w.WriteNumber("dataRows", r.DataRows);
        w.WriteEndObject();
        w.WriteStartObject("summary");
        w.WriteNumber("sent", s.Sent);
        w.WriteNumber("skipped", s.Skipped);
        w.WriteNumber("errors", s.Errors);
        w.WriteNumber("failedRequests", s.FailedRequests);
        w.WriteNumber("testsPassed", s.TestsPassed);
        w.WriteNumber("testsFailed", s.TestsFailed);
        w.WriteNumber("averageResponseMs", Ms(s.AverageResponseTime));
        w.WriteEndObject();
        w.WriteStartArray("iterations");
        foreach (var it in r.Iterations)
        {
            w.WriteStartObject();
            w.WriteNumber("index", it.Index);
            w.WriteStartArray("requests");
            foreach (var q in it.Requests) WriteRequest(w, q);
            w.WriteEndArray();
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WriteRequest(Utf8JsonWriter w, RunRequestResult q)
    {
        w.WriteStartObject();
        w.WriteString("id", q.RequestId.ToString("D", CultureInfo.InvariantCulture));
        w.WriteString("name", q.Name);
        w.WriteString("path", q.Path);
        w.WriteString("method", q.Method.ToString().ToUpperInvariant());
        w.WriteString("url", q.Url);
        w.WriteString("outcome", Camel(q.Outcome.ToString()));
        if (q.StatusCode is { } code) w.WriteNumber("status", code);
        else w.WriteNull("status");
        w.WriteNumber("durationMs", Ms(q.Duration));
        w.WriteNumber("sizeBytes", q.SizeBytes);
        Str(w, "error", q.Error);
        w.WriteStartArray("tests");
        foreach (var t in q.Tests)
        {
            w.WriteStartObject();
            w.WriteString("name", t.Name);
            w.WriteBoolean("passed", t.Passed);
            Str(w, "message", t.Message);
            w.WriteNumber("durationMs", Ms(t.Duration));
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteStartArray("console");
        foreach (var c in q.Console)
        {
            w.WriteStartObject();
            w.WriteString("at", Time(c.At));
            w.WriteString("level", Camel(c.Level.ToString()));
            w.WriteString("source", Camel(c.Source.ToString()));
            w.WriteString("message", c.Message);
            Str(w, "origin", c.Origin);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void Str(Utf8JsonWriter w, string key, string? value)
    {
        if (value is null) w.WriteNull(key);
        else w.WriteString(key, value);
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static long Ms(TimeSpan span) => (long)Math.Round(span.TotalMilliseconds, MidpointRounding.AwayFromZero);

    private static string Time(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
