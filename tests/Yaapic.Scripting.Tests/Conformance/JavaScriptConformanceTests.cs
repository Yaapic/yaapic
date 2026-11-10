using Yaapic.Core.Scripting;
using static Yaapic.Scripting.Tests.Conformance.ConformanceRunner;

namespace Yaapic.Scripting.Tests.Conformance;

/// <summary>
/// Typical Postman scripts run end to end through <see cref="ScriptedRequestSender"/> and the real QuickJS engine.
/// Each case is one idiom as the Postman documentation writes it; <c>docs/scripting-compatibility.md</c> lists the same surface.
/// </summary>
public sealed class JavaScriptConformanceTests
{
    private static readonly Case[] Cases =
    [
        // Idiom: pm.test + pm.response.to.have.status(200).
        new("status-code") { Post = "pm.test('Status code is 200', () => { pm.response.to.have.status(200); });", Tests = ["Status code is 200|P"] },
        // Idiom: pm.expect(pm.response.code).to.equal(...).
        new("response-code-equal") { Post = "pm.test('code', () => pm.expect(pm.response.code).to.equal(200));", Tests = ["code|P"] },
        // Idiom: status text test via pm.response.status.
        new("status-text") { Post = "pm.test('Status text is OK', () => pm.expect(pm.response.status).to.eql('OK'));", Tests = ["Status text is OK|P"] },
        // Idiom: pm.response.to.have.status('Created').
        new("status-by-name") { Status = 201, Reason = "Created", Post = "pm.test('created', () => pm.response.to.have.status('Created'));", Tests = ["created|P"] },
        // Idiom: "Response time is less than 200ms".
        new("response-time") { Post = "pm.test('Response time is less than 200ms', () => pm.expect(pm.response.responseTime).to.be.below(200));", Tests = ["Response time is less than 200ms|P"] },
        // Idiom: JSON value test with pm.response.json().
        new("json-value") { Post = "pm.test('count', () => pm.expect(pm.response.json().count).to.eql(2));", Tests = ["count|P"] },
        // Idiom: nested value pm.response.json().data[0].id.
        new("json-nested") { Post = "pm.test('first id', () => pm.expect(pm.response.json().data[0].id).to.eql(7));", Tests = ["first id|P"] },
        // Idiom: header presence with pm.response.to.have.header.
        new("header-present") { Post = "pm.test('has header', () => pm.response.to.have.header('Content-Type'));", Tests = ["has header|P"] },
        // Idiom: header value with pm.response.headers.get (case-insensitive name).
        new("header-value") { Post = "pm.test('json', () => pm.expect(pm.response.headers.get('content-type')).to.include('application/json'));", Tests = ["json|P"] },
        // Idiom: content type check with pm.response.to.have.header(name, value).
        new("content-type") { Post = "pm.test('ct', () => pm.response.to.have.header('Content-Type', 'application/json'));", Tests = ["ct|P"] },
        // Idiom: save a token from the JSON body into an environment variable.
        new("save-token") { Post = "pm.environment.set('token', pm.response.json().token);", Changes = ["Environment.token=abc123"] },
        // Idiom: use {{token}} in a request header added by the pre-request script.
        new("use-token") { Pre = "pm.request.headers.add({ key: 'Authorization', value: 'Bearer {{token}}' });", Env = ["token=abc123"], SentHeaders = ["Authorization=Bearer abc123"] },
        // Idiom: save a token and use it in the next send (two sends).
        new("token-chain") { Pre = "pm.request.headers.upsert({ key: 'X-Token', value: '{{token}}' });", Post = "pm.environment.set('token', pm.response.json().token);", Sends = 2, SentHeaders = ["X-Token=abc123"] },
        // Idiom: pm.variables.replaceIn('{{$guid}}').
        new("dynamic-guid") { Pre = "pm.environment.set('ok', String(/^[0-9a-f-]{36}$/.test(pm.variables.replaceIn('{{$guid}}'))));", Changes = ["Environment.ok=true"] },
        // Idiom: pm.variables.replaceIn('{{$timestamp}}').
        new("dynamic-timestamp") { Pre = "pm.environment.set('ok', String(/^\\d{10}$/.test(pm.variables.replaceIn('{{$timestamp}}'))));", Changes = ["Environment.ok=true"] },
        // Idiom: pm.variables.replaceIn('{{$randomInt}}').
        new("dynamic-random-int") { Pre = "pm.environment.set('ok', String(Number.isInteger(Number(pm.variables.replaceIn('{{$randomInt}}')))));", Changes = ["Environment.ok=true"] },
        // Idiom: signature header computed in pre-request from pm.request.url and the raw body (hand-rolled checksum).
        new("signature-header")
        {
            Method = "POST",
            RequestBody = "{\"a\":1}",
            Pre = """
                const s = pm.request.url.toString() + pm.request.body.raw;
                let h = 0;
                for (let i = 0; i < s.length; i++) { h = (h * 31 + s.charCodeAt(i)) >>> 0; }
                pm.request.headers.add({ key: 'X-Signature', value: h.toString(16) });
                """,
            SentHeaders = ["X-Signature=3f88c6b"],
        },
        // Idiom: pm.collectionVariables counter across two sends.
        new("collection-counter") { Post = "pm.collectionVariables.set('n', String(Number(pm.collectionVariables.get('n') || 0) + 1));", Sends = 2, Changes = ["Collection.n=2"] },
        // Idiom: pm.globals.set.
        new("globals-set") { Post = "pm.globals.set('g', 'one');", Changes = ["Globals.g=one"] },
        // Idiom: pm.globals.unset.
        new("globals-unset") { Globals = ["g=one"], Post = "pm.globals.unset('g');", Changes = ["Globals.g=<unset>"] },
        // Idiom: pm.environment.get / has.
        new("environment-get-has") { Env = ["a=1"], Post = "pm.test('env', () => { pm.expect(pm.environment.get('a')).to.eql('1'); pm.expect(pm.environment.has('b')).to.be.false; });", Tests = ["env|P"] },
        // Idiom: legacy postman.setEnvironmentVariable.
        new("legacy-set-environment") { Post = "postman.setEnvironmentVariable('legacy', 'yes');", Changes = ["Environment.legacy=yes"] },
        // Idiom: pm.expect(obj).to.have.property.
        new("expect-property") { Post = "pm.test('prop', () => pm.expect(pm.response.json()).to.have.property('token'));", Tests = ["prop|P"] },
        // Idiom: pm.expect(text).to.include.
        new("expect-include") { Post = "pm.test('body', () => pm.expect(pm.response.text()).to.include('abc123'));", Tests = ["body|P"] },
        // Idiom: pm.expect(x).to.be.an('array').
        new("expect-array") { Post = "pm.test('array', () => pm.expect(pm.response.json().data).to.be.an('array'));", Tests = ["array|P"] },
        // Idiom: pm.expect(list).to.have.lengthOf(2).
        new("expect-length") { Post = "pm.test('length', () => pm.expect(pm.response.json().data).to.have.lengthOf(2));", Tests = ["length|P"] },
        // Idiom: pm.expect(string).to.match(regex).
        new("expect-match") { Post = "pm.test('match', () => pm.expect(pm.response.json().token).to.match(/^abc\\d+$/));", Tests = ["match|P"] },
        // Idiom: pm.expect(code).to.be.oneOf([...]).
        new("expect-one-of") { Post = "pm.test('one of', () => pm.expect(pm.response.code).to.be.oneOf([200, 201]));", Tests = ["one of|P"] },
        // Idiom: pm.expect(obj).to.eql(deep object).
        new("expect-eql") { Post = "pm.test('eql', () => pm.expect(pm.response.json().data[0]).to.eql({ id: 7, name: 'ann' }));", Tests = ["eql|P"] },
        // Idiom: pm.response.to.have.jsonBody(path, value).
        new("response-json-body") { Post = "pm.test('json body', () => pm.response.to.have.jsonBody('count', 2));", Tests = ["json body|P"] },
        // Idiom: pm.response.to.be.ok.
        new("response-ok") { Post = "pm.test('ok', () => pm.response.to.be.ok);", Tests = ["ok|P"] },
        // Idiom: one pm.test per array item with forEach.
        new("foreach-tests") { Post = "pm.response.json().data.forEach(d => pm.test('item ' + d.id, () => pm.expect(d.id).to.be.a('number')));", Tests = ["item 7|P", "item 9|P"] },
        // Idiom: Array.prototype.map over the JSON body.
        new("array-map") { Post = "console.log(pm.response.json().data.map(d => d.id).join(','));", Console = ["7,9"] },
        // Idiom: Array.prototype.reduce to sum values.
        new("array-reduce") { Post = "console.log(pm.response.json().data.reduce((sum, d) => sum + d.id, 0));", Console = ["16"] },
        // Idiom: try/catch around pm.response.json() on a non-JSON body.
        new("json-try-catch") { ReplyBody = "not json", Post = "try { pm.response.json(); } catch (e) { console.log('bad json'); }", Console = ["bad json"] },
        // Idiom: JSON.parse(pm.response.text()).
        new("json-parse-text") { Post = "console.log(JSON.parse(pm.response.text()).token);", Console = ["abc123"] },
        // Idiom: async test with await pm.sendRequest(url).
        new("await-send-request") { Post = "const res = await pm.sendRequest('https://sub.test/x'); pm.test('sub', () => res.to.have.status(200));", Tests = ["sub|P"] },
        // Idiom: callback-style pm.sendRequest.
        new("callback-send-request") { Post = "pm.sendRequest('https://sub.test/x', (err, res) => { pm.environment.set('sub', String(res.code)); });", Changes = ["Environment.sub=200"] },
        // Idiom: console.log of an object.
        new("console-object") { Post = "console.log({ a: 1, b: [1, 2] });", Console = ["{\"a\":1,\"b\":[1,2]}"] },
        // Idiom: template literals.
        new("template-literal") { Post = "console.log(`id ${pm.response.json().data[0].id}`);", Console = ["id 7"] },
        // Idiom: destructuring the JSON body.
        new("destructuring") { Post = "const { count, token } = pm.response.json(); console.log(count, token);", Console = ["2 abc123"] },
        // Idiom: ES classes with private fields.
        new("classes") { Post = "class Acc { #n = 0; add(x) { this.#n += x; return this; } get total() { return this.#n; } } console.log(new Acc().add(2).add(3).total);", Console = ["5"] },
        // Idiom: optional chaining with nullish coalescing.
        new("optional-chaining") { Post = "console.log(pm.response.json().missing?.deep ?? 'none');", Console = ["none"] },
        // Idiom: Array.prototype.flat.
        new("array-flat") { Post = "console.log([[1], [2, [3]]].flat(Infinity).join());", Console = ["1,2,3"] },
        // Idiom: Object.entries.
        new("object-entries") { Post = "console.log(Object.entries({ a: 1, b: 2 }).map(([k, v]) => k + v).join());", Console = ["a1,b2"] },
        // Idiom: Number.prototype.toFixed instead of Intl.
        new("to-fixed") { Post = "console.log((3.14159).toFixed(2));", Console = ["3.14"] },
        // Idiom: pm.info.requestName.
        new("info-request-name") { Post = "console.log(pm.info.requestName);", Console = ["Users API"] },
        // Idiom: pm.request.method and pm.request.url.toString() in a test.
        new("request-method-url") { Post = "console.log(pm.request.method, pm.request.url.toString());", Console = ["GET https://api.example.com/users"] },
        // Idiom: pm.request.headers.upsert in pre-request.
        new("headers-upsert") { Pre = "pm.request.headers.upsert({ key: 'X-Trace', value: 't1' });", SentHeaders = ["X-Trace=t1"] },
        // Idiom: pm.request.url.query.add in pre-request.
        new("url-query-add") { Pre = "pm.request.url.query.add({ key: 'page', value: '2' });", SentUrl = "https://api.example.com/users?page=2" },
        // Idiom: a failing test reports the exact assertion message.
        new("failing-test") { Post = "pm.test('fails', () => pm.expect(pm.response.code).to.equal(201));", Tests = ["fails|F|AssertionError: expected 200 to equal 201"] },
        // Idiom: an error thrown after the response keeps the response.
        new("post-response-error") { Post = "throw new Error('boom');", Error = "boom" },
        // Idiom: a runaway loop in pre-request is stopped by the timeout.
        new("infinite-loop") { Pre = "while (true) {}", TimeoutSeconds = 0.3, Error = "timed out", Aborts = true },
    ];

    /// <summary>Gets the case names, so that every case shows as its own test.</summary>
    public static TheoryData<string> CaseNames => [.. Cases.Select(c => c.Name)];

    /// <summary>Gets the unsupported idioms and the message they must fail with.</summary>
    public static TheoryData<string, string> Unsupported => new()
    {
        // Idiom: pm.execution.setNextRequest.
        { "pm.execution.setNextRequest('x')", "pm.execution.setNextRequest is not supported in Yaapic" },
        // Idiom: pm.iterationData.get.
        { "pm.iterationData.get('a')", "pm.iterationData.get is not supported in Yaapic" },
        // Idiom: pm.vault.get.
        { "pm.vault.get('secret')", "pm.vault.get is not supported in Yaapic" },
        // Idiom: pm.visualizer.set.
        { "pm.visualizer.set('<p></p>')", "pm.visualizer.set is not supported in Yaapic" },
        // Idiom: pm.require('npm:...').
        { "pm.require('npm:lodash')", "pm.require is not supported in Yaapic" },
        // Idiom: pm.request.auth.
        { "pm.request.auth.type", "pm.request.auth.type is not supported in Yaapic" },
        // Idiom: pm.response.to.have.jsonSchema.
        { "pm.response.to.have.jsonSchema({})", "pm.response.to.have.jsonSchema is not supported in Yaapic" },
        // Idiom: pm.cookies.jar() (ADR 0009 words it "not available yet").
        { "pm.cookies.jar()", "The cookie jar is not available in Yaapic yet" },
        // Idiom: require('crypto-js').
        { "require('crypto-js')", "require is not supported in Yaapic" },
        // Idiom: CryptoJS global.
        { "CryptoJS.MD5('a')", "CryptoJS.MD5 is not supported in Yaapic" },
        // Idiom: postman.setNextRequest.
        { "postman.setNextRequest('x')", "postman.setNextRequest is not supported in Yaapic" },
    };

    /// <summary>Runs the supported case with the given name and checks every expectation it declares.</summary>
    /// <param name="name">The case name.</param>
    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Supported_idiom_behaves_as_in_Postman(string name) => ConformanceRunner.Verify(Cases.Single(x => x.Name == name));

    /// <summary>Checks that an unsupported Postman member fails with its documented message, never silently.</summary>
    /// <param name="script">The post-response script.</param>
    /// <param name="message">The message the run must fail with.</param>
    [Theory]
    [MemberData(nameof(Unsupported))]
    public async Task Unsupported_member_fails_with_a_clear_message(string script, string message)
    {
        var (report, _) = await ConformanceRunner.Run(new("unsupported"), post: script);

        var run = Assert.Single(report.Runs, r => !r.Succeeded);
        Assert.Contains(message, run.Error!.Message, StringComparison.Ordinal);
        Assert.NotNull(report.Report);
    }

    /// <summary>Guards the size of the suite, as the card requires at least 40 supported and 11 unsupported cases.</summary>
    [Fact]
    public void Suite_has_enough_cases()
    {
        Assert.True(Cases.Length >= 40);
        Assert.True(Unsupported.Count >= 11);
    }
}
