using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using static Yaapic.Scripting.Tests.Conformance.ConformanceRunner;

namespace Yaapic.Scripting.Tests.Conformance;

/// <summary>
/// The Lua twin of <see cref="JavaScriptConformanceTests"/>: typical scripts run end to end through
/// <see cref="ScriptedRequestSender"/> and the real Lua engine. <c>docs/scripting-compatibility.md</c> lists the same surface.
/// </summary>
public sealed class LuaConformanceTests
{
    private const string Json = """{"data":[{"id":7,"name":"ann"},{"id":9,"name":"bob"}],"token":"abc123","count":2,"none":null}""";

    private static Case Lua(string name) => new(name) { Language = ScriptLanguage.Lua, ReplyBody = Json };

    private static readonly Case[] Cases =
    [
        // Idiom: status test.
        Lua("status-code") with { Post = "yaapic.test('Status code is 200', function() yaapic.response.to.have.status(200) end)", Tests = ["Status code is 200|P"] },
        // Idiom: equality on the status code.
        Lua("response-code-equal") with { Post = "yaapic.test('code', function() yaapic.expect(yaapic.response.code).to.equal(200) end)", Tests = ["code|P"] },
        // Idiom: status text.
        Lua("status-by-name") with { Status = 201, Reason = "Created", Post = "yaapic.test('created', function() yaapic.response.to.have.status('Created') end)", Tests = ["created|P"] },
        // Idiom: response time budget.
        Lua("response-time") with { Post = "yaapic.test('fast', function() yaapic.expect(yaapic.response.response_time).to.be.below(200) end)", Tests = ["fast|P"] },
        // Idiom: header presence and value, case-insensitive.
        Lua("header-value") with { Post = "yaapic.test('json', function() yaapic.expect(yaapic.response.headers.get('content-type')).to.include('application/json') end) yaapic.test('has', function() yaapic.response.to.have.header('Content-Type', 'application/json') end)", Tests = ["json|P", "has|P"] },
        // Idiom: JSON value and nested tables (1-based).
        Lua("json-nested") with { Post = "local body = yaapic.response.json() yaapic.test('values', function() yaapic.expect(body.count).to.eql(2) yaapic.expect(body.data[1].id).to.eql(7) end)", Tests = ["values|P"] },
        // Idiom: yaapic.null for JSON null.
        Lua("json-null") with { Post = "yaapic.test('null', function() yaapic.expect(yaapic.response.json().none).to.equal(yaapic.null) yaapic.expect(yaapic.response.json().none).to.is_null() end)", Tests = ["null|P"] },
        // Idiom: arrays carry their length in n.
        Lua("json-array-length") with { Post = "local data = yaapic.response.json().data yaapic.test('length', function() yaapic.expect(data).to.have.length(2) end) console.log(#data, data.n)", Tests = ["length|P"], Console = ["2 2"] },
        // Idiom: whole-object deep equality.
        Lua("expect-eql") with { Post = "yaapic.test('eql', function() yaapic.expect(yaapic.response.json().data[1]).to.eql({ id = 7, name = 'ann' }) end)", Tests = ["eql|P"] },
        // Idiom: chained assertions with also and is_not.
        Lua("expect-chain") with { Post = "yaapic.test('chain', function() yaapic.expect(5).to.be.above(1).also.below(9) yaapic.expect('abc').to.is_not.equal('abd') end)", Tests = ["chain|P"] },
        // Idiom: have.json_body with a 0-based path.
        Lua("response-json-body") with { Post = "yaapic.test('body', function() yaapic.response.to.have.json_body('data[0].id', 7) end)", Tests = ["body|P"] },
        // Idiom: zero-argument response flags.
        Lua("response-ok") with { Post = "yaapic.test('ok', function() yaapic.response.to.be.ok() yaapic.response.to.be.json() end)", Tests = ["ok|P"] },
        // Idiom: save a token, then use it.
        Lua("save-token") with { Post = "yaapic.environment.set('token', yaapic.response.json().token)", Changes = ["Environment.token=abc123"] },
        // Idiom: header built from a variable.
        Lua("use-token") with { Pre = "yaapic.request.headers.add({ key = 'Authorization', value = 'Bearer {{token}}' })", Env = ["token=abc123"], SentHeaders = ["Authorization=Bearer abc123"] },
        // Idiom: the token saved by one send is sent by the next.
        Lua("token-chain") with { Pre = "yaapic.request.headers.upsert({ key = 'X-Token', value = '{{token}}' })", Post = "yaapic.environment.set('token', yaapic.response.json().token)", Sends = 2, SentHeaders = ["X-Token=abc123"] },
        // Idiom: dynamic variables through replace_in.
        Lua("dynamic-guid") with { Pre = "local g = yaapic.variables.replace_in('{{$guid}}') yaapic.environment.set('ok', tostring(#g == 36 and g:match('^[0-9a-f-]+$') ~= nil))", Changes = ["Environment.ok=true"] },
        // Idiom: timestamp.
        Lua("dynamic-timestamp") with { Pre = "yaapic.environment.set('ok', tostring(yaapic.variables.replace_in('{{$timestamp}}'):match('^%d%d%d%d%d%d%d%d%d%d$') ~= nil))", Changes = ["Environment.ok=true"] },
        // Idiom: signature header from string.format and a hand-rolled checksum.
        Lua("signature-header") with
        {
            Method = "POST",
            RequestBody = "{\"a\":1}",
            Pre = """
                local s = yaapic.request.url.to_string() .. yaapic.request.body.raw
                local h = 0
                for i = 1, #s do h = (h * 31 + s:byte(i)) % 4294967296 end
                yaapic.request.headers.add({ key = 'X-Signature', value = string.format('%x', h) })
                """,
            SentHeaders = ["X-Signature=3f88c6b"],
        },
        // Idiom: counter in collection variables across two sends.
        Lua("collection-counter") with { Post = "yaapic.collection_variables.set('n', tostring(tonumber(yaapic.collection_variables.get('n') or 0) + 1))", Sends = 2, Changes = ["Collection.n=2"] },
        // Idiom: globals set and unset.
        Lua("globals-set") with { Post = "yaapic.globals.set('g', 'one')", Changes = ["Globals.g=one"] },
        Lua("globals-unset") with { Globals = ["g=one"], Post = "yaapic.globals.unset('g')", Changes = ["Globals.g=<unset>"] },
        // Idiom: pcall around json() on a non-JSON body.
        Lua("json-pcall") with { ReplyBody = "not json", Post = "local ok = pcall(yaapic.response.json) console.log(ok)", Console = ["false"] },
        // Idiom: for, ipairs, pairs and table.sort.
        Lua("loops") with { Post = "local t = {} for _, d in ipairs(yaapic.response.json().data) do t[#t + 1] = d.name end table.sort(t, function(a, b) return a > b end) console.log(table.concat(t, ',')) local n = 0 for _ in pairs(yaapic.response.json()) do n = n + 1 end console.log(n)", Console = ["bob,ann", "4"] },
        // Idiom: one test per item.
        Lua("foreach-tests") with { Post = "for _, d in ipairs(yaapic.response.json().data) do yaapic.test('item ' .. d.id, function() yaapic.expect(d.id).to.be.a('number') end) end", Tests = ["item 7|P", "item 9|P"] },
        // Idiom: string methods and patterns.
        Lua("string-patterns") with { Post = "local s = 'Hello World' console.log(s:upper(), s:lower(), s:sub(1, 5), (s:gsub('o', '0')), s:match('(%a+) (%a+)'))", Console = ["HELLO WORLD hello world Hello Hell0 W0rld Hello World"] },
        // Idiom: integers and floats.
        Lua("integer-float") with { Post = "console.log(7 // 2, 7 / 2, math.type(1), math.type(1.0), 3 | 4, 2 ^ 10)", Console = ["3 3.5 integer float 7 1024.0"] },
        // Idiom: utf8 library.
        Lua("utf8") with { Post = "local s = 'h\\u{e9}llo' console.log(utf8.len(s), #s, utf8.char(233, 8364))", Console = ["5 6 é€"] },
        // Idiom: closures and varargs.
        Lua("closures-varargs") with { Post = "local function counter() local n = 0 return function() n = n + 1 return n end end local c = counter() c() console.log(c(), select('#', 1, nil, 3))", Console = ["2 3"] },
        // Idiom: console.log of a table.
        Lua("console-table") with { Post = "console.log({ a = 1, b = { 1, 2 } })", Console = ["{ a = 1, b = { 1, 2 } }"] },
        // Idiom: yaapic.sendRequest returns res, err.
        Lua("send-request") with { Post = "local res, err = yaapic.sendRequest('https://sub.test/x') yaapic.test('sub', function() yaapic.expect(err).to.is_nil() res.to.have.status(200) end)", Tests = ["sub|P"] },
        // Idiom: yaapic.sendRequest with a callback runs before it returns.
        Lua("send-request-callback") with { Post = "yaapic.sendRequest('https://sub.test/x', function(err, res) yaapic.environment.set('sub', tostring(res.code)) end)", Changes = ["Environment.sub=200"] },
        // Idiom: info and request accessors.
        Lua("info-request") with { Post = "console.log(yaapic.info.request_name, yaapic.request.method, yaapic.request.url.to_string())", Console = ["Users API GET https://api.example.com/users"] },
        // Idiom: query parameter added in pre-request.
        Lua("url-query-add") with { Pre = "yaapic.request.url.query.add({ key = 'page', value = '2' })", SentUrl = "https://api.example.com/users?page=2" },
        // Idiom: a failing test reports the same message as JavaScript.
        Lua("failing-test") with { Post = "yaapic.test('fails', function() yaapic.expect(yaapic.response.code).to.equal(201) end)", Tests = ["fails|F|AssertionError: expected 200 to equal 201"] },
        // Idiom: an error after the response keeps the response.
        Lua("post-response-error") with { Post = "error('boom')", Error = "boom" },
        // Idiom: an error in pre-request aborts the send.
        Lua("pre-request-error") with { Pre = "error('stop')", Error = "stop", Aborts = true },
        // Idiom: an infinite loop is stopped by the timeout.
        Lua("infinite-loop") with { Pre = "while true do end", TimeoutSeconds = 0.3, Error = "timed out", Aborts = true },
    ];

    /// <summary>Gets the case names, so that every case shows as its own test.</summary>
    public static TheoryData<string> CaseNames => [.. Cases.Select(c => c.Name)];

    /// <summary>Gets the unsupported idioms and the message they must fail with.</summary>
    public static TheoryData<string, string> Unsupported => new()
    {
        { "yaapic.execution.set_next_request('x')", "yaapic.execution.set_next_request is not supported in Yaapic" },
        { "yaapic.iterationData.get('a')", "yaapic.iterationData.get is not supported in Yaapic" },
        { "yaapic.vault.get('secret')", "yaapic.vault.get is not supported in Yaapic" },
        { "yaapic.visualizer.set('<p></p>')", "yaapic.visualizer.set is not supported in Yaapic" },
        { "yaapic.require('npm:lodash')", "yaapic.require is not supported in Yaapic" },
        { "local _ = yaapic.request.auth.type", "yaapic.request.auth.type is not supported in Yaapic" },
        { "yaapic.response.to.have.json_schema({})", "is not supported in Yaapic" },
        { "yaapic.cookies.jar()", "The cookie jar is not available in Yaapic yet" },
        { "require('x')", "attempt to call a nil value (global 'require')" },
        { "os.execute('dir')", "attempt to index a nil value (global 'os')" },
        { "io.open('f')", "attempt to index a nil value (global 'io')" },
        { "yaapic.expect(1).to.be.instance_of(Object)", "instance_of is not supported" },
    };

    /// <summary>Runs the supported case with the given name and checks every expectation it declares.</summary>
    /// <param name="name">The case name.</param>
    /// <returns>A task that completes when the case has been checked.</returns>
    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Supported_idiom_behaves_as_in_Postman(string name) => Verify(Cases.Single(x => x.Name == name));

    /// <summary>Checks that an unsupported member fails with its documented message, never silently.</summary>
    /// <param name="script">The post-response script.</param>
    /// <param name="message">The message the run must fail with.</param>
    /// <returns>A task that completes when the case has been checked.</returns>
    [Theory]
    [MemberData(nameof(Unsupported))]
    public async Task Unsupported_member_fails_with_a_clear_message(string script, string message)
    {
        var (report, _) = await Run(Lua("unsupported"), post: script);

        var run = Assert.Single(report.Runs, r => !r.Succeeded);
        Assert.Contains(message, run.Error!.Message, StringComparison.Ordinal);
        Assert.NotNull(report.Report);
    }

    /// <summary>Runs a collection JavaScript script, a Lua folder script, a Lua request script and a JavaScript request script in one send.</summary>
    /// <returns>A task that completes when the chain has been checked.</returns>
    [Fact]
    public async Task Mixed_language_chain_shares_variables_and_the_response()
    {
        var (report, transport) = await Run(new("mixed")
        {
            Chain =
            [
                (ScriptLanguage.JavaScript, "Collection", "yaapic.variables.set('who', 'js');", ""),
                (ScriptLanguage.Lua, "Folder", "", "yaapic.test('folder sees the response', function() yaapic.expect(yaapic.response.json().count).to.eql(2) end) yaapic.variables.set('seen', 'lua')"),
                (ScriptLanguage.Lua, "Users API", "yaapic.request.headers.add({ key = 'X-Who', value = yaapic.variables.get('who') })", ""),
                (ScriptLanguage.JavaScript, "Users API", "", "yaapic.test('request sees the Lua variable', () => yaapic.expect(yaapic.variables.get('seen')).to.eql('lua'));"),
            ],
        });

        Assert.All(report.Runs, r => Assert.True(r.Succeeded, r.Error?.Message));
        Assert.Equal(["folder sees the response|P", "request sees the Lua variable|P"], report.Tests.Select(t => t.Passed ? $"{t.Name}|P" : $"{t.Name}|F"));
        Assert.Contains(transport.Requests[^1].Headers, h => h.Key == "X-Who" && h.Value == "js");
    }

    /// <summary>Guards the size of the suite, as the card requires at least 30 supported and 10 unsupported cases.</summary>
    [Fact]
    public void Suite_has_enough_cases()
    {
        Assert.True(Cases.Length >= 30);
        Assert.True(Unsupported.Count >= 10);
    }
}
