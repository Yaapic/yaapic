using System.Text;
using Yaapic.Core.Http;
using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class ScriptSendRequestTests
{
    static readonly ScriptBlock Code = new(ScriptLanguage.JavaScript, "x();");

    static readonly ScriptChain Chain = new([new ScriptStep("req", ScriptSet.Empty with { PreRequest = Code })]);

    static HttpResponseData Ok(string body) =>
        new(200, "OK", [new KeyValueEntry("Content-Type", "text/plain")], Encoding.UTF8.GetBytes(body), TimeSpan.FromMilliseconds(5), "http://sub/");

    static ScriptRequest Sub(string url)
    {
        ScriptRequest r = new() { Url = url };
        return r;
    }

    static Task<ScriptedSendReport> Run(
        FakeScriptEngine engine,
        FakeHttpTransport transport,
        VariableScopes? scopes = null,
        CancellationTokenSource? cts = null)
    {
        ScriptEngineRegistry registry = new();
        registry.Add(engine);
        ScriptedRequestSender sender = new(new RequestSender(transport), registry, TimeProvider.System);
        var request = HttpRequestDefinition.Create("main") with { Url = "http://main/" };
        return sender.SendAsync(new(request, scopes ?? new(null, [], []), Chain, false, false), cts?.Token ?? TestContext.Current.CancellationToken);
    }

    static ScriptRunResult Done(ScriptInvocation i) => ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language);

    [Fact]
    public async Task Sub_request_is_resolved_with_overlay_variables_set_earlier_in_the_block()
    {
        ScriptSendResult? result = null;
        FakeScriptEngine engine = new(i =>
        {
            i.Host.SetVariable(ScriptScope.Local, "host", "http://sub.test");
            result = i.Host.Send(Sub("{{host}}/ping"));
            return Done(i);
        });
        FakeHttpTransport transport = new((r, _) => Task.FromResult(
            r.Url.StartsWith("http://sub.test", StringComparison.Ordinal) ? SendResult.Success(Ok("pong")) : SendResult.Fail(SendFailureKind.Unknown, "main")));

        var report = await Run(engine, transport);

        Assert.Equal("http://sub.test/ping", transport.Requests[0].Url);
        Assert.Equal("pong", result!.Response!.BodyText);
        Assert.Null(result.Error);
        Assert.Equal(2, transport.Requests.Count);
        Assert.Single(report.Runs);
    }

    [Fact]
    public async Task Failure_is_returned_as_error()
    {
        ScriptSendResult? result = null;
        FakeScriptEngine engine = new(i =>
        {
            result = i.Host.Send(Sub("http://sub/"));
            return Done(i);
        });
        FakeHttpTransport transport = new((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.Unknown, "no route")));

        await Run(engine, transport);

        Assert.Null(result!.Response);
        Assert.Equal("no route", result.Error);
    }

    [Fact]
    public async Task Invalid_method_is_returned_as_error()
    {
        ScriptSendResult? result = null;
        FakeScriptEngine engine = new(i =>
        {
            result = i.Host.Send(new ScriptRequest { Url = "http://sub/", Method = "NOPE" });
            return Done(i);
        });
        FakeHttpTransport transport = new((_, _) => Task.FromResult(SendResult.Success(Ok(""))));

        await Run(engine, transport);

        Assert.Contains("NOPE", result!.Error);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task Request_past_the_limit_is_refused()
    {
        List<ScriptSendResult> results = [];
        FakeScriptEngine engine = new(i =>
        {
            for (var n = 0; n < 21; n++)
            {
                results.Add(i.Host.Send(Sub("http://sub/")));
            }

            return Done(i);
        });
        FakeHttpTransport transport = new((_, _) => Task.FromResult(SendResult.Success(Ok(""))));

        await Run(engine, transport);

        Assert.All(results.Take(20), r => Assert.Null(r.Error));
        Assert.Equal("yaapic.sendRequest limit of 20 per run reached", results[20].Error);
        Assert.Equal(21, transport.Requests.Count); // 20 sub-requests + the main one
    }

    [Fact]
    public async Task Sub_requests_log_to_the_network_console_and_run_no_scripts()
    {
        FakeScriptEngine engine = new(i =>
        {
            i.Host.Send(Sub("http://sub/{{missing}}"));
            return Done(i);
        });
        FakeHttpTransport transport = new((_, _) => Task.FromResult(SendResult.Success(Ok("x"))));

        var report = await Run(engine, transport);

        Assert.Single(engine.Invocations);
        Assert.Single(report.Runs);
        Assert.Equal(3, report.NetworkLog.Count);
        Assert.Equal(ConsoleSource.Network, report.NetworkLog[0].Source);
        Assert.Equal("yaapic.sendRequest", report.NetworkLog[0].Origin);
        Assert.Equal(ConsoleLevel.Warn, report.NetworkLog[1].Level);
        Assert.Contains("missing", report.NetworkLog[1].Message);
        Assert.Null(report.NetworkLog[2].Origin);
    }

    [Fact]
    public async Task Cancelling_the_token_cancels_an_in_flight_send()
    {
        using CancellationTokenSource cts = new();
        ScriptSendResult? result = null;
        FakeScriptEngine engine = new(i =>
        {
            result = i.Host.Send(Sub("http://sub/"));
            return Done(i);
        });
        FakeHttpTransport transport = new(async (_, ct) =>
        {
            cts.CancelAfter(50);
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return SendResult.Success(Ok(""));
        });

        var started = DateTime.UtcNow;
        await Run(engine, transport, cts: cts);

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
        Assert.Equal("Request canceled", result!.Error);
    }
}
