using System.Text;
using Yaapic.Core.Http;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.Lua;
using Yaapic.TestDoubles;

namespace Yaapic.Scripting.Tests;

/// <summary>The Lua <c>yaapic.sendRequest</c> and <c>yaapic.cookies</c> on the real Lua engine, with a recording host and the real sender.</summary>
public sealed class LuaPmSendRequestTests
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
        var run = await new LuaEngine().RunAsync(
            new(new(ScriptLanguage.Lua, source), phase, Origin, host, ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(10) }),
            TestContext.Current.CancellationToken);
        return (run, host);
    }

    private static async Task<(ScriptedSendReport Report, FakeHttpTransport Transport)> RunReal(string source, int maxSends = 20)
    {
        ScriptEngineRegistry registry = new();
        registry.Add(new LuaEngine());
        FakeHttpTransport transport = new((r, _) => Task.FromResult(
            r.Url.Contains("sub.test", StringComparison.Ordinal) ? SendResult.Success(Reply()) : SendResult.Success(Reply("main"))));
        ScriptedRequestSender sender = new(new RequestSender(transport), registry, TimeProvider.System);
        var request = HttpRequestDefinition.Create("Users API") with { Url = "https://api.example.com/" };
        ScriptChain chain = new([new ScriptStep("Users API", ScriptSet.Empty with { PostResponse = new(ScriptLanguage.Lua, source) })]);
        var report = await sender.SendAsync(
            new(request, new(null, [], []), chain, false, false) { Limits = ScriptLimits.Default with { MaxSendRequests = maxSends, Timeout = TimeSpan.FromSeconds(10) } },
            TestContext.Current.CancellationToken);
        return (report, transport);
    }

    private static string Logged(ScriptRunResult run) => string.Join("|", run.Console.Select(c => c.Message));

    [Fact]
    public async Task String_form_sends_a_get_and_returns_the_response()
    {
        var (run, host) = await RunWithHost(
            "local res, err = yaapic.sendRequest('https://sub.test/x') console.log(tostring(err), res.code, res.json().ok, res.status, res.response_time)");

        Assert.True(run.Succeeded, run.Error?.Message);
        var sent = Assert.Single(host.Sent);
        Assert.Equal("GET", sent.Method);
        Assert.Equal("https://sub.test/x", sent.Url);
        Assert.Equal("nil 201 true Created 5", Logged(run));
    }

    [Fact]
    public async Task Snake_case_alias_and_response_read_api()
    {
        var (run, _) = await RunWithHost("""
            local res = yaapic.send_request('https://sub.test/x')
            console.log(res.text(), res.reason(), res.size().body, res.responseSize == res.response_size, res.headers.get('content-type'))
            """);

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal("{\"ok\":true} Created 11 true application/json", Logged(run));
    }

    [Fact]
    public async Task Table_form_with_header_array_and_raw_json_body()
    {
        var (run, host) = await RunWithHost("""
            yaapic.sendRequest({
              url = 'https://sub.test/users',
              method = 'post',
              header = { { key = 'X-A', value = '1' }, { key = 'X-Off', value = '2', disabled = true } },
              body = { mode = 'raw', raw = '{"a":1}', options = { raw = { language = 'json' } } },
            })
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
    public async Task Table_form_with_header_map_and_urlencoded_body_and_url_object()
    {
        var (run, host) = await RunWithHost("""
            yaapic.sendRequest({
              url = yaapic.request.url,
              method = 'PUT',
              header = { ['X-B'] = 'two' },
              body = { mode = 'urlencoded', urlencoded = { { key = 'k', value = 'v' } } },
            })
            """);

        Assert.True(run.Succeeded, run.Error?.Message);
        var sent = Assert.Single(host.Sent);
        Assert.Equal("https://api.example.com", sent.Url);
        Assert.Equal("urlencoded", sent.BodyMode);
        Assert.Equal("k", Assert.Single(sent.UrlEncoded).Key);
        Assert.Equal("two", Assert.Single(sent.Headers).Value);
    }

    [Fact]
    public async Task Failed_send_returns_nil_and_an_error_string()
    {
        var (run, _) = await RunWithHost(
            "local res, err = yaapic.sendRequest('https://sub.test/x') console.log(tostring(res), err, type(err))",
            _ => new(null, "boom"));

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal("nil boom string", Logged(run));
    }

    [Fact]
    public async Task Callback_runs_before_the_call_returns_with_err_then_res()
    {
        var (run, _) = await RunWithHost("""
            local res, err = yaapic.sendRequest('https://sub.test/x', function(e, r) console.log('callback', tostring(e), r.code) end)
            console.log('after', res.code, tostring(err))
            """);

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal("callback nil 201|after 201 nil", Logged(run));
    }

    [Fact]
    public async Task Callback_gets_the_error_of_a_failed_send()
    {
        var (run, _) = await RunWithHost(
            "local res, err = yaapic.sendRequest('https://sub.test/x', function(e, r) console.log(e, tostring(r)) end) console.log(tostring(res), err)",
            _ => new(null, "boom"));

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal("boom nil|nil boom", Logged(run));
    }

    [Fact]
    public async Task Callback_error_propagates_as_a_script_error()
    {
        var (run, _) = await RunWithHost("yaapic.sendRequest('https://sub.test/x', function() error('in callback', 0) end) console.log('unreachable')");

        Assert.Equal(ScriptRunStatus.Failed, run.Status);
        Assert.Equal("in callback", run.Error?.Message);
        Assert.Empty(run.Console);
    }

    [Fact]
    public async Task Callback_must_be_a_function()
    {
        var (run, host) = await RunWithHost("yaapic.sendRequest('https://sub.test/x', 5)");

        Assert.Equal(ScriptRunStatus.Failed, run.Status);
        Assert.Contains("yaapic.sendRequest: callback must be a function", run.Error?.Message, StringComparison.Ordinal);
        Assert.Empty(host.Sent);
    }

    [Fact]
    public async Task Invalid_input_raises()
    {
        var (run, host) = await RunWithHost("yaapic.sendRequest({ method = 'GET' })");

        Assert.Equal(ScriptRunStatus.Failed, run.Status);
        Assert.Contains("yaapic.sendRequest: expected a URL or an options table with a url", run.Error?.Message, StringComparison.Ordinal);
        Assert.Empty(host.Sent);
    }

    [Fact]
    public async Task Twenty_first_send_returns_the_host_limit_message()
    {
        var (report, _) = await RunReal("""
            for i = 1, 20 do assert(yaapic.sendRequest('https://sub.test/' .. i)) end
            local res, err = yaapic.sendRequest('https://sub.test/21')
            console.log(tostring(res), err)
            """);

        var run = Assert.Single(report.Runs);
        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal("nil yaapic.sendRequest limit of 20 per run reached", Logged(run));
    }

    [Fact]
    public async Task Real_sender_resolves_variables_in_the_url_and_body()
    {
        var (report, transport) = await RunReal("""
            yaapic.variables.set('h', 'sub.test')
            yaapic.variables.set('n', 'Ada')
            yaapic.sendRequest({ url = 'https://{{h}}/hi', method = 'POST', body = { mode = 'raw', raw = '{"n":"{{n}}"}' } })
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
            local res = yaapic.sendRequest('https://sub.test/x')
            yaapic.test('real', function()
              res.to.have.status(201)
              yaapic.expect(res.json().ok).to.equal(true)
            end)
            """);

        var run = Assert.Single(report.Runs);
        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.True(Assert.Single(run.Tests).Passed);
    }

    [Fact]
    public async Task Unsupported_body_mode_raises()
    {
        var (run, host) = await RunWithHost("yaapic.sendRequest({ url = 'https://sub.test', body = { mode = 'formdata' } })");

        Assert.Equal(ScriptRunStatus.Failed, run.Status);
        Assert.Contains("yaapic.sendRequest body mode 'formdata' is not supported", run.Error?.Message, StringComparison.Ordinal);
        Assert.Empty(host.Sent);
    }

    [Fact]
    public async Task Cookies_read_the_set_cookie_headers_of_the_response()
    {
        var response = ScriptResponse.FromData(Reply("{}", "sid=abc; Path=/; HttpOnly; Secure", "theme=dark; Domain=example.com"));
        var (run, _) = await RunWithHost("""
            console.log(yaapic.cookies.get('sid'), yaapic.cookies.has('theme'), yaapic.cookies.has('x'), tostring(yaapic.cookies.get('x')), yaapic.cookies.count())
            console.log(yaapic.json.encode(yaapic.cookies.to_object()) == yaapic.json.encode(yaapic.cookies.toObject()))
            local names = {}
            yaapic.cookies.each(function(c) names[#names + 1] = c.name end)
            local first = yaapic.cookies.all()[1]
            console.log(table.concat(names, ','), first.name, first.value, first.path, tostring(first.domain), first.http_only, first.httpOnly, first.secure)
            console.log(yaapic.cookies.to_object().theme, #yaapic.cookies.all())
            """, response: response, cookies: response.Cookies);

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal(
            ["abc true false nil 2", "true", "sid,theme sid abc / nil true true true", "dark 2"],
            run.Console.Select(c => c.Message));
    }

    [Fact]
    public async Task Post_response_cookies_are_also_on_pm_response()
    {
        var response = ScriptResponse.FromData(Reply("{}", "sid=abc"));
        var (run, _) = await RunWithHost("console.log(yaapic.response.cookies.get('sid'), yaapic.response.cookies.count())", response: response, cookies: response.Cookies);

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal("abc 1", Logged(run));
    }

    [Fact]
    public async Task Cookies_are_empty_in_pre_request_and_jar_raises()
    {
        var (run, _) = await RunWithHost(
            "console.log(yaapic.cookies.count(), tostring(yaapic.cookies.get('a'))) local ok, e = pcall(yaapic.cookies.jar) console.log(ok, (tostring(e):gsub('^.-:%d+: ', '')))",
            phase: ScriptPhase.PreRequest);

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.Equal("0 nil|false The cookie jar is not available in Yaapic yet", Logged(run));
    }

    [Fact]
    public async Task Send_request_response_has_cookies_and_a_jar_that_raises()
    {
        var (run, _) = await RunWithHost(
            "local res = yaapic.sendRequest('https://sub.test/x') console.log(res.cookies.get('a'), res.cookies.count(), pcall(res.cookies.jar))",
            _ => new(ScriptResponse.FromData(Reply("{}", "a=1; Path=/")), null));

        Assert.True(run.Succeeded, run.Error?.Message);
        Assert.StartsWith("1 1 false", Logged(run), StringComparison.Ordinal);
        Assert.Contains("The cookie jar is not available in Yaapic yet", Logged(run), StringComparison.Ordinal);
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
