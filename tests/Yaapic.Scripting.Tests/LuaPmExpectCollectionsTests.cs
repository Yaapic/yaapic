using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.Lua;

namespace Yaapic.Scripting.Tests;

/// <summary>The Lua collection/string <c>yaapic.expect</c> assertions and <c>yaapic.response.to</c> on the real Lua engine.</summary>
public sealed class LuaPmExpectCollectionsTests
{
    private const string Origin = "post-response · Users API";

    private static ScriptResponse Response(int code = 200, string text = "{\"a\":{\"b\":[{\"c\":7}]},\"n\":1,\"data\":[{\"id\":1},{\"id\":2}]}", string contentType = "application/json", string reason = "OK") =>
        ScriptResponse.FromData(new(
            code,
            reason,
            [new("Content-Type", contentType), new("X-Id", "42")],
            [.. System.Text.Encoding.UTF8.GetBytes(text)],
            TimeSpan.FromMilliseconds(5),
            "https://api.example.com/v1/users"));

    private static async Task<ScriptRunResult> Run(string source, ScriptResponse? response, ScriptPhase phase = ScriptPhase.PostResponse)
    {
        var request = ScriptRequest.FromDefinition(
            new(Guid.Empty, "Users API", HttpMethodKind.Get, "https://api.example.com", [], [], RequestBody.None, RequestAuth.None, RequestSettings.Default),
            readOnly: true);
        return await new LuaEngine().RunAsync(
            new(new(ScriptLanguage.Lua, source), phase, Origin, new Host(request, response), ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(10) }),
            TestContext.Current.CancellationToken);
    }

    private static async Task<Yaapic.Domain.Scripting.TestResult> One(string statements, ScriptResponse? response = null)
    {
        var result = await Run($"yaapic.test('t', function() {statements} end)", response ?? Response());
        Assert.True(result.Succeeded, result.Error?.Message);
        return Assert.Single(result.Tests);
    }

    [Theory]
    [InlineData("yaapic.expect('hello').to.include('ell') yaapic.expect('hello').to.contain('h') yaapic.expect('hello').to.is_not.include('xyz') yaapic.expect('a.b').to.include('.')")]
    [InlineData("yaapic.expect({ 1, 2, 3 }).to.include(2) yaapic.expect({ 1, 2, 3 }).to.contain(3) yaapic.expect({ 1 }).to.is_not.include(2)")]
    [InlineData("yaapic.expect({ { a = 1 } }).to.deep.include({ a = 1 }) yaapic.expect({ { a = 1 } }).to.is_not.include({ a = 1 }) yaapic.expect({ 1 }).to.is_not.deep.include(2)")]
    [InlineData("yaapic.expect({ a = 1, b = 2 }).to.include({ a = 1 }) yaapic.expect({ a = 1, b = 2 }).to.include({ a = 1, b = 2 }) yaapic.expect({ a = 1, b = 2 }).to.is_not.include({ a = 3, b = 4 })")]
    [InlineData("yaapic.expect({ a = { 1 } }).to.deep.include({ a = { 1 } })")]
    [InlineData("yaapic.expect({ 1, 2, 3 }).to.include.members({ 3, 1 }) yaapic.expect({ 1, 2, 3 }).to.have.members({ 3, 1, 2 }) yaapic.expect({}).to.have.members({})")]
    [InlineData("yaapic.expect({ 1, 2, 3 }).to.have.ordered.members({ 1, 2, 3 }) yaapic.expect({ 1, 2, 3 }).to.include.ordered.members({ 1, 2 })")]
    [InlineData("yaapic.expect({ { a = 1 } }).to.have.deep.members({ { a = 1 } }) yaapic.expect({ 1, 2 }).to.is_not.have.members({ 1, 3 }) yaapic.expect({ 1, 1, 2 }).to.is_not.have.members({ 1, 2, 2 })")]
    [InlineData("yaapic.expect({ a = 1 }).to.have.property('a') yaapic.expect({ a = 1 }).to.have.property('a', 1) yaapic.expect({ a = 1 }).to.is_not.have.property('b')")]
    [InlineData("yaapic.expect({ a = 1 }).to.have.property('a').that.equals(1)")]
    [InlineData("yaapic.expect({ a = { b = 2 } }).to.have.property('a').that.has.property('b').which.is.above(1)")]
    [InlineData("yaapic.expect({ a = { b = { { c = 3 } } } }).to.have.nested.property('a.b[0].c', 3) yaapic.expect({ a = { b = { { c = 3 } } } }).to.have.nested.property('a.b[0].c').that.equals(3)")]
    [InlineData("yaapic.expect(yaapic.json.decode('{\"a\":[{\"c\":3}]}')).to.have.nested.property('a[0].c', 3) yaapic.expect(yaapic.json.decode('{\"a\":[5,6]}')).to.have.nested.property('a.1', 6)")]
    [InlineData("yaapic.expect({ a = { b = 1 } }).to.have.deep.property('a', { b = 1 }) yaapic.expect({ a = 1 }).to.have.own.property('a') yaapic.expect({ a = 1 }).to.is_not.have.own.property('b')")]
    [InlineData("yaapic.expect({ a = 1 }).to.have.own_property('a') yaapic.expect({ a = 1 }).to.have.ownProperty('a') yaapic.expect({ a = 1 }).to.is_not.have.own_property('b')")]
    [InlineData("yaapic.expect({ a = 1, b = 2 }).to.have.all.keys('a', 'b') yaapic.expect({ a = 1, b = 2 }).to.have.keys({ 'b', 'a' }) yaapic.expect({ a = 1 }).to.have.key('a')")]
    [InlineData("yaapic.expect({ a = 1, b = 2 }).to.have.any.keys('a', 'z') yaapic.expect({ a = 1, b = 2 }).to.contain.keys('a') yaapic.expect({ a = 1, b = 2 }).to.is_not.have.any.keys('x', 'y')")]
    [InlineData("yaapic.expect({ a = 1, b = 2 }).to.have.keys({ a = true, b = true }) yaapic.expect({ 'x', 'y' }).to.have.keys(1, 2)")]
    [InlineData("yaapic.expect('abc').to.match('b') yaapic.expect('abc').to.matches('^a') yaapic.expect('abc').to.is_not.match('z') yaapic.expect('a1').to.match('%d$')")]
    [InlineData("yaapic.expect('abc').to.have.string('bc') yaapic.expect('abc').to.is_not.have.string('z') yaapic.expect('a.c').to.have.string('.')")]
    [InlineData("yaapic.expect(200).to.be.one_of({ 200, 201 }) yaapic.expect(404).to.is_not.be.one_of({ 200, 201 }) yaapic.expect(200).to.be.oneOf({ 200 })")]
    [InlineData("yaapic.expect(4).to.satisfy(function(n) return n % 2 == 0 end) yaapic.expect(5).to.is_not.satisfies(function(n) return n % 2 == 0 end)")]
    [InlineData("yaapic.expect(function() error('boom') end).to.throw() yaapic.expect(function() error('boom', 0) end).to.throws('boom') yaapic.expect(function() error({ message = 'x' }) end).to.throw()")]
    [InlineData("yaapic.expect(function() error('boom: detail') end).to.Throw('detail') yaapic.expect(function() return 1 end).to.is_not.throw() yaapic.expect(function() return 1 end).to.is_not.throw('x')")]
    [InlineData("yaapic.expect(function() error('boom') end).to.is_not.throw('zzz') yaapic.expect(function() yaapic.expect(1).to.equal(2) end).to.throw('expected 1 to equal 2')")]
    [InlineData("yaapic.expect(yaapic.response.code).to.be.one_of({ 200, 201 }) yaapic.expect(yaapic.response.json()).to.have.nested.property('a.b[0].c').that.equals(7)")]
    public async Task A_passing_assertion_does_not_throw(string statements)
    {
        var test = await One(statements);

        Assert.True(test.Passed, test.Message);
    }

    [Theory]
    [InlineData("yaapic.expect('abc').to.match('z')", "AssertionError: expected 'abc' to match 'z'")]
    [InlineData("yaapic.expect('abc').to.is_not.match('b')", "AssertionError: expected 'abc' not to match 'b'")]
    [InlineData("yaapic.expect(12).to.match('1')", "AssertionError: expected 12 to be a string")]
    [InlineData("yaapic.expect('abc').to.match(1)", "bad argument #1 to 'match' (string expected, got number)")]
    [InlineData("yaapic.expect('abc').to.have.string('b').and_x()", "yaapic.expect(...).and_x is not supported in Yaapic")]
    [InlineData("yaapic.expect(1).to.have.string('b')", "AssertionError: expected 1 to be a string")]
    [InlineData("yaapic.expect(function() error('boom', 0) end).to.is_not.throw()", "AssertionError: expected [Function (anonymous)] to not throw an error but 'boom' was thrown")]
    [InlineData("yaapic.expect(function() error('a: b', 0) end).to.throw('zzz')", "AssertionError: expected [Function (anonymous)] to throw error including 'zzz' but got 'a: b'")]
    [InlineData("yaapic.expect(1).to.throw()", "AssertionError: expected 1 to be a function")]
    [InlineData("yaapic.expect(function() end).to.throw(1)", "bad argument #1 to 'throw' (string expected, got number)")]
    [InlineData("yaapic.expect({}).to.be.instance_of({})", "yaapic.expect(...).instance_of is not supported in Lua")]
    [InlineData("yaapic.expect({}).to.be.instanceOf({})", "yaapic.expect(...).instance_of is not supported in Lua")]
    [InlineData("yaapic.expect(1).to.include({ a = 1 })", "AssertionError: expected 1 to have property 'a'")]
    [InlineData("yaapic.expect({ a = 1 }).to.include(1)", "AssertionError: the given combination of arguments (object and number) is invalid for this assertion. You can use an array, a map, an object, a set, a string, or a weakset instead of a number")]
    [InlineData("yaapic.expect(yaapic.null).to.include(1)", "AssertionError: object tested must be an array, a map, an object, a set, a string, or a weakset, but null given")]
    [InlineData("yaapic.expect({ a = 1 }).to.have.property('toString')", "AssertionError: expected { a: 1 } to have property 'toString'")]
    [InlineData("yaapic.expect({ a = 1 }).to.have.own_property('b')", "AssertionError: expected { a: 1 } to have own property 'b'")]
    [InlineData("yaapic.expect({ a = 1 }).to.have.own.property('b')", "AssertionError: expected { a: 1 } to have own property 'b'")]
    [InlineData("yaapic.expect(5).to.have.property('a')", "AssertionError: expected 5 to have property 'a'")]
    [InlineData("yaapic.expect({ a = 1, b = 2 }).to.have.keys({})", "AssertionError: keys required")]
    [InlineData("yaapic.expect({ 1, 2 }).to.have.members({ 1, 2, 3 })", "AssertionError: expected [ 1, 2 ] to have the same members as [ 1, 2, 3 ]")]
    public async Task A_failing_lua_assertion_reports_its_message(string statements, string message)
    {
        var test = await One(statements);

        Assert.False(test.Passed);
        Assert.Equal(message, test.Message);
    }

    [Theory]
    [InlineData(200, "ok success", "info redirection clientError serverError error accepted badRequest unauthorized forbidden notFound rateLimited")]
    [InlineData(201, "success", "ok info redirection clientError serverError error accepted badRequest unauthorized forbidden notFound rateLimited")]
    [InlineData(202, "success accepted", "ok info redirection clientError serverError error badRequest unauthorized forbidden notFound rateLimited")]
    [InlineData(301, "redirection", "ok success info clientError serverError error accepted badRequest unauthorized forbidden notFound rateLimited")]
    [InlineData(400, "clientError error badRequest", "ok success info redirection serverError accepted unauthorized forbidden notFound rateLimited")]
    [InlineData(401, "clientError error unauthorized", "ok success info redirection serverError accepted badRequest forbidden notFound rateLimited")]
    [InlineData(403, "clientError error forbidden", "ok success info redirection serverError accepted badRequest unauthorized notFound rateLimited")]
    [InlineData(404, "clientError error notFound", "ok success info redirection serverError accepted badRequest unauthorized forbidden rateLimited")]
    [InlineData(429, "clientError error rateLimited", "ok success info redirection serverError accepted badRequest unauthorized forbidden notFound")]
    [InlineData(500, "serverError error", "ok success info redirection clientError accepted badRequest unauthorized forbidden notFound rateLimited")]
    [InlineData(100, "info", "ok success redirection clientError serverError error accepted badRequest unauthorized forbidden notFound rateLimited")]
    public async Task Response_state_flags_match_the_status_code(int code, string yes, string no)
    {
        var response = Response(code);
        foreach (var flag in yes.Split(' '))
        {
            var test = await One($"yaapic.response.to.be.{flag}()", response);
            Assert.True(test.Passed, $"{code} {flag}: {test.Message}");
            var negated = await One($"yaapic.response.to.is_not.be.{flag}()", response);
            Assert.Equal($"AssertionError: expected response to not be {flag}", negated.Message);
        }

        foreach (var flag in no.Split(' '))
        {
            Assert.True((await One($"yaapic.response.to.is_not.be.{flag}()", response)).Passed, $"{code} not {flag}");
            var test = await One($"yaapic.response.to.be.{flag}()", response);
            Assert.Equal($"AssertionError: expected response to be {flag} but got {code}", test.Message);
        }
    }

    [Fact]
    public async Task Response_flags_also_have_snake_case_names()
    {
        var test = await One(
            "yaapic.response.to.be.client_error() yaapic.response.to.be.bad_request() yaapic.response.to.be.is_not.server_error() yaapic.response.to.be.is_not.not_found() yaapic.response.to.be.is_not.rate_limited() yaapic.response.to.be.with_body()",
            Response(400));

        Assert.True(test.Passed, test.Message);
    }

    [Theory]
    [InlineData("", "text/plain", "withBody", false)]
    [InlineData("x", "text/plain", "withBody", true)]
    [InlineData("x", "text/plain", "with_body", true)]
    [InlineData("{}", "application/json; charset=utf-8", "json", true)]
    [InlineData("{}", "application/vnd.api+json", "json", true)]
    [InlineData("{}", "text/json", "json", true)]
    [InlineData("{}", "application/json5", "json", false)]
    [InlineData("<p>", "text/html", "json", false)]
    [InlineData("<p>", "text/html; charset=utf-8", "html", true)]
    [InlineData("hi", "text/plain", "text", true)]
    [InlineData("{}", "application/json", "text", false)]
    [InlineData("{}", "application/json", "html", false)]
    public async Task Response_body_and_content_type_flags(string body, string contentType, string flag, bool expected)
    {
        var response = Response(200, body, contentType);

        var positive = await One($"yaapic.response.to.be.{flag}()", response);
        var negative = await One($"yaapic.response.to.is_not.be.{flag}()", response);

        Assert.Equal(expected, positive.Passed);
        Assert.Equal(!expected, negative.Passed);
    }

    [Theory]
    [InlineData("yaapic.response.to.have.status(200)", true, null)]
    [InlineData("yaapic.response.to.have.status('OK')", true, null)]
    [InlineData("yaapic.response.to.have.status(404)", false, "AssertionError: expected response to have status code 404 but got 200")]
    [InlineData("yaapic.response.to.is_not.have.status(200)", false, "AssertionError: expected response to not have status code 200")]
    [InlineData("yaapic.response.to.have.status('Not Found')", false, "AssertionError: expected response to have status reason 'Not Found' but got 'OK'")]
    [InlineData("yaapic.response.to.is_not.have.status('OK')", false, "AssertionError: expected response to not have status reason 'OK'")]
    [InlineData("yaapic.response.to.have.header('content-type')", true, null)]
    [InlineData("yaapic.response.to.have.header('X-Id', '42')", true, null)]
    [InlineData("yaapic.response.to.have.header('X-Missing')", false, "AssertionError: expected response to have header with key 'X-Missing'")]
    [InlineData("yaapic.response.to.is_not.have.header('X-Id')", false, "AssertionError: expected response to not have header with key 'X-Id'")]
    [InlineData("yaapic.response.to.have.header('X-Id', '1')", false, "AssertionError: expected response to have header 'X-Id' with value '1' but got '42'")]
    [InlineData("yaapic.response.to.have.header('X-Nope', '1')", false, "AssertionError: expected response to have header 'X-Nope' with value '1'")]
    [InlineData("yaapic.response.to.have.body('{\"a\":{\"b\":[{\"c\":7}]},\"n\":1,\"data\":[{\"id\":1},{\"id\":2}]}')", true, null)]
    [InlineData("yaapic.response.to.have.body('\"n\":1')", true, null)]
    [InlineData("yaapic.response.to.have.body('^{.*}$')", true, null)]
    [InlineData("yaapic.response.to.have.body('(')", false, "AssertionError: expected response to have body '(' but got '{\"a\":{\"b\":[{\"c\":7}]},\"n\":1,\"data\":[{\"id\":1},{\"id\":2}]}'")]
    [InlineData("yaapic.response.to.have.body()", true, null)]
    [InlineData("yaapic.response.to.have.body('zzz')", false, "AssertionError: expected response to have body 'zzz' but got '{\"a\":{\"b\":[{\"c\":7}]},\"n\":1,\"data\":[{\"id\":1},{\"id\":2}]}'")]
    [InlineData("yaapic.response.to.is_not.have.body('n')", false, "AssertionError: expected response to not have body 'n'")]
    [InlineData("yaapic.response.to.have.jsonBody()", true, null)]
    [InlineData("yaapic.response.to.have.json_body('n')", true, null)]
    [InlineData("yaapic.response.to.have.json_body('n', 1)", true, null)]
    [InlineData("yaapic.response.to.have.json_body('a.b[0].c', 7)", true, null)]
    [InlineData("yaapic.response.to.have.json_body('a.b', { { c = 7 } })", true, null)]
    [InlineData("yaapic.response.to.have.json_body('data[0].id', 1) yaapic.response.to.have.json_body('data[1].id', 2)", true, null)]
    [InlineData("yaapic.response.to.have.json_body('data.0.id', 1)", true, null)]
    [InlineData("yaapic.response.to.is_not.have.json_body('data[2]') yaapic.response.to.is_not.have.json_body('data[1].id', 1)", true, null)]
    [InlineData("yaapic.response.to.have.json_body('data[2].id')", false, "AssertionError: expected response JSON body to have path 'data[2].id'")]
    [InlineData("yaapic.response.to.have.json_body('data[1].id', 1)", false, "AssertionError: expected response JSON body path 'data[1].id' to equal 1 but got 2")]
    [InlineData("yaapic.response.to.is_not.have.json_body('zzz')", true, null)]
    [InlineData("yaapic.response.to.have.json_body('zzz')", false, "AssertionError: expected response JSON body to have path 'zzz'")]
    [InlineData("yaapic.response.to.have.json_body('n', 2)", false, "AssertionError: expected response JSON body path 'n' to equal 2 but got 1")]
    [InlineData("yaapic.response.to.have.json_body('zzz', 2)", false, "AssertionError: expected response JSON body path 'zzz' to equal 2 but the path does not exist")]
    [InlineData("yaapic.response.to.is_not.have.json_body('n', 1)", false, "AssertionError: expected response JSON body path 'n' to not equal 1")]
    [InlineData("yaapic.response.to.is_not.have.json_body('n')", false, "AssertionError: expected response JSON body to not have path 'n'")]
    [InlineData("yaapic.response.to.is_not.have.json_body()", false, "AssertionError: expected response to not have a JSON body")]
    [InlineData("yaapic.response.to.have.json_schema({})", false, "yaapic.response.to.have.jsonSchema is not supported in Yaapic")]
    [InlineData("yaapic.response.to.have.jsonSchema({})", false, "yaapic.response.to.have.jsonSchema is not supported in Yaapic")]
    [InlineData("yaapic.response.to.have.nothing()", false, "yaapic.response.to.nothing is not supported in Yaapic")]
    public async Task Response_methods_and_message_style(string statements, bool passed, string? message)
    {
        var test = await One(statements);

        Assert.Equal(passed, test.Passed);
        Assert.Equal(message, test.Message);
    }

    [Theory]
    [InlineData("yaapic.response.to.have.json_body()", "AssertionError: expected response to have a JSON body but the body is not valid JSON")]
    [InlineData("yaapic.response.to.have.json_body('a')", "AssertionError: expected response JSON body to have path 'a'")]
    [InlineData("yaapic.response.to.have.with_body()", "AssertionError: expected response to have a body")]
    [InlineData("yaapic.response.to.have.body()", "AssertionError: expected response to have a body")]
    [InlineData("yaapic.response.to.be.json()", "AssertionError: expected response to be json but got no content type")]
    public async Task Response_helpers_on_an_empty_body_without_content_type(string statements, string message)
    {
        var response = ScriptResponse.FromData(new(204, "No Content", [], [], TimeSpan.Zero, "https://api.example.com"));

        var test = await One(statements, response);

        Assert.Equal(message, test.Message);
    }

    [Fact]
    public async Task Response_json_content_type_failure_names_the_type()
    {
        var test = await One("yaapic.response.to.be.json()", Response(200, "<p>", "text/html"));

        Assert.Equal("AssertionError: expected response to be json but got content type 'text/html'", test.Message);
    }

    [Fact]
    public async Task Response_to_in_pre_request_throws_the_post_response_only_error()
    {
        var result = await Run("yaapic.response.to.be.ok()", null, ScriptPhase.PreRequest);

        Assert.False(result.Succeeded);
        Assert.EndsWith("yaapic.response is only available in post-response scripts", result.Error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_access_to_response_to_starts_a_fresh_chain()
    {
        var test = await One("yaapic.response.to.is_not.be.not_found() yaapic.response.to.be.ok()");

        Assert.True(test.Passed, test.Message);
    }

    private sealed class Host(ScriptRequest request, ScriptResponse? response) : IScriptHost
    {
        public ScriptInfo Info => new("test", "Users API", Guid.Empty, Origin);

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
