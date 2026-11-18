using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.QuickJs;

namespace Yaapic.Scripting.Tests;

/// <summary>The JavaScript <c>yaapic.request</c> and <c>yaapic.response</c>, on the real QuickJS engine over a real <see cref="ScriptRequest"/>.</summary>
public sealed class PmRequestResponseTests
{
    internal const string Origin = "pre-request · Users API";

    internal static readonly Guid RequestId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    internal static HttpRequestDefinition Definition(string url = "https://api.example.com:8443/v1/users?page=1&q=a%20b#top", RequestBody? body = null) =>
        new(
            RequestId,
            "Users API",
            HttpMethodKind.Get,
            url,
            [],
            [new("Accept", "application/json"), new("X-Old", "1"), new("X-Off", "no", false)],
            body ?? RequestBody.None,
            RequestAuth.None,
            RequestSettings.Default);

    private static async Task<(ScriptRunResult Result, ScriptRequest Request)> Run(
        string source,
        HttpRequestDefinition? definition = null,
        ScriptResponse? response = null,
        bool readOnly = false)
    {
        var request = ScriptRequest.FromDefinition(definition ?? Definition(), readOnly);
        TestHost host = new(request, response, new(readOnly ? "test" : "prerequest", "Users API", RequestId, Origin));
        var result = await new QuickJsEngine().RunAsync(
            new(
                new(ScriptLanguage.JavaScript, source),
                readOnly ? ScriptPhase.PostResponse : ScriptPhase.PreRequest,
                Origin,
                host,
                ScriptLimits.Default),
            TestContext.Current.CancellationToken);
        return (result, request);
    }

    private static async Task<string> Eval(string expression, HttpRequestDefinition? definition = null, ScriptResponse? response = null)
    {
        var (result, _) = await Run($"console.log(JSON.stringify({expression}) ?? 'undefined')", definition, response, response is not null);
        Assert.True(result.Succeeded, result.Error?.Message);
        return Assert.Single(result.Console).Message;
    }

    internal static ScriptResponse Response(string text, string contentType = "application/json", int code = 200, int padding = 0) =>
        ScriptResponse.FromData(new(
            code,
            code == 200 ? "OK" : "Not Found",
            [new("Content-Type", contentType), new("Set-Cookie", "a=1"), new("set-cookie", "b=2")],
            [.. System.Text.Encoding.UTF8.GetBytes(text), .. new byte[padding]],
            TimeSpan.FromMilliseconds(42),
            "https://api.example.com/v1/users"));

    [Fact]
    public async Task Method_is_read_and_set_in_upper_case()
    {
        Assert.Equal("\"GET\"", await Eval("yaapic.request.method"));
        var (result, request) = await Run("yaapic.request.method = 'post';");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("POST", request.ApplyTo(Definition()).Method.ToText());
    }

    [Fact]
    public async Task Id_and_name_come_from_the_request_info()
    {
        Assert.Equal($"[\"{RequestId}\",\"Users API\"]", await Eval("[yaapic.request.id, yaapic.request.name]"));
    }

    [Fact]
    public async Task Url_exposes_its_parts()
    {
        Assert.Equal("\"https://api.example.com:8443/v1/users?page=1&q=a%20b#top\"", await Eval("yaapic.request.url.toString()"));
        Assert.Equal(
            "[\"https\",[\"api\",\"example\",\"com\"],\"8443\",[\"v1\",\"users\"],\"api.example.com\",\"/v1/users\",\"/v1/users?page=1&q=a%20b\",\"page=1&q=a%20b\",\"top\"]",
            await Eval("(() => { const u = yaapic.request.url; return [u.protocol, u.host, u.port, u.path, u.getHost(), u.getPath(), u.getPathWithQuery(), u.getQueryString(), u.hash]; })()"));
        Assert.Equal(
            "[[{\"key\":\"page\",\"value\":\"1\"},{\"key\":\"q\",\"value\":\"a b\"}],2,\"a b\",true,false,{\"page\":\"1\",\"q\":\"a b\"}]",
            await Eval("(() => { const q = yaapic.request.url.query; return [q.all(), q.count(), q.get('q'), q.has('page'), q.has('nope'), q.toObject()]; })()"));
    }

    [Fact]
    public async Task Url_with_variables_survives_untouched()
    {
        const string Url = "{{base}}/users/{{id}}?a={{x}}&b=1#f";
        Assert.Equal($"\"{Url}\"", await Eval("yaapic.request.url.toString()", Definition(Url)));
        Assert.Equal(
            "[[\"{{base}}\"],\"\",\"{{x}}\"]",
            await Eval("(() => { const u = yaapic.request.url; return [u.host, u.port, u.query.get('a')]; })()", Definition(Url)));
    }

    [Fact]
    public async Task Url_set_and_update_reach_the_applied_definition()
    {
        var (result, request) = await Run("yaapic.request.url = 'https://other.test/x?y=2'; yaapic.request.url.update('{{base}}/z?k=v#frag');");

        Assert.True(result.Succeeded, result.Error?.Message);
        var applied = request.ApplyTo(Definition());
        Assert.Equal("{{base}}/z#frag", applied.Url);
        Assert.Equal([new("k", "v")], applied.QueryParams);
    }

    [Fact]
    public async Task Url_set_accepts_a_url_object()
    {
        var (result, request) = await Run("const u = yaapic.request.url; u.query.add({ key: 'n', value: '1' }); yaapic.request.url = u;");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Contains(new KeyValueEntry("n", "1"), request.ApplyTo(Definition()).QueryParams);
    }

    [Fact]
    public async Task Query_add_upsert_and_remove_are_applied_with_variables_and_fragment()
    {
        var (result, request) = await Run(
            "const q = yaapic.request.url.query; q.add({ key: 'new', value: 'a b&c' }); q.upsert({ key: 'page', value: '2' }); q.upsert({ key: 'tok', value: '{{token}}' }); q.remove('q');",
            Definition("{{base}}/v1?page=1&q=x#top"));

        Assert.True(result.Succeeded, result.Error?.Message);
        var applied = request.ApplyTo(Definition("{{base}}/v1?page=1&q=x#top"));
        Assert.Equal("{{base}}/v1#top", applied.Url);
        Assert.Equal([new("page", "2"), new("new", "a b&c"), new("tok", "{{token}}")], applied.QueryParams);
        Assert.Contains("tok={{token}}", request.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Url_part_setters_rebuild_the_url()
    {
        var (result, request) = await Run("const u = yaapic.request.url; u.protocol = 'http'; u.host = ['svc', 'test']; u.port = 80; u.path = ['a', 'b']; u.hash = 'x';");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("http://svc.test:80/a/b?page=1&q=a%20b#x", request.Url);
    }

    [Fact]
    public async Task Headers_are_case_insensitive_and_editable()
    {
        Assert.Equal(
            "[\"application/json\",true,false,3,{\"Accept\":\"application/json\",\"X-Old\":\"1\"},{\"key\":\"X-Old\",\"value\":\"1\"}]",
            await Eval("(() => { const h = yaapic.request.headers; return [h.get('ACCEPT'), h.has('x-old'), h.has('X-Off'), h.count(), h.toObject(), h.idx(1)]; })()"));

        var (result, request) = await Run(
            "const h = yaapic.request.headers; h.add({ key: 'X-New', value: 'n' }); h.upsert({ key: 'accept', value: 'text/plain' }); h.remove('X-OLD'); const keys = []; h.each((i) => keys.push(i.key)); console.log(keys.join());");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("accept,X-Off,X-New", Assert.Single(result.Console).Message);
        Assert.Equal([new("accept", "text/plain"), new("X-Off", "no", false), new("X-New", "n")], request.ApplyTo(Definition()).Headers);
    }

    [Fact]
    public async Task Raw_body_is_set_and_the_mode_switched()
    {
        var (result, request) = await Run("yaapic.request.body.mode = 'raw'; yaapic.request.body.raw = '{\"a\":1}'; console.log(yaapic.request.body.mode, yaapic.request.body.toString());");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("raw {\"a\":1}", Assert.Single(result.Console).Message);
        var applied = request.ApplyTo(Definition());
        Assert.Equal(BodyKind.Raw, applied.Body.Kind);
        Assert.Equal("{\"a\":1}", applied.Body.Text);
    }

    [Fact]
    public async Task Urlencoded_body_is_editable()
    {
        var original = Definition(body: RequestBody.UrlEncoded([new("a", "1"), new("b", "2")]));
        var (result, request) = await Run(
            "const u = yaapic.request.body.urlencoded; u.add({ key: 'c', value: '3' }); u.upsert({ key: 'a', value: 'x' }); u.remove('b'); console.log(JSON.stringify(u.toObject()));",
            original);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("{\"a\":\"x\",\"c\":\"3\"}", Assert.Single(result.Console).Message);
        Assert.Equal([new("a", "x"), new("c", "3")], request.ApplyTo(original).Body.Fields.Select(f => new KeyValueEntry(f.Key, f.Value)));
    }

    [Fact]
    public async Task Formdata_is_read_only_and_its_mode_cannot_be_set()
    {
        var original = Definition(body: RequestBody.FormData([new("f", "v")]));
        Assert.Equal("[\"formdata\",{\"f\":\"v\"}]", await Eval("[yaapic.request.body.mode, yaapic.request.body.formdata.toObject()]", original));

        var (result, request) = await Run(
            "const out = []; for (const f of [() => yaapic.request.body.formdata.add({ key: 'x', value: 'y' }), () => { yaapic.request.body.mode = 'formdata'; }, () => { yaapic.request.body.mode = 'binary'; }, () => { yaapic.request.body.mode = 'bogus'; }]) { try { f(); } catch (e) { out.push(e.message); } } console.log(out.join('|'));",
            original);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(
            "yaapic.request.body.formdata is read-only|yaapic.request.body mode 'formdata' cannot be set from a script|yaapic.request.body mode 'binary' cannot be set from a script|yaapic.request.body mode 'bogus' is not valid",
            Assert.Single(result.Console).Message);
        Assert.Equal("formdata", request.BodyMode);
    }

    [Fact]
    public async Task Every_mutation_in_post_response_throws_and_leaves_the_request_untouched()
    {
        var (result, request) = await Run(
            """
            const attempts = [
              () => { yaapic.request.method = 'POST'; },
              () => { yaapic.request.url = 'http://x'; },
              () => yaapic.request.url.update('http://x'),
              () => yaapic.request.url.query.add({ key: 'a', value: 'b' }),
              () => yaapic.request.headers.add({ key: 'a', value: 'b' }),
              () => yaapic.request.headers.remove('Accept'),
              () => { yaapic.request.body.mode = 'raw'; },
              () => { yaapic.request.body.raw = 'x'; },
              () => yaapic.request.body.urlencoded.add({ key: 'a', value: 'b' }),
            ];
            const messages = new Set();
            for (const attempt of attempts) {
              try { attempt(); messages.add('no error'); } catch (e) { messages.add(e.message); }
            }
            console.log([...messages].join('|'));
            """,
            response: Response("{}"),
            readOnly: true);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("yaapic.request is read-only in post-response scripts", Assert.Single(result.Console).Message);
        var untouched = ScriptRequest.FromDefinition(Definition(), true);
        Assert.Equal(untouched.Url, request.Url);
        Assert.Equal(untouched.Method, request.Method);
        Assert.Equal(untouched.Headers, request.Headers);
        Assert.Equal(untouched.BodyMode, request.BodyMode);
    }

    [Fact]
    public async Task Reading_the_request_works_in_post_response()
    {
        Assert.Equal("[\"GET\",\"application/json\"]", await Eval("[yaapic.request.method, yaapic.request.headers.get('accept')]", response: Response("{}")));
    }

    [Fact]
    public async Task Request_auth_and_certificate_are_unsupported()
    {
        var (result, _) = await Run("try { yaapic.request.auth.type; } catch (e) { console.log(e.message); } try { yaapic.request.certificate; yaapic.request.certificate.x; } catch (e) { console.log(e.message); }");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(
            ["yaapic.request.auth.type is not supported in Yaapic", "yaapic.request.certificate.x is not supported in Yaapic"],
            result.Console.Select(c => c.Message));
    }

    [Fact]
    public async Task Response_exposes_a_json_body()
    {
        var response = Response("{\"id\":7,\"tags\":[\"a\"]}");
        Assert.Equal(
            "[200,\"OK\",\"OK\",42,21,{\"body\":21,\"header\":0,\"total\":21},\"{\\\"id\\\":7,\\\"tags\\\":[\\\"a\\\"]}\",{\"id\":7,\"tags\":[\"a\"]}]",
            await Eval("[yaapic.response.code, yaapic.response.status, yaapic.response.reason(), yaapic.response.responseTime, yaapic.response.responseSize, yaapic.response.size(), yaapic.response.text(), yaapic.response.json()]", response: response));
        Assert.Equal("14", await Eval("yaapic.response.json((k, v) => (k === 'id' ? v * 2 : v)).id", response: response));
    }

    [Fact]
    public async Task Response_headers_are_read_only_keep_duplicates_and_return_the_first()
    {
        var response = Response("{}");
        Assert.Equal(
            "[\"application/json\",\"a=1\",true,3,\"yaapic.response.headers is read-only\"]",
            await Eval(
                "(() => { const h = yaapic.response.headers; let err; try { h.add({ key: 'a', value: 'b' }); } catch (e) { err = e.message; } return [h.get('content-type'), h.get('SET-COOKIE'), h.has('Content-Type'), h.count(), err]; })()",
                response: response));
    }

    [Fact]
    public async Task Response_text_and_empty_and_binary_bodies()
    {
        Assert.Equal("[\"hello\",404,\"Not Found\"]", await Eval("[yaapic.response.text(), yaapic.response.code, yaapic.response.status]", response: Response("hello", "text/plain", 404)));
        Assert.Equal("[\"\",0]", await Eval("[yaapic.response.text(), yaapic.response.size().body]", response: Response("", "text/plain")));
        Assert.Equal("[\"\",2048]", await Eval("[yaapic.response.text(), yaapic.response.responseSize]", response: Response("", "image/png", padding: 2048)));
    }

    [Theory]
    [InlineData("", "text/plain")]
    [InlineData("not json", "text/plain")]
    [InlineData("{\"a\":", "application/json")]
    public async Task Invalid_json_throws_with_the_parser_message(string text, string contentType)
    {
        var (result, _) = await Run(
            "try { yaapic.response.json(); } catch (e) { console.log(e.message); }",
            response: Response(text, contentType),
            readOnly: true);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.StartsWith("Response body is not valid JSON: ", Assert.Single(result.Console).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Response_in_pre_request_throws()
    {
        var (result, _) = await Run("yaapic.response.code;");

        Assert.False(result.Succeeded);
        Assert.Equal("yaapic.response is only available in post-response scripts", result.Error?.Message);
    }

    internal sealed class TestHost(ScriptRequest request, ScriptResponse? response, ScriptInfo info) : IScriptHost
    {
        public ScriptInfo Info => info;

        public ScriptRequest Request => request;

        public ScriptResponse? Response => response;

        public IReadOnlyList<ScriptCookie> ResponseCookies => [];

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

        public ScriptSendResult Send(ScriptRequest request) => new(null, "not supported");
    }
}
