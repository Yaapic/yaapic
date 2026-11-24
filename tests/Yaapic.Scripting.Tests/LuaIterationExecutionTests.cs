using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.Tests.Conformance;

namespace Yaapic.Scripting.Tests;

/// <summary>The Lua <c>yaapic.iterationData</c> and <c>yaapic.execution</c>, end to end through the real engine and <see cref="ScriptedRequestSender"/>.</summary>
public sealed class LuaIterationExecutionTests
{
    private static readonly ScriptRunContext InRun = new(2, 5, [new("b", "2"), new("a", "1")], true);

    private static Task<(ScriptedSendReport Report, Yaapic.TestDoubles.FakeHttpTransport Transport)> Run(string? pre = null, string? post = null, ScriptRunContext? run = null) =>
        ConformanceRunner.Run(new("it") { Language = ScriptLanguage.Lua, RunContext = run }, pre, post);

    [Fact]
    public async Task IterationData_reads_the_row_with_both_spellings()
    {
        var (report, _) = await Run(
            pre: "local d = yaapic.iterationData local o = d.to_object() console.log(d.get('a'), d.get('zz'), d.has('b'), d.has('zz'), o.a, o.b, d.toJSON().a, yaapic.iteration_data == d)",
            run: InRun);

        Assert.True(report.Runs.All(r => r.Succeeded), report.Runs.FirstOrDefault(r => !r.Succeeded)?.Error?.Message);
        Assert.Equal("1 nil true false 1 2 1 true", Assert.Single(report.Console).Message);
    }

    [Fact]
    public async Task IterationData_is_empty_outside_a_data_driven_run()
    {
        var (report, _) = await Run(pre: "console.log(yaapic.iterationData.has('a'), next(yaapic.iterationData.toObject()))");

        Assert.Equal("false nil", Assert.Single(report.Console).Message);
    }

    [Theory]
    [InlineData("yaapic.iterationData.get(1)", "yaapic.iterationData.get: name must be a string")]
    [InlineData("yaapic.iterationData.has(nil)", "yaapic.iterationData.has: name must be a string")]
    [InlineData("yaapic.execution.set_next_request(5)", "yaapic.execution.setNextRequest: expected a request name, id or null")]
    [InlineData("yaapic.execution.setNextRequest({})", "yaapic.execution.setNextRequest: expected a request name, id or null")]
    [InlineData("yaapic.iterationData.x = 1", "yaapic.iterationData is read-only")]
    [InlineData("yaapic.execution.x = 1", "yaapic.execution is read-only")]
    public async Task Bad_use_raises_an_error(string call, string message)
    {
        var (report, _) = await Run(pre: $"local ok, e = pcall(function() {call} end) console.log(ok, e)");

        Assert.EndsWith(message, Assert.Single(report.Console).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("yaapic.execution.setNextRequest('Login')", "Login")]
    [InlineData("yaapic.execution.set_next_request('Login')", "Login")]
    [InlineData("yaapic.execution.set_next_request('11111111-2222-3333-4444-555555555555')", "11111111-2222-3333-4444-555555555555")]
    [InlineData("yaapic.execution.set_next_request(nil)", null)]
    [InlineData("yaapic.execution.set_next_request(yaapic.null)", null)]
    [InlineData("yaapic.execution.set_next_request()", null)]
    public async Task SetNextRequest_reaches_the_report_in_a_run(string call, string? expected)
    {
        var (report, _) = await Run(post: call, run: InRun);

        Assert.True(report.NextRequestSet);
        Assert.Equal(expected, report.NextRequest);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public async Task SetNextRequest_outside_a_run_only_warns()
    {
        var (report, _) = await Run(post: "yaapic.execution.set_next_request('Login')");

        Assert.False(report.NextRequestSet);
        Assert.Single(report.Warnings);
    }

    [Fact]
    public async Task SkipRequest_in_pre_request_skips_the_send_and_the_script_keeps_running()
    {
        var (report, transport) = await Run(pre: "yaapic.execution.skip_request() console.log('after')");

        Assert.True(report.Skipped);
        Assert.Empty(transport.Requests);
        Assert.Equal("after", Assert.Single(report.Console).Message);
    }

    [Fact]
    public async Task SkipRequest_in_post_response_is_a_script_error()
    {
        var (report, _) = await Run(post: "yaapic.execution.skipRequest()");

        Assert.False(report.Skipped);
        Assert.Contains("skipRequest", Assert.Single(report.Runs, r => !r.Succeeded).Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Info_reports_the_iteration_inside_and_outside_a_run()
    {
        const string Script = "console.log(yaapic.info.iteration, yaapic.info.iteration_count, yaapic.info.iterationCount)";
        var (inRun, _) = await Run(pre: Script, run: InRun);
        var (single, _) = await Run(pre: Script);

        Assert.Equal("2 5 5", Assert.Single(inRun.Console).Message);
        Assert.Equal("0 1 1", Assert.Single(single.Console).Message);
    }

    [Fact]
    public async Task Mixed_chain_shares_the_row_and_the_last_setNextRequest_wins()
    {
        var (report, _) = await ConformanceRunner.Run(new("mix")
        {
            RunContext = InRun,
            Chain =
            [
                (ScriptLanguage.JavaScript, "Collection", "console.log(yaapic.iterationData.get('a')); yaapic.execution.setNextRequest('First');", ""),
                (ScriptLanguage.Lua, "Request", "console.log(yaapic.iterationData.get('a'))", "yaapic.execution.set_next_request('Last')"),
            ],
        });

        Assert.Equal(["1", "1"], report.Console.Select(e => e.Message));
        Assert.Equal("Last", report.NextRequest);
    }
}
