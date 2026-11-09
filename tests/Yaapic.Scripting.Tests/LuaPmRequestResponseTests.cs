using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.Lua;

namespace Yaapic.Scripting.Tests;

/// <summary>The Lua <c>pm.request</c> and <c>pm.response</c>, on the real Lua engine over a real <see cref="ScriptRequest"/>.</summary>
public sealed class LuaPmRequestResponseTests
{
    // Script errors carry the "chunk:line: " position; the tests compare the message only.
    private const string Helpers = "local function msg(f) local ok, e = pcall(f) return (tostring(e):gsub('^.-:%d+: ', '')) end ";

    private static HttpRequestDefinition Definition(string url = "https://api.example.com:8443/v1/users?page=1&q=a%20b#top", RequestBody? body = null) =>
        PmRequestResponseTests.Definition(url, body);

    private static async Task<(ScriptRunResult Result, ScriptRequest Request)> Run(
        string source,
        HttpRequestDefinition? definition = null,
        ScriptResponse? response = null,
        bool readOnly = false)
    {
        var request = ScriptRequest.FromDefinition(definition ?? Definition(), readOnly);
        PmRequestResponseTests.TestHost host = new(request, response, new(readOnly ? "test" : "prerequest", "Users API", PmRequestResponseTests.RequestId, PmRequestResponseTests.Origin));
        var result = await new LuaEngine().RunAsync(
            new(
                new(ScriptLanguage.Lua, Helpers + source),
                readOnly ? ScriptPhase.PostResponse : ScriptPhase.PreRequest,
                PmRequestResponseTests.Origin,
                host,
                ScriptLimits.Default),
            TestContext.Current.CancellationToken);
        return (result, request);
    }

    private static async Task<string> Eval(string expression, HttpRequestDefinition? definition = null, ScriptResponse? response = null)
    {
        var (result, _) = await Run($"console.log(pm.json.encode({expression}))", definition, response, response is not null);
        Assert.True(result.Succeeded, result.Error?.Message);
        return Assert.Single(result.Console).Message;
    }

    private static ScriptResponse Response(string text, string contentType = "application/json", int code = 200, int padding = 0) =>
        PmRequestResponseTests.Response(text, contentType, code, padding);

    [Fact]
    public async Task Method_is_read_and_set_in_upper_case()
    {
        Assert.Equal("[\"GET\",\"GET\"]", await Eval("{ pm.request.get_method(), pm.request.method }"));
        var (result, request) = await Run("pm.request.set_method('post')");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("POST", request.ApplyTo(Definition()).Method.ToText());
    }

    [Fact]
    public async Task Camel_case_aliases_work()
    {
        var (result, request) = await Run("pm.request.setMethod('put') pm.request.setUrl('http://a.test/x') console.log(pm.request.getMethod(), pm.request.url.toString(), pm.request.url.getHost())");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("PUT http://a.test/x a.test", Assert.Single(result.Console).Message);
        Assert.Equal("PUT", request.ApplyTo(Definition()).Method.ToText());
    }

    [Fact]
    public async Task Plain_field_assignment_is_not_supported()
    {
        var (result, request) = await Run(
            "local out = {} for _, f in ipairs({ function() pm.request.method = 'POST' end, function() pm.request.url = 'x' end, function() pm.request.body.mode = 'raw' end, function() pm.request.url.protocol = 'http' end, function() pm.request.other = 1 end }) do out[#out + 1] = msg(f) end console.log(table.concat(out, '|'))");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(
            "pm.request.method cannot be assigned in Lua; use pm.request.set_method()|pm.request.url cannot be assigned in Lua; use pm.request.set_url()|pm.request.body.mode cannot be assigned in Lua; use pm.request.body.set_mode()|pm.request.url.protocol cannot be assigned in Lua; use pm.request.url.update()|pm.request.other cannot be assigned in Lua",
            Assert.Single(result.Console).Message);
        Assert.Equal("GET", request.Method);
    }

    [Fact]
    public async Task Id_and_name_come_from_the_request_info()
    {
        Assert.Equal($"[\"{PmRequestResponseTests.RequestId}\",\"Users API\"]", await Eval("{ pm.request.id, pm.request.name }"));
    }

    [Fact]
    public async Task Url_exposes_its_parts()
    {
        Assert.Equal("\"https://api.example.com:8443/v1/users?page=1&q=a%20b#top\"", await Eval("pm.request.url.to_string()"));
        Assert.Equal(
            "[\"https\",[\"api\",\"example\",\"com\"],\"8443\",[\"v1\",\"users\"],\"api.example.com\",\"/v1/users\",\"/v1/users?page=1&q=a%20b\",\"page=1&q=a%20b\",\"top\"]",
            await Eval("(function() local u = pm.request.url return { u.protocol, u.host, u.port, u.path, u.get_host(), u.get_path(), u.get_path_with_query(), u.get_query_string(), u.hash } end)()"));
        Assert.Equal(
            "[[{\"key\":\"page\",\"value\":\"1\"},{\"key\":\"q\",\"value\":\"a b\"}],2,\"a b\",true,false,{\"page\":\"1\",\"q\":\"a b\"}]",
            await Eval("(function() local q = pm.request.url.query return { q.all(), q.count(), q.get('q'), q.has('page'), q.has('nope'), q.to_object() } end)()"));
    }

    [Fact]
    public async Task Url_with_variables_survives_untouched()
    {
        const string Url = "{{base}}/users/{{id}}?a={{x}}&b=1#f";
        Assert.Equal($"\"{Url}\"", await Eval("tostring(pm.request.url)", Definition(Url)));
        Assert.Equal(
            "[[\"{{base}}\"],\"\",\"{{x}}\"]",
            await Eval("(function() local u = pm.request.url return { u.host, u.port, u.query.get('a') } end)()", Definition(Url)));
    }

    [Theory]
    [InlineData("https://u:p@h.test:81/a/b?x=1#f", "https|h.test|81|/a/b|x=1|f")]
    [InlineData("h.test", "|h.test||/||")]
    [InlineData("/only/path?q=1", "|||/only/path|q=1|")]
    [InlineData("http://h.test?q=1", "http|h.test||/|q=1|")]
    public async Task Url_splitter_handles_partial_urls(string url, string expected)
    {
        var (result, _) = await Run(
            "local u = pm.request.url console.log(table.concat({ u.protocol, u.get_host(), u.port, u.get_path(), u.get_query_string(), u.hash }, '|'))",
            Definition(url));

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(expected, Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task Url_update_and_set_reach_the_applied_definition()
    {
        var (result, request) = await Run("pm.request.set_url('https://other.test/x?y=2') pm.request.url.update('{{base}}/z?k=v#frag')");

        Assert.True(result.Succeeded, result.Error?.Message);
        var applied = request.ApplyTo(Definition());
        Assert.Equal("{{base}}/z#frag", applied.Url);
        Assert.Equal([new("k", "v")], applied.QueryParams);
    }

    [Fact]
    public async Task Query_add_upsert_and_remove_are_applied_with_variables_and_fragment()
    {
        var (result, request) = await Run(
            "local q = pm.request.url.query q.add{ key = 'new', value = 'a b&c' } q.upsert{ key = 'page', value = 2 } q.upsert{ key = 'tok', value = '{{token}}' } q.remove('q')",
            Definition("{{base}}/v1?page=1&q=x#top"));

        Assert.True(result.Succeeded, result.Error?.Message);
        var applied = request.ApplyTo(Definition("{{base}}/v1?page=1&q=x#top"));
        Assert.Equal("{{base}}/v1#top", applied.Url);
        Assert.Equal([new("page", "2"), new("new", "a b&c"), new("tok", "{{token}}")], applied.QueryParams);
        Assert.Contains("tok={{token}}", request.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Headers_are_case_insensitive_and_editable()
    {
        Assert.Equal(
            "[\"application/json\",true,false,3,{\"Accept\":\"application/json\",\"X-Old\":\"1\"},{\"key\":\"X-Old\",\"value\":\"1\"}]",
            await Eval("(function() local h = pm.request.headers return { h.get('ACCEPT'), h.has('x-old'), h.has('X-Off'), h.count(), h.to_object(), h.idx(2) } end)()"));

        var (result, request) = await Run(
            "local h = pm.request.headers h.add{ key = 'X-New', value = 'n' } h.upsert{ key = 'accept', value = 'text/plain' } h.remove('X-OLD') local keys = {} h.each(function(i) keys[#keys + 1] = i.key end) console.log(table.concat(keys, ','))");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("accept,X-Off,X-New", Assert.Single(result.Console).Message);
        Assert.Equal([new("accept", "text/plain"), new("X-Off", "no", false), new("X-New", "n")], request.ApplyTo(Definition()).Headers);
    }

    [Fact]
    public async Task Invalid_list_items_are_rejected()
    {
        var (result, _) = await Run("console.log(msg(function() pm.request.headers.add('x') end), '|', msg(function() pm.request.headers.get(1) end))");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(
            "pm.request.headers.add: item must be a table with a string key | bad argument #1 to 'get' (string expected, got number)",
            Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task Raw_body_is_set_and_the_mode_switched()
    {
        var (result, request) = await Run("pm.request.body.set_mode('raw') pm.request.body.set_raw('{\"a\":1}') console.log(pm.request.body.get_mode(), pm.request.body.mode, pm.request.body.raw, pm.request.body.to_string())");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("raw raw {\"a\":1} {\"a\":1}", Assert.Single(result.Console).Message);
        var applied = request.ApplyTo(Definition());
        Assert.Equal(BodyKind.Raw, applied.Body.Kind);
        Assert.Equal("{\"a\":1}", applied.Body.Text);
    }

    [Fact]
    public async Task Urlencoded_body_is_editable()
    {
        var original = Definition(body: RequestBody.UrlEncoded([new("a", "1"), new("b", "2")]));
        var (result, request) = await Run(
            "local u = pm.request.body.urlencoded u.add{ key = 'c', value = 3 } u.upsert{ key = 'a', value = 'x' } u.remove('b') console.log(pm.json.encode(u.to_object()))",
            original);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("{\"a\":\"x\",\"c\":\"3\"}", Assert.Single(result.Console).Message);
        Assert.Equal([new("a", "x"), new("c", "3")], request.ApplyTo(original).Body.Fields.Select(f => new KeyValueEntry(f.Key, f.Value)));
    }

    [Fact]
    public async Task Formdata_is_read_only_and_its_mode_cannot_be_set()
    {
        var original = Definition(body: RequestBody.FormData([new("f", "v")]));
        Assert.Equal("[\"formdata\",{\"f\":\"v\"}]", await Eval("{ pm.request.body.mode, pm.request.body.formdata.to_object() }", original));

        var (result, request) = await Run(
            "local out = {} for _, f in ipairs({ function() pm.request.body.formdata.add{ key = 'x', value = 'y' } end, function() pm.request.body.set_mode('formdata') end, function() pm.request.body.set_mode('binary') end, function() pm.request.body.set_mode('bogus') end }) do out[#out + 1] = msg(f) end console.log(table.concat(out, '|'))",
            original);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(
            "pm.request.body.formdata is read-only|pm.request.body mode 'formdata' cannot be set from a script|pm.request.body mode 'binary' cannot be set from a script|pm.request.body mode 'bogus' is not valid",
            Assert.Single(result.Console).Message);
        Assert.Equal("formdata", request.BodyMode);
    }

    [Fact]
    public async Task Every_mutation_in_post_response_fails_and_leaves_the_request_untouched()
    {
        var (result, request) = await Run(
            """
            local attempts = {
              function() pm.request.set_method('POST') end,
              function() pm.request.set_url('http://x') end,
              function() pm.request.url.update('http://x') end,
              function() pm.request.url.query.add{ key = 'a', value = 'b' } end,
              function() pm.request.headers.add{ key = 'a', value = 'b' } end,
              function() pm.request.headers.remove('Accept') end,
              function() pm.request.body.set_mode('raw') end,
              function() pm.request.body.set_raw('x') end,
              function() pm.request.body.urlencoded.add{ key = 'a', value = 'b' } end,
            }
            local seen, order = {}, {}
            for _, attempt in ipairs(attempts) do
              local m = msg(attempt)
              if not seen[m] then seen[m] = true order[#order + 1] = m end
            end
            console.log(table.concat(order, '|'))
            """,
            response: Response("{}"),
            readOnly: true);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("pm.request is read-only in post-response scripts", Assert.Single(result.Console).Message);
        var untouched = ScriptRequest.FromDefinition(Definition(), true);
        Assert.Equal(untouched.Url, request.Url);
        Assert.Equal(untouched.Method, request.Method);
        Assert.Equal(untouched.Headers, request.Headers);
        Assert.Equal(untouched.BodyMode, request.BodyMode);
    }

    [Fact]
    public async Task Reading_the_request_works_in_post_response()
    {
        Assert.Equal("[\"GET\",\"application/json\"]", await Eval("{ pm.request.get_method(), pm.request.headers.get('accept') }", response: Response("{}")));
    }

    [Fact]
    public async Task Request_auth_and_certificate_are_unsupported()
    {
        var (result, _) = await Run("console.log(msg(function() return pm.request.auth.type end)) console.log(msg(function() return pm.request.certificate.x end))");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(
            ["pm.request.auth.type is not supported in Yaapic", "pm.request.certificate.x is not supported in Yaapic"],
            result.Console.Select(c => c.Message));
    }

    [Fact]
    public async Task Response_exposes_a_json_body()
    {
        var response = Response("{\"id\":7,\"tags\":[\"a\"]}");
        Assert.Equal(
            "[200,\"OK\",\"OK\",42,21,{\"body\":21,\"header\":0,\"total\":21},\"{\\\"id\\\":7,\\\"tags\\\":[\\\"a\\\"]}\",{\"id\":7,\"tags\":[\"a\"]}]",
            await Eval("{ pm.response.code, pm.response.status, pm.response.reason(), pm.response.response_time, pm.response.responseSize, pm.response.size(), pm.response.text(), pm.response.json() }", response: response));
    }

    [Fact]
    public async Task Response_headers_are_read_only_keep_duplicates_and_return_the_first()
    {
        Assert.Equal(
            "[\"application/json\",\"a=1\",true,3,\"pm.response.headers is read-only\"]",
            await Eval(
                "(function() local h = pm.response.headers return { h.get('content-type'), h.get('SET-COOKIE'), h.has('Content-Type'), h.count(), msg(function() h.add{ key = 'a', value = 'b' } end) } end)()",
                response: Response("{}")));
    }

    [Fact]
    public async Task Response_text_and_empty_and_binary_bodies()
    {
        Assert.Equal("[\"hello\",404,\"Not Found\"]", await Eval("{ pm.response.text(), pm.response.code, pm.response.status }", response: Response("hello", "text/plain", 404)));
        Assert.Equal("[\"\",0]", await Eval("{ pm.response.text(), pm.response.size().body }", response: Response("", "text/plain")));
        Assert.Equal("[\"\",2048]", await Eval("{ pm.response.text(), pm.response.response_size }", response: Response("", "image/png", padding: 2048)));
    }

    [Theory]
    [InlineData("", "text/plain")]
    [InlineData("not json", "text/plain")]
    [InlineData("{\"a\":", "application/json")]
    public async Task Invalid_json_fails_with_the_parser_message(string text, string contentType)
    {
        var (result, _) = await Run(
            "console.log(msg(function() return pm.response.json() end))",
            response: Response(text, contentType),
            readOnly: true);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.StartsWith("Response body is not valid JSON: ", Assert.Single(result.Console).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Response_fields_are_read_only()
    {
        var (result, _) = await Run("console.log(msg(function() pm.response.code = 1 end))", response: Response("{}"), readOnly: true);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("pm.response.code cannot be assigned in Lua", Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task Response_in_pre_request_fails()
    {
        var (result, _) = await Run("local c = pm.response.code");

        Assert.False(result.Succeeded);
        Assert.EndsWith("pm.response is only available in post-response scripts", result.Error?.Message, StringComparison.Ordinal);
    }
}
