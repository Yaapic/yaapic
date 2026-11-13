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
        // Idiom: yaapic.test + yaapic.response.to.have.status(200).
        new("status-code") { Post = "yaapic.test('Status code is 200', () => { yaapic.response.to.have.status(200); });", Tests = ["Status code is 200|P"] },
        // Idiom: yaapic.expect(yaapic.response.code).to.equal(...).
        new("response-code-equal") { Post = "yaapic.test('code', () => yaapic.expect(yaapic.response.code).to.equal(200));", Tests = ["code|P"] },
        // Idiom: status text test via yaapic.response.status.
        new("status-text") { Post = "yaapic.test('Status text is OK', () => yaapic.expect(yaapic.response.status).to.eql('OK'));", Tests = ["Status text is OK|P"] },
        // Idiom: yaapic.response.to.have.status('Created').
        new("status-by-name") { Status = 201, Reason = "Created", Post = "yaapic.test('created', () => yaapic.response.to.have.status('Created'));", Tests = ["created|P"] },
        // Idiom: "Response time is less than 200ms".
        new("response-time") { Post = "yaapic.test('Response time is less than 200ms', () => yaapic.expect(yaapic.response.responseTime).to.be.below(200));", Tests = ["Response time is less than 200ms|P"] },
        // Idiom: JSON value test with yaapic.response.json().
        new("json-value") { Post = "yaapic.test('count', () => yaapic.expect(yaapic.response.json().count).to.eql(2));", Tests = ["count|P"] },
        // Idiom: nested value yaapic.response.json().data[0].id.
        new("json-nested") { Post = "yaapic.test('first id', () => yaapic.expect(yaapic.response.json().data[0].id).to.eql(7));", Tests = ["first id|P"] },
        // Idiom: header presence with yaapic.response.to.have.header.
        new("header-present") { Post = "yaapic.test('has header', () => yaapic.response.to.have.header('Content-Type'));", Tests = ["has header|P"] },
        // Idiom: header value with yaapic.response.headers.get (case-insensitive name).
        new("header-value") { Post = "yaapic.test('json', () => yaapic.expect(yaapic.response.headers.get('content-type')).to.include('application/json'));", Tests = ["json|P"] },
        // Idiom: content type check with yaapic.response.to.have.header(name, value).
        new("content-type") { Post = "yaapic.test('ct', () => yaapic.response.to.have.header('Content-Type', 'application/json'));", Tests = ["ct|P"] },
        // Idiom: save a token from the JSON body into an environment variable.
        new("save-token") { Post = "yaapic.environment.set('token', yaapic.response.json().token);", Changes = ["Environment.token=abc123"] },
        // Idiom: use {{token}} in a request header added by the pre-request script.
        new("use-token") { Pre = "yaapic.request.headers.add({ key: 'Authorization', value: 'Bearer {{token}}' });", Env = ["token=abc123"], SentHeaders = ["Authorization=Bearer abc123"] },
        // Idiom: save a token and use it in the next send (two sends).
        new("token-chain") { Pre = "yaapic.request.headers.upsert({ key: 'X-Token', value: '{{token}}' });", Post = "yaapic.environment.set('token', yaapic.response.json().token);", Sends = 2, SentHeaders = ["X-Token=abc123"] },
        // Idiom: yaapic.variables.replaceIn('{{$guid}}').
        new("dynamic-guid") { Pre = "yaapic.environment.set('ok', String(/^[0-9a-f-]{36}$/.test(yaapic.variables.replaceIn('{{$guid}}'))));", Changes = ["Environment.ok=true"] },
        // Idiom: yaapic.variables.replaceIn('{{$timestamp}}').
        new("dynamic-timestamp") { Pre = "yaapic.environment.set('ok', String(/^\\d{10}$/.test(yaapic.variables.replaceIn('{{$timestamp}}'))));", Changes = ["Environment.ok=true"] },
        // Idiom: yaapic.variables.replaceIn('{{$randomInt}}').
        new("dynamic-random-int") { Pre = "yaapic.environment.set('ok', String(Number.isInteger(Number(yaapic.variables.replaceIn('{{$randomInt}}')))));", Changes = ["Environment.ok=true"] },
        // Idiom: signature header computed in pre-request from yaapic.request.url and the raw body (hand-rolled checksum).
        new("signature-header")
        {
            Method = "POST",
            RequestBody = "{\"a\":1}",
            Pre = """
                const s = yaapic.request.url.toString() + yaapic.request.body.raw;
                let h = 0;
                for (let i = 0; i < s.length; i++) { h = (h * 31 + s.charCodeAt(i)) >>> 0; }
                yaapic.request.headers.add({ key: 'X-Signature', value: h.toString(16) });
                """,
            SentHeaders = ["X-Signature=3f88c6b"],
        },
        // Idiom: yaapic.collectionVariables counter across two sends.
        new("collection-counter") { Post = "yaapic.collectionVariables.set('n', String(Number(yaapic.collectionVariables.get('n') || 0) + 1));", Sends = 2, Changes = ["Collection.n=2"] },
        // Idiom: yaapic.globals.set.
        new("globals-set") { Post = "yaapic.globals.set('g', 'one');", Changes = ["Globals.g=one"] },
        // Idiom: yaapic.globals.unset.
        new("globals-unset") { Globals = ["g=one"], Post = "yaapic.globals.unset('g');", Changes = ["Globals.g=<unset>"] },
        // Idiom: yaapic.environment.get / has.
        new("environment-get-has") { Env = ["a=1"], Post = "yaapic.test('env', () => { yaapic.expect(yaapic.environment.get('a')).to.eql('1'); yaapic.expect(yaapic.environment.has('b')).to.be.false; });", Tests = ["env|P"] },
        // Idiom: legacy postman.setEnvironmentVariable.
        new("legacy-set-environment") { Post = "postman.setEnvironmentVariable('legacy', 'yes');", Changes = ["Environment.legacy=yes"] },
        // Idiom: yaapic.expect(obj).to.have.property.
        new("expect-property") { Post = "yaapic.test('prop', () => yaapic.expect(yaapic.response.json()).to.have.property('token'));", Tests = ["prop|P"] },
        // Idiom: yaapic.expect(text).to.include.
        new("expect-include") { Post = "yaapic.test('body', () => yaapic.expect(yaapic.response.text()).to.include('abc123'));", Tests = ["body|P"] },
        // Idiom: yaapic.expect(x).to.be.an('array').
        new("expect-array") { Post = "yaapic.test('array', () => yaapic.expect(yaapic.response.json().data).to.be.an('array'));", Tests = ["array|P"] },
        // Idiom: yaapic.expect(list).to.have.lengthOf(2).
        new("expect-length") { Post = "yaapic.test('length', () => yaapic.expect(yaapic.response.json().data).to.have.lengthOf(2));", Tests = ["length|P"] },
        // Idiom: yaapic.expect(string).to.match(regex).
        new("expect-match") { Post = "yaapic.test('match', () => yaapic.expect(yaapic.response.json().token).to.match(/^abc\\d+$/));", Tests = ["match|P"] },
        // Idiom: yaapic.expect(code).to.be.oneOf([...]).
        new("expect-one-of") { Post = "yaapic.test('one of', () => yaapic.expect(yaapic.response.code).to.be.oneOf([200, 201]));", Tests = ["one of|P"] },
        // Idiom: yaapic.expect(obj).to.eql(deep object).
        new("expect-eql") { Post = "yaapic.test('eql', () => yaapic.expect(yaapic.response.json().data[0]).to.eql({ id: 7, name: 'ann' }));", Tests = ["eql|P"] },
        // Idiom: yaapic.response.to.have.jsonBody(path, value).
        new("response-json-body") { Post = "yaapic.test('json body', () => yaapic.response.to.have.jsonBody('count', 2));", Tests = ["json body|P"] },
        // Idiom: yaapic.response.to.be.ok.
        new("response-ok") { Post = "yaapic.test('ok', () => yaapic.response.to.be.ok);", Tests = ["ok|P"] },
        // Idiom: one yaapic.test per array item with forEach.
        new("foreach-tests") { Post = "yaapic.response.json().data.forEach(d => yaapic.test('item ' + d.id, () => yaapic.expect(d.id).to.be.a('number')));", Tests = ["item 7|P", "item 9|P"] },
        // Idiom: Array.prototype.map over the JSON body.
        new("array-map") { Post = "console.log(yaapic.response.json().data.map(d => d.id).join(','));", Console = ["7,9"] },
        // Idiom: Array.prototype.reduce to sum values.
        new("array-reduce") { Post = "console.log(yaapic.response.json().data.reduce((sum, d) => sum + d.id, 0));", Console = ["16"] },
        // Idiom: try/catch around yaapic.response.json() on a non-JSON body.
        new("json-try-catch") { ReplyBody = "not json", Post = "try { yaapic.response.json(); } catch (e) { console.log('bad json'); }", Console = ["bad json"] },
        // Idiom: JSON.parse(yaapic.response.text()).
        new("json-parse-text") { Post = "console.log(JSON.parse(yaapic.response.text()).token);", Console = ["abc123"] },
        // Idiom: async test with await yaapic.sendRequest(url).
        new("await-send-request") { Post = "const res = await yaapic.sendRequest('https://sub.test/x'); yaapic.test('sub', () => res.to.have.status(200));", Tests = ["sub|P"] },
        // Idiom: callback-style yaapic.sendRequest.
        new("callback-send-request") { Post = "yaapic.sendRequest('https://sub.test/x', (err, res) => { yaapic.environment.set('sub', String(res.code)); });", Changes = ["Environment.sub=200"] },
        // Idiom: console.log of an object.
        new("console-object") { Post = "console.log({ a: 1, b: [1, 2] });", Console = ["{\"a\":1,\"b\":[1,2]}"] },
        // Idiom: template literals.
        new("template-literal") { Post = "console.log(`id ${yaapic.response.json().data[0].id}`);", Console = ["id 7"] },
        // Idiom: destructuring the JSON body.
        new("destructuring") { Post = "const { count, token } = yaapic.response.json(); console.log(count, token);", Console = ["2 abc123"] },
        // Idiom: ES classes with private fields.
        new("classes") { Post = "class Acc { #n = 0; add(x) { this.#n += x; return this; } get total() { return this.#n; } } console.log(new Acc().add(2).add(3).total);", Console = ["5"] },
        // Idiom: optional chaining with nullish coalescing.
        new("optional-chaining") { Post = "console.log(yaapic.response.json().missing?.deep ?? 'none');", Console = ["none"] },
        // Idiom: Array.prototype.flat.
        new("array-flat") { Post = "console.log([[1], [2, [3]]].flat(Infinity).join());", Console = ["1,2,3"] },
        // Idiom: Object.entries.
        new("object-entries") { Post = "console.log(Object.entries({ a: 1, b: 2 }).map(([k, v]) => k + v).join());", Console = ["a1,b2"] },
        // Idiom: Number.prototype.toFixed instead of Intl.
        new("to-fixed") { Post = "console.log((3.14159).toFixed(2));", Console = ["3.14"] },
        // Idiom: yaapic.info.requestName.
        new("info-request-name") { Post = "console.log(yaapic.info.requestName);", Console = ["Users API"] },
        // Idiom: yaapic.request.method and yaapic.request.url.toString() in a test.
        new("request-method-url") { Post = "console.log(yaapic.request.method, yaapic.request.url.toString());", Console = ["GET https://api.example.com/users"] },
        // Idiom: yaapic.request.headers.upsert in pre-request.
        new("headers-upsert") { Pre = "yaapic.request.headers.upsert({ key: 'X-Trace', value: 't1' });", SentHeaders = ["X-Trace=t1"] },
        // Idiom: yaapic.request.url.query.add in pre-request.
        new("url-query-add") { Pre = "yaapic.request.url.query.add({ key: 'page', value: '2' });", SentUrl = "https://api.example.com/users?page=2" },
        // Idiom: a failing test reports the exact assertion message.
        new("failing-test") { Post = "yaapic.test('fails', () => yaapic.expect(yaapic.response.code).to.equal(201));", Tests = ["fails|F|AssertionError: expected 200 to equal 201"] },
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
        // Idiom: yaapic.execution.setNextRequest.
        { "yaapic.execution.setNextRequest('x')", "yaapic.execution.setNextRequest is not supported in Yaapic" },
        // Idiom: yaapic.iterationData.get.
        { "yaapic.iterationData.get('a')", "yaapic.iterationData.get is not supported in Yaapic" },
        // Idiom: yaapic.vault.get.
        { "yaapic.vault.get('secret')", "yaapic.vault.get is not supported in Yaapic" },
        // Idiom: yaapic.visualizer.set.
        { "yaapic.visualizer.set('<p></p>')", "yaapic.visualizer.set is not supported in Yaapic" },
        // Idiom: yaapic.require('npm:...').
        { "yaapic.require('npm:lodash')", "yaapic.require is not supported in Yaapic" },
        // Idiom: yaapic.request.auth.
        { "yaapic.request.auth.type", "yaapic.request.auth.type is not supported in Yaapic" },
        // Idiom: yaapic.response.to.have.jsonSchema.
        { "yaapic.response.to.have.jsonSchema({})", "yaapic.response.to.have.jsonSchema is not supported in Yaapic" },
        // Idiom: yaapic.cookies.jar() (ADR 0009 words it "not available yet").
        { "yaapic.cookies.jar()", "The cookie jar is not available in Yaapic yet" },
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
