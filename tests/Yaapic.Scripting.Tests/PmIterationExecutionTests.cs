using Yaapic.Core.Scripting;
using Yaapic.Scripting.Tests.Conformance;

namespace Yaapic.Scripting.Tests;

/// <summary>The JavaScript <c>yaapic.iterationData</c> and <c>yaapic.execution</c>, end to end through the real engine and <see cref="ScriptedRequestSender"/>.</summary>
public sealed class PmIterationExecutionTests
{
    private static readonly ScriptRunContext InRun = new(2, 5, [new("b", "2"), new("a", "1")], true);

    private static Task<(ScriptedSendReport Report, Yaapic.TestDoubles.FakeHttpTransport Transport)> Run(string? pre = null, string? post = null, ScriptRunContext? run = null) =>
        ConformanceRunner.Run(new("it") { RunContext = run }, pre, post);

    [Fact]
    public async Task IterationData_reads_the_row_in_insertion_order()
    {
        var (report, _) = await Run(
            pre: "const d = yaapic.iterationData; console.log(d.get('a'), d.get('zz'), d.has('b'), d.has('zz'), JSON.stringify(d.toObject()), JSON.stringify(d), Object.isFrozen(d));",
            run: InRun);

        Assert.True(report.Runs.All(r => r.Succeeded), report.Runs.FirstOrDefault(r => !r.Succeeded)?.Error?.Message);
        Assert.Equal("1 undefined true false {\"b\":\"2\",\"a\":\"1\"} {\"b\":\"2\",\"a\":\"1\"} true", Assert.Single(report.Console).Message);
    }

    [Fact]
    public async Task IterationData_is_empty_outside_a_data_driven_run()
    {
        var (report, _) = await Run(pre: "console.log(yaapic.iterationData.has('a'), JSON.stringify(yaapic.iterationData.toObject()));");

        Assert.Equal("false {}", Assert.Single(report.Console).Message);
    }

    [Theory]
    [InlineData("yaapic.iterationData.get(1)", "yaapic.iterationData.get: name must be a string")]
    [InlineData("yaapic.iterationData.has(null)", "yaapic.iterationData.has: name must be a string")]
    [InlineData("yaapic.execution.setNextRequest(5)", "yaapic.execution.setNextRequest: expected a request name, id or null")]
    [InlineData("yaapic.execution.setNextRequest({})", "yaapic.execution.setNextRequest: expected a request name, id or null")]
    public async Task Bad_arguments_throw_a_TypeError(string call, string message)
    {
        var (report, _) = await Run(pre: $"try {{ {call}; console.log('no throw'); }} catch (e) {{ console.log(e instanceof TypeError, e.message); }}");

        Assert.Equal($"true {message}", Assert.Single(report.Console).Message);
    }

    [Theory]
    [InlineData("yaapic.execution.setNextRequest('Login')", "Login")]
    [InlineData("postman.setNextRequest('Login')", "Login")]
    [InlineData("yaapic.execution.setNextRequest('11111111-2222-3333-4444-555555555555')", "11111111-2222-3333-4444-555555555555")]
    [InlineData("yaapic.execution.setNextRequest(null)", null)]
    [InlineData("yaapic.execution.setNextRequest()", null)]
    public async Task SetNextRequest_reaches_the_report_in_a_run(string call, string? expected)
    {
        var (report, _) = await Run(post: call + ";", run: InRun);

        Assert.True(report.NextRequestSet);
        Assert.Equal(expected, report.NextRequest);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public async Task SetNextRequest_outside_a_run_only_warns()
    {
        var (report, _) = await Run(post: "yaapic.execution.setNextRequest('Login');");

        Assert.False(report.NextRequestSet);
        Assert.Single(report.Warnings);
    }

    [Fact]
    public async Task SkipRequest_in_pre_request_skips_the_send_and_the_script_keeps_running()
    {
        var (report, transport) = await Run(pre: "yaapic.execution.skipRequest(); console.log('after');");

        Assert.True(report.Skipped);
        Assert.Empty(transport.Requests);
        Assert.Equal("after", Assert.Single(report.Console).Message);
    }

    [Fact]
    public async Task SkipRequest_in_post_response_is_a_script_error()
    {
        var (report, _) = await Run(post: "yaapic.execution.skipRequest();");

        Assert.False(report.Skipped);
        var failed = Assert.Single(report.Runs, r => !r.Succeeded);
        Assert.Contains("skipRequest", failed.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Info_reports_the_iteration_inside_and_outside_a_run()
    {
        var (inRun, _) = await Run(pre: "console.log(yaapic.info.iteration, yaapic.info.iterationCount);", run: InRun);
        var (single, _) = await Run(pre: "console.log(yaapic.info.iteration, yaapic.info.iterationCount);");

        Assert.Equal("2 5", Assert.Single(inRun.Console).Message);
        Assert.Equal("0 1", Assert.Single(single.Console).Message);
    }
}
