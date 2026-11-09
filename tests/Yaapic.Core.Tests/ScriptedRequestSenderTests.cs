using Yaapic.Core.Http;
using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class ScriptedRequestSenderTests
{
    static readonly ScriptBlock Code = new(ScriptLanguage.JavaScript, "x();");

    static ScriptSet Pre(ScriptBlock? block = null) => ScriptSet.Empty with { PreRequest = block ?? Code };

    static ScriptRunResult Ok(ScriptInvocation i) => ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language);

    static ScriptRunResult Fail(ScriptInvocation i, ScriptRunStatus status = ScriptRunStatus.Failed) =>
        new(i.Phase, i.Origin, i.Block.Language, status, new ScriptError("boom"), [], [], TimeSpan.Zero);

    static FakeHttpTransport Transport() =>
        new((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.Unknown, "none")));

    static HttpRequestDefinition Req(string url = "http://a/") => HttpRequestDefinition.Create("r") with { Url = url };

    static VariableScopes NoScopes() => new(null, [], []);

    static (ScriptedRequestSender Sender, FakeHttpTransport Transport) Build(FakeScriptEngine engine)
    {
        var transport = Transport();
        ScriptEngineRegistry registry = new();
        registry.Add(engine);
        return (new ScriptedRequestSender(new RequestSender(transport), registry, TimeProvider.System), transport);
    }

    static ScriptedSendInput Input(HttpRequestDefinition request, ScriptChain chain, VariableScopes? scopes = null) =>
        new(request, scopes ?? NoScopes(), chain, false, false);

    static ScriptChain Chain(params (string Origin, ScriptSet Set)[] steps) =>
        new([.. steps.Select(s => new ScriptStep(s.Origin, s.Set))]);

    [Fact]
    public async Task No_scripts_behaves_like_plain_sender()
    {
        var engine = FakeScriptEngine.FromQueue();
        var (sender, transport) = Build(engine);
        var request = Req("http://a/x");

        var report = await sender.SendAsync(Input(request, ScriptChain.Empty.WithRequest("r", ScriptSet.Empty)), CancellationToken.None);

        var expected = await new RequestSender(Transport()).SendAsync(request, NoScopes(), CancellationToken.None);
        Assert.Equal(expected.Result, report.Report!.Result);
        Assert.Equal(expected.UnresolvedVariables, report.Report.UnresolvedVariables);
        Assert.Empty(report.Runs);
        Assert.Empty(report.Changes);
        Assert.Null(report.AbortedBy);
        Assert.Empty(engine.Invocations);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task Runs_blocks_in_chain_order_and_skips_empty_ones()
    {
        var engine = FakeScriptEngine.FromQueue();
        var (sender, _) = Build(engine);
        var chain = Chain(("col", Pre()), ("empty", ScriptSet.Empty), ("folder", Pre()), ("req", Pre()));

        var report = await sender.SendAsync(Input(Req(), chain), CancellationToken.None);

        Assert.Equal(
            ["pre-request · col", "pre-request · folder", "pre-request · req"],
            engine.Invocations.Select(i => i.Origin));
        Assert.Equal(3, report.Runs.Count);
        Assert.All(engine.Invocations, i => Assert.Equal(ScriptPhase.PreRequest, i.Phase));
    }

    [Fact]
    public async Task Blocks_share_one_request()
    {
        string? seen = null;
        var calls = 0;
        var engine = new FakeScriptEngine(i =>
        {
            if (calls++ == 0)
            {
                i.Host.Request.Headers.Add(new KeyValueEntry("X-A", "1"));
            }
            else
            {
                seen = i.Host.Request.Headers.Single(h => h.Key == "X-A").Value;
            }

            return Ok(i);
        });
        var (sender, _) = Build(engine);

        await sender.SendAsync(Input(Req(), Chain(("col", Pre()), ("req", Pre()))), CancellationToken.None);

        Assert.Equal("1", seen);
    }

    [Fact]
    public async Task Variable_set_by_script_resolves_in_sent_url()
    {
        var engine = new FakeScriptEngine(i =>
        {
            i.Host.SetVariable(ScriptScope.Local, "host", "http://b.com");
            return Ok(i);
        });
        var (sender, transport) = Build(engine);

        await sender.SendAsync(Input(Req("{{host}}/x"), Chain(("req", Pre()))), CancellationToken.None);

        Assert.Equal("http://b.com/x", transport.Requests[0].Url);
    }

    [Fact]
    public async Task Mutated_method_url_header_and_body_reach_the_transport()
    {
        var engine = new FakeScriptEngine(i =>
        {
            var r = i.Host.Request;
            r.Method = "POST";
            r.Url = "http://c/y";
            r.Headers.Add(new KeyValueEntry("X-B", "2"));
            r.BodyMode = "raw";
            r.BodyContentType = "json";
            r.BodyText = "{\"a\":1}";
            return Ok(i);
        });
        var (sender, transport) = Build(engine);

        var report = await sender.SendAsync(Input(Req(), Chain(("req", Pre()))), CancellationToken.None);

        var sent = transport.Requests[0];
        Assert.Equal(HttpMethodKind.Post, sent.Method);
        Assert.Equal("http://c/y", sent.Url);
        Assert.Contains(sent.Headers, h => h.Key == "X-B" && h.Value == "2");
        Assert.Equal("{\"a\":1}", sent.BodyText);
        Assert.Equal(HttpMethodKind.Post, report.SentDefinition!.Method);
    }

    [Fact]
    public async Task First_failing_block_stops_chain_and_discards_changes()
    {
        var calls = 0;
        var engine = new FakeScriptEngine(i =>
        {
            i.Host.SetVariable(ScriptScope.Globals, "g", "1");
            return ++calls == 2 ? Fail(i) : Ok(i);
        });
        var (sender, transport) = Build(engine);

        var report = await sender.SendAsync(
            Input(Req(), Chain(("a", Pre()), ("b", Pre()), ("c", Pre()))),
            CancellationToken.None);

        Assert.Empty(transport.Requests);
        Assert.Null(report.Report);
        Assert.Null(report.SentDefinition);
        Assert.Equal(2, report.Runs.Count);
        Assert.Same(report.Runs[1], report.AbortedBy);
        Assert.Empty(report.Changes);
        Assert.Equal(2, engine.Invocations.Count);
    }

    [Theory]
    [InlineData(ScriptRunStatus.TimedOut)]
    [InlineData(ScriptRunStatus.MemoryLimit)]
    public async Task Limit_statuses_abort(ScriptRunStatus status)
    {
        var (sender, transport) = Build(new FakeScriptEngine(i => Fail(i, status)));

        var report = await sender.SendAsync(Input(Req(), Chain(("r", Pre()))), CancellationToken.None);

        Assert.Equal(status, report.AbortedBy!.Status);
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task Unavailable_engine_aborts_with_reason()
    {
        var transport = Transport();
        var sender = new ScriptedRequestSender(new RequestSender(transport), new ScriptEngineRegistry(), TimeProvider.System);

        var report = await sender.SendAsync(Input(Req(), Chain(("r", Pre()))), CancellationToken.None);

        Assert.Equal(ScriptRunStatus.Unavailable, report.AbortedBy!.Status);
        Assert.Contains("JavaScript", report.AbortedBy.Error!.Message);
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task Unknown_method_aborts_as_failed_run()
    {
        var engine = new FakeScriptEngine(i =>
        {
            i.Host.Request.Method = "NOPE";
            return Ok(i);
        });
        var (sender, transport) = Build(engine);

        var report = await sender.SendAsync(Input(Req(), Chain(("col", Pre()), ("req", Pre()))), CancellationToken.None);

        Assert.Equal(ScriptRunStatus.Failed, report.AbortedBy!.Status);
        Assert.Contains("NOPE", report.AbortedBy.Error!.Message);
        Assert.Equal("pre-request · req", report.AbortedBy.Origin);
        Assert.Same(report.AbortedBy, report.Runs[^1]);
        Assert.Empty(transport.Requests);
        Assert.Empty(report.Changes);
    }

    [Fact]
    public async Task Canceled_token_aborts_with_canceled_run()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        var engine = FakeScriptEngine.FromQueue();
        var (sender, transport) = Build(engine);

        var report = await sender.SendAsync(Input(Req(), Chain(("r", Pre()))), cts.Token);

        Assert.Equal(ScriptRunStatus.Canceled, report.AbortedBy!.Status);
        Assert.Null(report.Report);
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task Cancellation_during_a_run_aborts_with_canceled_run()
    {
        using CancellationTokenSource cts = new();
        var engine = FakeScriptEngine.FromQueue();
        engine.Delay = TimeSpan.FromSeconds(30);
        var (sender, _) = Build(engine);

        var task = sender.SendAsync(Input(Req(), Chain(("r", Pre()))), cts.Token);
        await cts.CancelAsync();
        var report = await task;

        Assert.Equal(ScriptRunStatus.Canceled, report.AbortedBy!.Status);
    }

    [Fact]
    public async Task Changes_and_warnings_propagate_on_success()
    {
        var engine = new FakeScriptEngine(i =>
        {
            i.Host.SetVariable(ScriptScope.Globals, "g", "1");
            i.Host.SetVariable(ScriptScope.Environment, "e", "2");
            return Ok(i);
        });
        var (sender, _) = Build(engine);

        var report = await sender.SendAsync(Input(Req(), Chain(("r", Pre()))), CancellationToken.None);

        Assert.Contains(report.Changes, c => c.Scope == VariableScopeKind.Globals && c.Name == "g" && c.Value == "1");
        Assert.Single(report.Warnings);
    }

    [Fact]
    public async Task Default_limits_are_used_when_none_given()
    {
        var engine = FakeScriptEngine.FromQueue();
        var (sender, _) = Build(engine);

        await sender.SendAsync(Input(Req(), Chain(("r", Pre()))), CancellationToken.None);

        Assert.Same(ScriptLimits.Default, engine.Invocations[0].Limits);
    }
}
