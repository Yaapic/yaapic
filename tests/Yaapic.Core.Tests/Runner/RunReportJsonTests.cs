using System.Text;
using Yaapic.Core.Import;
using Yaapic.Core.Runner;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using TestResult = Yaapic.Domain.Scripting.TestResult;

namespace Yaapic.Core.Tests.Runner;

public class RunReportJsonTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    private static RunResult Sample(RunStatus status = RunStatus.Completed, string? error = null)
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var passed = new RunRequestResult(id, "Get \"users\"", "Col/Sub", HttpMethodKind.Get, "{{baseUrl}}/users",
            RunRequestOutcome.Passed, 200, TimeSpan.FromMilliseconds(40.5), 512,
            [new TestResult("ok", true, null, TimeSpan.FromMilliseconds(1), "t")],
            [new ConsoleEntry(Start, ConsoleLevel.Info, ConsoleSource.Script, "hi", "pre")], null);
        var skipped = new RunRequestResult(id, "b", "Col", HttpMethodKind.Post, "u", RunRequestOutcome.Skipped, null,
            TimeSpan.Zero, 0, [], [], "boom");
        return new RunResult("My Run", Start, TimeSpan.FromMilliseconds(1234), status, error,
            new RunOptions(2, 5, true, false), 3, [new RunIterationResult(0, [passed, skipped])]);
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [Fact]
    public void Write_produces_the_exact_report()
    {
        const string expected = """
            {
              "schema": "yaapic-run/1",
              "name": "My Run",
              "startedAt": "2026-11-24T10:00:00.000Z",
              "durationMs": 1234,
              "status": "completed",
              "error": null,
              "options": {
                "iterations": 2,
                "delayMs": 5,
                "stopOnFailure": true,
                "persistVariables": false,
                "dataRows": 3
              },
              "summary": {
                "sent": 1,
                "skipped": 1,
                "errors": 0,
                "failedRequests": 0,
                "testsPassed": 1,
                "testsFailed": 0,
                "averageResponseMs": 41
              },
              "iterations": [
                {
                  "index": 0,
                  "requests": [
                    {
                      "id": "00000000-0000-0000-0000-000000000001",
                      "name": "Get \"users\"",
                      "path": "Col/Sub",
                      "method": "GET",
                      "url": "{{baseUrl}}/users",
                      "outcome": "passed",
                      "status": 200,
                      "durationMs": 41,
                      "sizeBytes": 512,
                      "error": null,
                      "tests": [
                        {
                          "name": "ok",
                          "passed": true,
                          "message": null,
                          "durationMs": 1
                        }
                      ],
                      "console": [
                        {
                          "at": "2026-11-24T10:00:00.000Z",
                          "level": "info",
                          "source": "script",
                          "message": "hi",
                          "origin": "pre"
                        }
                      ]
                    },
                    {
                      "id": "00000000-0000-0000-0000-000000000001",
                      "name": "b",
                      "path": "Col",
                      "method": "POST",
                      "url": "u",
                      "outcome": "skipped",
                      "status": null,
                      "durationMs": 0,
                      "sizeBytes": 0,
                      "error": "boom",
                      "tests": [],
                      "console": []
                    }
                  ]
                }
              ]
            }
            """;

        Assert.Equal(expected.ReplaceLineEndings("\n"), Text(RunReportJson.Write(Sample())));
    }

    [Theory]
    [InlineData(RunStatus.Completed, "completed")]
    [InlineData(RunStatus.Stopped, "stopped")]
    [InlineData(RunStatus.StoppedOnFailure, "stoppedOnFailure")]
    [InlineData(RunStatus.Canceled, "canceled")]
    [InlineData(RunStatus.Failed, "failed")]
    public void Status_spellings_and_round_trip(RunStatus status, string spelling)
    {
        var root = DataNodeJson.Parse(Text(RunReportJson.Write(Sample(status, "x"))));
        Assert.Equal(spelling, root.GetString("status"));
        Assert.Equal("x", root.GetString("error"));
    }

    [Fact]
    public void Output_has_no_bom_and_no_carriage_return()
    {
        var bytes = RunReportJson.Write(Sample());
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.DoesNotContain((byte)'\r', bytes);
    }

    [Fact]
    public void SuggestedFileName_uses_slug_and_utc_start()
    {
        var result = Sample() with { StartedAt = new DateTimeOffset(2026, 11, 24, 12, 5, 9, TimeSpan.FromHours(2)) };
        Assert.Equal("my-run-run-20261124-100509.json", RunReportJson.SuggestedFileName(result));
    }
}
