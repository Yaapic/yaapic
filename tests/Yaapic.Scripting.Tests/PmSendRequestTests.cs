using System.Text;
using Yaapic.Core.Http;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.QuickJs;
using Yaapic.TestDoubles;

namespace Yaapic.Scripting.Tests;

/// <summary><c>yaapic.sendRequest</c> and <c>yaapic.cookies</c> on the real QuickJS engine, with a recording host and the real sender.</summary>
public sealed class PmSendRequestTests
{
    private const string Origin = "post-response · Users API";

    private static HttpResponseData Reply(string body = "{\"ok\":true}", params string[] setCookies) =>
        new(
            201,
            "Created",
            [new("Content-Type", "application/json"), .. setCookies.Select(c => new KeyValueEntry("Set-Cookie", c))],
            [.. Encoding.UTF8.GetBytes(body)],
            TimeSpan.FromMilliseconds(5),
            "https://sub.test/x");

    private static ScriptRequest MainRequest() => ScriptRequest.FromDefinition(
        new(Guid.Empty, "Users API", HttpMethodKind.Get, "https://api.example.com", [], [], RequestBody.None, RequestAuth.None, RequestSettings.Default),
        readOnly: true);

    private static async Task<(ScriptRunResult Run, RecordingHost Host)> RunWithHost(
        string source,
        Func<ScriptRequest, ScriptSendResult>? send = null,
        ScriptResponse? response = null,
        IReadOnlyList<ScriptCookie>? cookies = null,
        ScriptPhase phase = ScriptPhase.PostResponse)
    {
        var host = new RecordingHost(response, cookies ?? [], send ?? (_ => new(ScriptResponse.FromData(Reply()), null)));
        var run = await new QuickJsEngine().RunAsync(
            new(new(ScriptLanguage.JavaScript, source), phase, Origin, host, ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(10) }),
            TestContext.Current.CancellationToken);
        return (run, host);
    }

    private static async Task<(ScriptedSendReport Report, FakeHttpTransport Transport)> RunReal(string source, int maxSends = 20)
    {
        ScriptEngineRegistry registry = new();
        registry.Add(new QuickJsEngine());
        FakeHttpTransport transport = new((r, _) => Task.FromResult(
            r.Url.Contains("sub.test", StringComparison.Ordinal) ? SendResult.Success(Reply()) : SendResult.Success(Reply("main"))));
        ScriptedRequestSender sender = new(new RequestSender(transport), registry, TimeProvider.System);
        var request = HttpRequestDefinition.Create("Users API") with { Url = "https://api.example.com/" };
        ScriptChain chain = new([new ScriptStep("Users API", ScriptSet.Empty with { PostResponse = new(ScriptLanguage.JavaScript, source) })]);
        var report = await sender.SendAsync(
            new(request, new(null, [], []), chain, false, false) { Limits = ScriptLimits.Default with { MaxSendRequests = maxSends, Timeout = TimeSpan.FromSeconds(10) } },
            TestContext.Current.CancellationToken);
        return (report, transport);
    }

    private static string Logged(ScriptRunResult run) => string.Join("|", run.Console.Select(c => c.Message));

    [Fact]
    public async Task String_form_sends_a_get_and_the_callback_gets_a_response()
    {
        var (run, host) = await RunWithHost(
            "yaapic.sendRequest('https://sub.test/x', (err, res) => { console.log(String(err), res.code, res.json().ok, res.status); });");

        Assert.True(run.Succeeded, run.Error?.Message);
        var sent = Assert.Single(host.Sent);
        Assert.Equal("GET", sent.Method);
        Assert.Equal("https://sub.test/x", sent.Url);
        Assert.Equal("null 201 true Created", Logged(run));
    }

    [Fact]
    public async Task Options_form_with_header_array_and_raw_json_body()
    {
        var (run, host) = await RunWithHost("""
            yaapic.sendRequest({
              url: 'https://sub.test/users',
              method: 'post',
              header: [{ key: 'X-A', value: '1' }, { key: 'X-Off', value: '2', disabled: true }],
              body: { mode: 'raw', raw: '{"a":1}', options: { raw: { language: 'json' } } },
            }, () => {});
            """);

        Assert.True(run.Succeeded, run.Error?.Message);
        var sent = Assert.Single(host.Sent);
        Assert.Equal("POST", sent.Method);
        Assert.Equal("raw", sent.BodyMode);
        Assert.Equal("{\"a\":1}", sent.BodyText);
        Assert.Equal("json", sent.BodyContentType);
        Assert.Equal([("X-A", "1", true), ("X-Off", "2", false)], sent.Headers.Select(h => (h.Key, h.Value, h.Enabled)));
    }

    [Fact]
    public async Task Options_form_with_header_object_and_urlencoded_body_and_url_object()
    {
        var (run, host) = await RunWithHost("""
            yaapic.sendRequest({
              url: yaapic.request.url,
              method: 'PUT',
              header: { 'X-B': 'two' },
              body: { mode: 'urlencoded', urlencoded: [{ key: 'k', value: 'v' }] },
            }, () => {});
            """);

        Assert.True(run.Succeeded, run.Error?.Message);
        var sent = Assert.Single(host.Sent);
        Assert.Equal("https://api.example.com", sent.Url);
        Assert.Equal("urlencoded", sent.BodyMode);
        Assert.Equal("k", Assert.Single(sent.UrlEncoded).Key);
        Assert.Equal("two", Assert.Single(sent.Headers).Value);
    }

    [Fact]
    public async Task Code_after_the_call_runs_before_the_callback()
    {
        var (run, _) = await RunWithHost("""
            yaapic.sendRequest('https://sub.test/x', () => console.log('callback'));
            console.log('after');
            """);

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal("after|callback", Logged(run));
    }

    [Fact]
    public async Task Promise_form_resolves_with_the_response_and_supports_to_assertions()
    {
        var (run, _) = await RunWithHost("""
            const res = await yaapic.sendRequest('https://sub.test/x');
            yaapic.test('sub', () => { res.to.have.status(201); yaapic.expect(res.json().ok).to.be.true; });
            """);

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.True(Assert.Single(run.Tests).Passed);
    }

    [Fact]
    public async Task Failed_send_gives_an_error_in_the_callback_and_a_rejection_in_the_promise()
    {
        var (run, _) = await RunWithHost("""
            yaapic.sendRequest('https://sub.test/x', (err, res) => console.log(err.message, res === undefined));
            try { await yaapic.sendRequest('https://sub.test/y'); } catch (e) { console.log('rejected', e instanceof Error, e.message); }
            """, _ => new(null, "boom"));

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal("boom true|rejected true boom", Logged(run));
    }

    [Fact]
    public async Task Twenty_first_send_fails_with_the_host_limit_message()
    {
        var (report, _) = await RunReal("""
            for (let i = 0; i < 20; i++) { await yaapic.sendRequest('https://sub.test/' + i); }
            try { await yaapic.sendRequest('https://sub.test/21'); } catch (e) { console.log(e.message); }
            """);

        var run = Assert.Single(report.Runs);
        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal("yaapic.sendRequest limit of 20 per run reached", Logged(run));
    }

    [Fact]
    public async Task Throwing_callback_fails_the_run()
    {
        var (run, _) = await RunWithHost("yaapic.sendRequest('https://sub.test/x', () => { throw new Error('in callback'); });");

        Assert.Equal(ScriptRunStatus.Failed, run.Status);
        Assert.Equal("in callback", run.Error?.Message);
    }

    [Fact]
    public async Task Real_sender_resolves_variables_in_the_url_and_body()
    {
        var (report, transport) = await RunReal("""
            yaapic.variables.set('h', 'sub.test');
            yaapic.variables.set('n', 'Ada');
            await yaapic.sendRequest({ url: 'https://{{h}}/hi', method: 'POST', body: { mode: 'raw', raw: '{"n":"{{n}}"}' } });
            """);

        Assert.True(Assert.Single(report.Runs).Succeeded, report.Runs[0].Error?.Message);
        var sub = transport.Requests.Single(r => r.Url.Contains("sub.test", StringComparison.Ordinal));
        Assert.Equal("https://sub.test/hi", sub.Url);
        Assert.Contains("\"n\":\"Ada\"", sub.BodyText);
    }

    [Fact]
    public async Task Real_sender_response_exposes_json_and_to()
    {
        var (report, _) = await RunReal("""
            const res = await yaapic.sendRequest('https://sub.test/x');
            yaapic.test('real', () => { res.to.have.status(201); yaapic.expect(res.json()).to.eql({ ok: true }); });
            """);

        var run = Assert.Single(report.Runs);
        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.True(Assert.Single(run.Tests).Passed);
    }

    [Fact]
    public async Task Unsupported_body_mode_throws()
    {
        var (run, host) = await RunWithHost("yaapic.sendRequest({ url: 'https://sub.test', body: { mode: 'formdata' } }, () => {});");

        Assert.Equal(ScriptRunStatus.Failed, run.Status);
        Assert.Equal("yaapic.sendRequest body mode 'formdata' is not supported", run.Error?.Message);
        Assert.Empty(host.Sent);
    }

    [Fact]
    public async Task Cookies_read_the_set_cookie_headers_of_the_response()
    {
        var response = ScriptResponse.FromData(Reply("{}", "sid=abc; Path=/; HttpOnly; Secure", "theme=dark; Domain=example.com"));
        var (run, _) = await RunWithHost("""
            console.log(yaapic.cookies.get('sid'), yaapic.cookies.has('theme'), yaapic.cookies.has('x'), yaapic.cookies.get('x'), yaapic.cookies.count());
            console.log(JSON.stringify(yaapic.cookies.toObject()));
            const names = []; yaapic.cookies.each((c) => names.push(c.name));
            console.log(names.join(','), JSON.stringify(yaapic.cookies.all()[0]));
            """, response: response, cookies: response.Cookies);

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal(
            ["abc true false undefined 2", "{\"sid\":\"abc\",\"theme\":\"dark\"}", "sid,theme {\"name\":\"sid\",\"value\":\"abc\",\"path\":\"/\",\"httpOnly\":true,\"secure\":true}"],
            run.Console.Select(c => c.Message));
    }

    [Fact]
    public async Task Cookies_are_empty_in_pre_request_and_jar_throws()
    {
        var (run, _) = await RunWithHost(
            "console.log(yaapic.cookies.count(), yaapic.cookies.get('a')); try { yaapic.cookies.jar(); } catch (e) { console.log(e.message); }",
            phase: ScriptPhase.PreRequest);

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal("0 undefined|The cookie jar is not available in Yaapic yet", Logged(run));
    }

    [Fact]
    public async Task Send_request_response_has_cookies()
    {
        var (run, _) = await RunWithHost(
            "const res = await yaapic.sendRequest('https://sub.test/x'); console.log(res.cookies.get('a'), res.cookies.count());",
            _ => new(ScriptResponse.FromData(Reply("{}", "a=1; Path=/")), null));

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal("1 1", Logged(run));
    }

    private sealed class RecordingHost(ScriptResponse? response, IReadOnlyList<ScriptCookie> cookies, Func<ScriptRequest, ScriptSendResult> send) : IScriptHost
    {
        public List<SentRequest> Sent { get; } = [];

        public ScriptInfo Info => new("test", "Users API", Guid.Empty, Origin);

        public ScriptRequest Request { get; } = MainRequest();

        public ScriptResponse? Response => response;

        public IReadOnlyList<ScriptCookie> ResponseCookies => cookies;

        public bool TryGetVariable(ScriptScope scope, string name, out string value)
        {
            value = "";
            return false;
        }

        public void SetVariable(ScriptScope scope, string name, string value)
        {
        }

        public bool UnsetVariable(ScriptScope scope, string name) => false;

        public void ClearVariables(ScriptScope scope)
        {
        }

        public IReadOnlyList<KeyValueEntry> ListVariables(ScriptScope scope) => [];

        public string ReplaceIn(string template) => template;

        public ScriptSendResult Send(ScriptRequest request)
        {
            Sent.Add(new(request.Method, request.Url, [.. request.Headers], request.BodyMode, request.BodyText, request.BodyContentType, [.. request.UrlEncoded]));
            return send(request);
        }
    }

    private sealed record SentRequest(
        string Method,
        string Url,
        List<KeyValueEntry> Headers,
        string BodyMode,
        string BodyText,
        string BodyContentType,
        List<KeyValueEntry> UrlEncoded);
}
