using System.Text;
using Yaapic.Core.Import;
using Yaapic.Core.LoadTesting;
using Yaapic.Core.Runner;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests.LoadTesting;

public class LoadTestReportJsonTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 24, 10, 0, 5, TimeSpan.Zero);

    private static LoadTestResult Sample(bool canceled = false, bool truncated = false)
    {
        var plan = new LoadTestPlan(
            "My Load",
            [new RunItem(HttpRequestDefinition.Create("Get \"users\"") with { Url = "{{baseUrl}}/users" }, "Col/Sub")],
            2, TimeSpan.FromSeconds(10), LoadProfile.RampUp, TimeSpan.FromSeconds(4), 250, []);
        var stats = new LoadStats(10, 1, 0.1, 5, 12.345, 40, 10, 30, 35, 40, 1.256);
        return new LoadTestResult(plan, Start, TimeSpan.FromMilliseconds(10002), canceled, stats, [stats],
            [new LoadBucket(0, 4, 1, 11.04, 2)], ["boom"], truncated);
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [Fact]
    public void Write_produces_the_exact_report()
    {
        const string expected = """
            {
              "schema": "yaapic-loadtest/1",
              "name": "My Load",
              "startedAt": "2026-11-24T10:00:05.000Z",
              "durationMs": 10002,
              "canceled": false,
              "plan": {
                "virtualUsers": 2,
                "durationSeconds": 10,
                "profile": "rampUp",
                "rampUpSeconds": 4,
                "thinkTimeMs": 250,
                "dataRows": 0,
                "requests": [
                  {
                    "name": "Get \"users\"",
                    "path": "Col/Sub",
                    "method": "GET",
                    "url": "{{baseUrl}}/users"
                  }
                ]
              },
              "overall": {
                "count": 10,
                "errors": 1,
                "errorRate": 0.1,
                "minMs": 5,
                "averageMs": 12.3,
                "maxMs": 40,
                "p50Ms": 10,
                "p90Ms": 30,
                "p95Ms": 35,
                "p99Ms": 40,
                "throughput": 1.26
              },
              "requests": [
                {
                  "name": "Get \"users\"",
                  "count": 10,
                  "errors": 1,
                  "errorRate": 0.1,
                  "minMs": 5,
                  "averageMs": 12.3,
                  "maxMs": 40,
                  "p50Ms": 10,
                  "p90Ms": 30,
                  "p95Ms": 35,
                  "p99Ms": 40,
                  "throughput": 1.26
                }
              ],
              "buckets": [
                {
                  "second": 0,
                  "requests": 4,
                  "errors": 1,
                  "averageMs": 11,
                  "activeUsers": 2
                }
              ],
              "errors": [
                "boom"
              ]
            }
            """;
        Assert.Equal(expected, Text(LoadTestReportJson.Write(Sample())));
    }

    [Fact]
    public void Truncation_note_and_canceled_flag_are_written()
    {
        var text = Text(LoadTestReportJson.Write(Sample(canceled: true, truncated: true)));
        Assert.Contains("\"canceled\": true", text, StringComparison.Ordinal);
        Assert.Contains("\"note\": \"percentiles computed on the first 2000000 samples\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"note\"", Text(LoadTestReportJson.Write(Sample())), StringComparison.Ordinal);
    }

    [Fact]
    public void Report_parses_back_and_has_no_bom()
    {
        var bytes = LoadTestReportJson.Write(Sample());
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.NotNull(DataNodeJson.Parse(Text(bytes)));
    }

    [Fact]
    public void SuggestedFileName_uses_slug_and_utc_start() =>
        Assert.Equal("my-load-load-20261124-100005.json", LoadTestReportJson.SuggestedFileName(Sample()));
}
