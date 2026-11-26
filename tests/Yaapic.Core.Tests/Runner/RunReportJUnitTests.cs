using System.Text;
using System.Xml;
using Yaapic.Core.Runner;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using TestResult = Yaapic.Domain.Scripting.TestResult;

namespace Yaapic.Core.Tests.Runner;

public class RunReportJUnitTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static RunRequestResult Req(string name, RunRequestOutcome o, int? status, TestResult[] tests, ConsoleEntry[] console, string? error = null) =>
        new(Id, name, "Col/Sub", HttpMethodKind.Get, "{{baseUrl}}/x", o, status, TimeSpan.FromMilliseconds(40), 1, tests, console, error);

    private static RunResult Sample(params RunRequestResult[] requests) =>
        new("My Run", Start, TimeSpan.FromMilliseconds(1500), RunStatus.Completed, null, new RunOptions(1, 0, false, false), 0,
            [new RunIterationResult(0, requests)]);

    private static string Text(byte[] b) => Encoding.UTF8.GetString(b);

    [Fact]
    public void Write_produces_the_exact_report()
    {
        var result = Sample(
            Req("a", RunRequestOutcome.Failed, 200,
                [new TestResult("ok", true, null, TimeSpan.FromMilliseconds(1), "t"), new TestResult("bad", false, "nope", TimeSpan.FromMilliseconds(2), "t")],
                [new ConsoleEntry(Start, ConsoleLevel.Info, ConsoleSource.Script, "hi", null)]),
            Req("b", RunRequestOutcome.Error, null, [], [], "boom"),
            Req("c", RunRequestOutcome.Skipped, null, [], []),
            Req("d", RunRequestOutcome.Passed, 204, [], []));
        const string expected = """
            <?xml version="1.0" encoding="utf-8"?>
            <testsuites name="My Run" tests="5" failures="1" errors="1" skipped="1" time="1.500">
              <testsuite name="Iteration 1 / Col/Sub/a" tests="2" failures="1" errors="0" skipped="0" time="0.040" timestamp="2026-11-24T10:00:00Z">
                <properties>
                  <property name="method" value="GET" />
                  <property name="url" value="{{baseUrl}}/x" />
                  <property name="status" value="200" />
                </properties>
                <testcase name="ok" classname="Col/Sub/a" time="0.001" />
                <testcase name="bad" classname="Col/Sub/a" time="0.002">
                  <failure message="nope" />
                </testcase>
                <system-out>[info] hi</system-out>
              </testsuite>
              <testsuite name="Iteration 1 / Col/Sub/b" tests="1" failures="0" errors="1" skipped="0" time="0.040" timestamp="2026-11-24T10:00:00Z">
                <properties>
                  <property name="method" value="GET" />
                  <property name="url" value="{{baseUrl}}/x" />
                </properties>
                <testcase name="request" classname="Col/Sub/b" time="0.040">
                  <error message="boom" />
                </testcase>
              </testsuite>
              <testsuite name="Iteration 1 / Col/Sub/c" tests="1" failures="0" errors="0" skipped="1" time="0.040" timestamp="2026-11-24T10:00:00Z">
                <properties>
                  <property name="method" value="GET" />
                  <property name="url" value="{{baseUrl}}/x" />
                </properties>
                <testcase name="request" classname="Col/Sub/c" time="0.000">
                  <skipped />
                </testcase>
              </testsuite>
              <testsuite name="Iteration 1 / Col/Sub/d" tests="1" failures="0" errors="0" skipped="0" time="0.040" timestamp="2026-11-24T10:00:00Z">
                <properties>
                  <property name="method" value="GET" />
                  <property name="url" value="{{baseUrl}}/x" />
                  <property name="status" value="204" />
                </properties>
                <testcase name="request" classname="Col/Sub/d" time="0.040" />
              </testsuite>
            </testsuites>
            """;
        Assert.Equal(expected, Text(RunReportJUnit.Write(result)));
    }

    [Fact]
    public void Write_escapes_markup_and_replaces_control_characters()
    {
        var bytes = RunReportJUnit.Write(Sample(Req("<&\">", RunRequestOutcome.Error, null, [], [], "bad\u0001x")));
        var doc = new XmlDocument();
        doc.LoadXml(Text(bytes));
        Assert.Equal("Iteration 1 / Col/Sub/<&\">", doc.SelectSingleNode("//testsuite")!.Attributes!["name"]!.Value);
        Assert.Equal("bad?x", doc.SelectSingleNode("//error")!.Attributes!["message"]!.Value);
    }

    [Fact]
    public void Write_has_no_bom_and_parses_with_XmlReader()
    {
        var bytes = RunReportJUnit.Write(Sample());
        Assert.NotEqual(0xEF, bytes[0]);
        using var reader = XmlReader.Create(new MemoryStream(bytes));
        while (reader.Read()) { }
    }

    [Fact]
    public void SuggestedFileName_uses_slug_and_utc_start() =>
        Assert.Equal("my-run-run-20261124-100000.xml", RunReportJUnit.SuggestedFileName(Sample()));
}
