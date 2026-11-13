using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.QuickJs;

namespace Yaapic.Scripting.Tests;

/// <summary>The collection/string <c>yaapic.expect</c> assertions and <c>yaapic.response.to.*</c> on the real QuickJS engine.</summary>
public sealed class PmExpectCollectionsTests
{
    private const string Origin = "post-response · Users API";

    private static ScriptResponse Response(int code = 200, string text = "{\"a\":{\"b\":[{\"c\":7}]},\"n\":1}", string contentType = "application/json", string reason = "OK") =>
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
        return await new QuickJsEngine().RunAsync(
            new(new(ScriptLanguage.JavaScript, source), phase, Origin, new Host(request, response), ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(10) }),
            TestContext.Current.CancellationToken);
    }

    private static async Task<Yaapic.Domain.Scripting.TestResult> One(string statements, ScriptResponse? response = null)
    {
        var result = await Run($"yaapic.test('t', () => {{ {statements} }});", response ?? Response());
        Assert.True(result.Succeeded, result.Error?.Message);
        return Assert.Single(result.Tests);
    }

    [Theory]
    [InlineData("yaapic.expect('hello').to.include('ell')")]
    [InlineData("yaapic.expect('hello').to.not.include('xyz')")]
    [InlineData("yaapic.expect([1, 2, 3]).to.include(2)")]
    [InlineData("yaapic.expect([1, 2, 3]).to.contain(3)")]
    [InlineData("yaapic.expect([{ a: 1 }]).to.deep.include({ a: 1 })")]
    [InlineData("yaapic.expect([1]).to.not.deep.include(2)")]
    [InlineData("yaapic.expect({ a: 1, b: 2 }).to.include({ a: 1 })")]
    [InlineData("yaapic.expect({ a: 1, b: 2 }).to.include({ a: 1, b: 2 })")]
    [InlineData("yaapic.expect({ a: 1, b: 2 }).to.not.include({ a: 3, b: 4 })")]
    [InlineData("yaapic.expect(new Set([1, 2])).to.include(2)")]
    [InlineData("yaapic.expect(new Map([['k', 'v']])).to.include('v')")]
    [InlineData("yaapic.expect([1, 2, 3]).to.include.members([3, 1])")]
    [InlineData("yaapic.expect([1, 2, 3]).to.have.members([3, 1, 2])")]
    [InlineData("yaapic.expect([1, 2, 3]).to.have.ordered.members([1, 2, 3])")]
    [InlineData("yaapic.expect([1, 2, 3]).to.include.ordered.members([1, 2])")]
    [InlineData("yaapic.expect([{ a: 1 }]).to.have.deep.members([{ a: 1 }])")]
    [InlineData("yaapic.expect([1, 2]).to.not.have.members([1, 3])")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.property('a')")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.property('a', 1)")]
    [InlineData("yaapic.expect({ a: 1 }).to.not.have.property('b')")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.property('toString')")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.property('a').that.equals(1)")]
    [InlineData("yaapic.expect({ a: { b: 2 } }).to.have.property('a').that.has.property('b').which.is.above(1)")]
    [InlineData("yaapic.expect({ a: { b: [{ c: 3 }] } }).to.have.nested.property('a.b[0].c', 3)")]
    [InlineData("yaapic.expect({ a: { b: 1 } }).to.have.deep.property('a', { b: 1 })")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.own.property('a')")]
    [InlineData("yaapic.expect({ a: 1 }).to.not.have.own.property('toString')")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.ownProperty('a')")]
    [InlineData("yaapic.expect({ a: 1 }).to.haveOwnProperty('a')")]
    [InlineData("yaapic.expect({ a: 1, b: 2 }).to.have.all.keys('a', 'b')")]
    [InlineData("yaapic.expect({ a: 1, b: 2 }).to.have.keys(['b', 'a'])")]
    [InlineData("yaapic.expect({ a: 1, b: 2 }).to.have.any.keys('a', 'z')")]
    [InlineData("yaapic.expect({ a: 1, b: 2 }).to.contain.keys('a')")]
    [InlineData("yaapic.expect({ a: 1, b: 2 }).to.not.have.any.keys('x', 'y')")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.key('a')")]
    [InlineData("yaapic.expect(new Map([['k', 1]])).to.have.keys('k')")]
    [InlineData("yaapic.expect('abc').to.match(/b/)")]
    [InlineData("yaapic.expect('abc').to.matches(/^a/)")]
    [InlineData("yaapic.expect('abc').to.not.match(/z/)")]
    [InlineData("yaapic.expect('abc').to.have.string('bc')")]
    [InlineData("yaapic.expect('abc').to.not.have.string('z')")]
    [InlineData("yaapic.expect(200).to.be.oneOf([200, 201])")]
    [InlineData("yaapic.expect(404).to.not.be.oneOf([200, 201])")]
    [InlineData("yaapic.expect([]).to.be.an.instanceOf(Array)")]
    [InlineData("yaapic.expect(new Error('x')).to.be.instanceof(Error)")]
    [InlineData("yaapic.expect({}).to.not.be.an.instanceOf(Array)")]
    [InlineData("yaapic.expect(4).to.satisfy((n) => n % 2 === 0)")]
    [InlineData("yaapic.expect(5).to.not.satisfies((n) => n % 2 === 0)")]
    [InlineData("yaapic.expect(() => { throw new TypeError('boom'); }).to.throw()")]
    [InlineData("yaapic.expect(() => { throw new TypeError('boom'); }).to.throw(TypeError)")]
    [InlineData("yaapic.expect(() => { throw new TypeError('boom'); }).to.throws('boom')")]
    [InlineData("yaapic.expect(() => { throw new TypeError('boom'); }).to.Throw(/bo+m/)")]
    [InlineData("yaapic.expect(() => { throw new TypeError('boom'); }).to.throw(TypeError, 'boom')")]
    [InlineData("yaapic.expect(() => { throw new TypeError('boom'); }).to.not.throw(RangeError)")]
    [InlineData("yaapic.expect(() => { throw new TypeError('boom'); }).to.not.throw(RangeError, 'boom')")]
    [InlineData("yaapic.expect(() => 1).to.not.throw()")]
    [InlineData("yaapic.expect(() => 1).to.not.throw(TypeError)")]
    public async Task A_passing_assertion_does_not_throw(string statements)
    {
        var test = await One(statements);

        Assert.True(test.Passed, test.Message);
    }

    [Theory]
    [InlineData("yaapic.expect('hello').to.include('xyz')", "AssertionError: expected 'hello' to include 'xyz'")]
    [InlineData("yaapic.expect('hello').to.not.include('ell')", "AssertionError: expected 'hello' to not include 'ell'")]
    [InlineData("yaapic.expect([1, 2]).to.include(3)", "AssertionError: expected [ 1, 2 ] to include 3")]
    [InlineData("yaapic.expect([{ a: 1 }]).to.include({ a: 1 })", "AssertionError: expected [ { a: 1 } ] to include { a: 1 }")]
    [InlineData("yaapic.expect([{ a: 1 }]).to.deep.include({ a: 2 })", "AssertionError: expected [ { a: 1 } ] to include { a: 2 }")]
    [InlineData("yaapic.expect({ a: 1 }).to.include({ a: 2 })", "AssertionError: expected { a: 1 } to have property 'a' of 2, but got 1")]
    [InlineData("yaapic.expect({ a: 1 }).to.include({ b: 2 })", "AssertionError: expected { a: 1 } to have property 'b'")]
    [InlineData("yaapic.expect({ a: 1, b: 2 }).to.not.include({ a: 1 })", "AssertionError: expected { a: 1, b: 2 } to not have property 'a' of 1")]
    [InlineData("yaapic.expect(new Set([1])).to.include(2)", "AssertionError: expected Set(1) { 1 } to include 2")]
    [InlineData("yaapic.expect([1, 2]).to.include.members([1, 5])", "AssertionError: expected [ 1, 2 ] to be a superset of [ 1, 5 ]")]
    [InlineData("yaapic.expect([1, 2]).to.have.members([1])", "AssertionError: expected [ 1, 2 ] to have the same members as [ 1 ]")]
    [InlineData("yaapic.expect([1, 2]).to.have.ordered.members([2, 1])", "AssertionError: expected [ 1, 2 ] to have the same ordered members as [ 2, 1 ]")]
    [InlineData("yaapic.expect([1, 2]).to.include.ordered.members([2])", "AssertionError: expected [ 1, 2 ] to be an ordered superset of [ 2 ]")]
    [InlineData("yaapic.expect([1, 2]).to.not.have.members([2, 1])", "AssertionError: expected [ 1, 2 ] to not have the same members as [ 2, 1 ]")]
    [InlineData("yaapic.expect([{ a: 1 }]).to.have.members([{ a: 1 }])", "AssertionError: expected [ { a: 1 } ] to have the same members as [ { a: 1 } ]")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.property('b')", "AssertionError: expected { a: 1 } to have property 'b'")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.property('a', 2)", "AssertionError: expected { a: 1 } to have property 'a' of 2, but got 1")]
    [InlineData("yaapic.expect({ a: 1 }).to.not.have.property('a')", "AssertionError: expected { a: 1 } to not have property 'a'")]
    [InlineData("yaapic.expect({ a: 1 }).to.not.have.property('a', 1)", "AssertionError: expected { a: 1 } to not have property 'a' of 1")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.property('a').that.equals(2)", "AssertionError: expected 1 to equal 2")]
    [InlineData("yaapic.expect({ a: { b: [{ c: 3 }] } }).to.have.nested.property('a.b[0].d')", "AssertionError: expected { a: { b: [ [Object] ] } } to have nested property 'a.b[0].d'")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.own.property('toString')", "AssertionError: expected { a: 1 } to have own property 'toString'")]
    [InlineData("yaapic.expect({ a: { b: 1 } }).to.have.deep.property('a', { b: 2 })", "AssertionError: expected { a: { b: 1 } } to have deep property 'a' of { b: 2 }, but got { b: 1 }")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.ownProperty('b')", "AssertionError: expected { a: 1 } to have own property 'b'")]
    [InlineData("yaapic.expect(null).to.have.property('a')", "AssertionError: Target cannot be null or undefined.")]
    [InlineData("yaapic.expect({ a: 1, b: 2 }).to.have.keys('a')", "AssertionError: expected { a: 1, b: 2 } to have key 'a'")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.all.keys('a', 'b')", "AssertionError: expected { a: 1 } to have keys 'a', and 'b'")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.any.keys('x', 'y')", "AssertionError: expected { a: 1 } to have keys 'x', or 'y'")]
    [InlineData("yaapic.expect({ a: 1 }).to.contain.keys('x')", "AssertionError: expected { a: 1 } to contain key 'x'")]
    [InlineData("yaapic.expect({ a: 1 }).to.not.have.keys('a')", "AssertionError: expected { a: 1 } to not have key 'a'")]
    [InlineData("yaapic.expect({ a: 1 }).to.have.keys()", "AssertionError: keys required")]
    [InlineData("yaapic.expect('abc').to.match(/z/)", "AssertionError: expected 'abc' to match /z/")]
    [InlineData("yaapic.expect('abc').to.not.match(/b/)", "AssertionError: expected 'abc' not to match /b/")]
    [InlineData("yaapic.expect('abc').to.have.string('z')", "AssertionError: expected 'abc' to contain 'z'")]
    [InlineData("yaapic.expect('abc').to.not.have.string('b')", "AssertionError: expected 'abc' to not contain 'b'")]
    [InlineData("yaapic.expect(404).to.be.oneOf([200, 201])", "AssertionError: expected 404 to be one of [ 200, 201 ]")]
    [InlineData("yaapic.expect(200).to.not.be.oneOf([200])", "AssertionError: expected 200 to not be one of [ 200 ]")]
    [InlineData("yaapic.expect(200).to.be.oneOf(5)", "AssertionError: expected 5 to be an array")]
    [InlineData("yaapic.expect({}).to.be.an.instanceOf(Array)", "AssertionError: expected {} to be an instance of Array")]
    [InlineData("yaapic.expect([]).to.not.be.an.instanceOf(Array)", "AssertionError: expected [] to not be an instance of Array")]
    [InlineData("yaapic.expect(3).to.satisfy(function odd(n) { return n % 2 === 0; })", "AssertionError: expected 3 to satisfy [Function: odd]")]
    [InlineData("yaapic.expect(4).to.not.satisfy(function even(n) { return n % 2 === 0; })", "AssertionError: expected 4 to not satisfy [Function: even]")]
    [InlineData("yaapic.expect(() => 1).to.throw()", "AssertionError: expected [Function (anonymous)] to throw an error")]
    [InlineData("yaapic.expect(() => { throw new TypeError('boom'); }).to.not.throw()", "AssertionError: expected [Function (anonymous)] to not throw an error but 'TypeError: boom' was thrown")]
    [InlineData("yaapic.expect(() => 1).to.throw(TypeError)", "AssertionError: expected [Function (anonymous)] to throw TypeError")]
    [InlineData("yaapic.expect(() => { throw new RangeError('r'); }).to.throw(TypeError)", "AssertionError: expected [Function (anonymous)] to throw TypeError but 'RangeError: r' was thrown")]
    [InlineData("yaapic.expect(() => { throw new TypeError('r'); }).to.not.throw(TypeError)", "AssertionError: expected [Function (anonymous)] to not throw TypeError")]
    [InlineData("yaapic.expect(() => { throw new TypeError('boom'); }).to.throw('zzz')", "AssertionError: expected [Function (anonymous)] to throw error including 'zzz' but got 'boom'")]
    [InlineData("yaapic.expect(() => { throw new TypeError('boom'); }).to.throw(/zzz/)", "AssertionError: expected [Function (anonymous)] to throw error matching /zzz/ but got 'boom'")]
    [InlineData("yaapic.expect(() => { throw new TypeError('boom'); }).to.not.throw(/boom/)", "AssertionError: expected [Function (anonymous)] to throw error not matching /boom/")]
    [InlineData("yaapic.expect(() => { throw new TypeError('boom'); }).to.not.throw('boom')", "AssertionError: expected [Function (anonymous)] to throw error not including 'boom'")]
    [InlineData("yaapic.expect(1, 'custom').to.include.members([1])", "AssertionError: custom: expected 1 to be an array")]
    public async Task A_failing_assertion_reports_the_exact_chai_message(string statements, string message)
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
            var test = await One($"yaapic.response.to.be.{flag};", response);
            Assert.True(test.Passed, $"{code} {flag}: {test.Message}");
            var negated = await One($"yaapic.response.to.not.be.{flag};", response);
            Assert.Equal($"AssertionError: expected response to not be {flag}", negated.Message);
        }

        foreach (var flag in no.Split(' '))
        {
            Assert.True((await One($"yaapic.response.to.not.be.{flag};", response)).Passed, $"{code} not {flag}");
            var test = await One($"yaapic.response.to.be.{flag};", response);
            Assert.Equal($"AssertionError: expected response to be {flag} but got {code}", test.Message);
        }
    }

    [Theory]
    [InlineData("", "text/plain", "withBody", false)]
    [InlineData("x", "text/plain", "withBody", true)]
    [InlineData("{}", "application/json; charset=utf-8", "json", true)]
    [InlineData("{}", "application/vnd.api+json", "json", true)]
    [InlineData("<p>", "text/html", "json", false)]
    [InlineData("<p>", "text/html; charset=utf-8", "html", true)]
    [InlineData("hi", "text/plain", "text", true)]
    [InlineData("{}", "application/json", "text", false)]
    [InlineData("{}", "application/json", "html", false)]
    public async Task Response_body_and_content_type_flags(string body, string contentType, string flag, bool expected)
    {
        var response = Response(200, body, contentType);

        var positive = await One($"yaapic.response.to.be.{flag};", response);
        var negative = await One($"yaapic.response.to.not.be.{flag};", response);

        Assert.Equal(expected, positive.Passed);
        Assert.Equal(!expected, negative.Passed);
    }

    [Theory]
    [InlineData("yaapic.response.to.have.status(200)", true, null)]
    [InlineData("yaapic.response.to.have.status('OK')", true, null)]
    [InlineData("yaapic.response.to.have.status(404)", false, "AssertionError: expected response to have status code 404 but got 200")]
    [InlineData("yaapic.response.to.not.have.status(200)", false, "AssertionError: expected response to not have status code 200")]
    [InlineData("yaapic.response.to.have.status('Not Found')", false, "AssertionError: expected response to have status reason 'Not Found' but got 'OK'")]
    [InlineData("yaapic.response.to.not.have.status('OK')", false, "AssertionError: expected response to not have status reason 'OK'")]
    [InlineData("yaapic.response.to.have.header('content-type')", true, null)]
    [InlineData("yaapic.response.to.have.header('X-Id', '42')", true, null)]
    [InlineData("yaapic.response.to.have.header('X-Missing')", false, "AssertionError: expected response to have header with key 'X-Missing'")]
    [InlineData("yaapic.response.to.not.have.header('X-Id')", false, "AssertionError: expected response to not have header with key 'X-Id'")]
    [InlineData("yaapic.response.to.have.header('X-Id', '1')", false, "AssertionError: expected response to have header 'X-Id' with value '1' but got '42'")]
    [InlineData("yaapic.response.to.have.header('X-Nope', '1')", false, "AssertionError: expected response to have header 'X-Nope' with value '1'")]
    [InlineData("yaapic.response.to.have.body('{\"a\":{\"b\":[{\"c\":7}]},\"n\":1}')", true, null)]
    [InlineData("yaapic.response.to.have.body(/\"n\":1/)", true, null)]
    [InlineData("yaapic.response.to.have.body()", true, null)]
    [InlineData("yaapic.response.to.have.body('x')", false, "AssertionError: expected response to have body 'x' but got '{\"a\":{\"b\":[{\"c\":7}]},\"n\":1}'")]
    [InlineData("yaapic.response.to.have.body(/zzz/)", false, "AssertionError: expected response body to match /zzz/ but got '{\"a\":{\"b\":[{\"c\":7}]},\"n\":1}'")]
    [InlineData("yaapic.response.to.not.have.body(/n/)", false, "AssertionError: expected response body to not match /n/")]
    [InlineData("yaapic.response.to.have.jsonBody()", true, null)]
    [InlineData("yaapic.response.to.have.jsonBody('n')", true, null)]
    [InlineData("yaapic.response.to.have.jsonBody('n', 1)", true, null)]
    [InlineData("yaapic.response.to.have.jsonBody('a.b[0].c', 7)", true, null)]
    [InlineData("yaapic.response.to.have.jsonBody('a.b', [{ c: 7 }])", true, null)]
    [InlineData("yaapic.response.to.not.have.jsonBody('zzz')", true, null)]
    [InlineData("yaapic.response.to.have.jsonBody('zzz')", false, "AssertionError: expected response JSON body to have path 'zzz'")]
    [InlineData("yaapic.response.to.have.jsonBody('n', 2)", false, "AssertionError: expected response JSON body path 'n' to equal 2 but got 1")]
    [InlineData("yaapic.response.to.have.jsonBody('zzz', 2)", false, "AssertionError: expected response JSON body path 'zzz' to equal 2 but the path does not exist")]
    [InlineData("yaapic.response.to.not.have.jsonBody('n', 1)", false, "AssertionError: expected response JSON body path 'n' to not equal 1")]
    [InlineData("yaapic.response.to.not.have.jsonBody('n')", false, "AssertionError: expected response JSON body to not have path 'n'")]
    [InlineData("yaapic.response.to.not.have.jsonBody()", false, "AssertionError: expected response to not have a JSON body")]
    [InlineData("yaapic.response.to.have.jsonSchema({})", false, "Error: yaapic.response.to.have.jsonSchema is not supported in Yaapic")]
    [InlineData("yaapic.expect(yaapic.response.code).to.be.oneOf([200, 201])", true, null)]
    [InlineData("yaapic.expect(yaapic.response.code).to.not.be.oneOf([200, 201])", false, "AssertionError: expected 200 to not be one of [ 200, 201 ]")]
    [InlineData("yaapic.expect(yaapic.response.json()).to.have.nested.property('a.b[0].c').that.equals(7)", true, null)]
    public async Task Response_methods_and_message_style(string statements, bool passed, string? message)
    {
        var test = await One(statements);

        Assert.Equal(passed, test.Passed);
        Assert.Equal(message, test.Message);
    }

    [Theory]
    [InlineData("yaapic.response.to.have.jsonBody()", "AssertionError: expected response to have a JSON body but the body is not valid JSON")]
    [InlineData("yaapic.response.to.have.jsonBody('a')", "AssertionError: expected response JSON body to have path 'a'")]
    [InlineData("yaapic.response.to.have.withBody", "AssertionError: expected response to have a body")]
    [InlineData("yaapic.response.to.have.body()", "AssertionError: expected response to have a body")]
    [InlineData("yaapic.response.to.be.json", "AssertionError: expected response to be json but got no content type")]
    public async Task Response_helpers_on_an_empty_body_without_content_type(string statements, string message)
    {
        var response = ScriptResponse.FromData(new(204, "No Content", [], [], TimeSpan.Zero, "https://api.example.com"));

        var test = await One(statements, response);

        Assert.Equal(message, test.Message);
    }

    [Fact]
    public async Task Response_json_content_type_failure_names_the_type()
    {
        var test = await One("yaapic.response.to.be.json", Response(200, "<p>", "text/html"));

        Assert.Equal("AssertionError: expected response to be json but got content type 'text/html'", test.Message);
    }

    [Fact]
    public async Task Response_to_in_pre_request_throws_the_post_response_only_error()
    {
        var result = await Run("yaapic.response.to.be.ok;", null, ScriptPhase.PreRequest);

        Assert.False(result.Succeeded);
        Assert.Equal("yaapic.response is only available in post-response scripts", result.Error?.Message);
    }

    [Fact]
    public async Task Each_access_to_response_to_starts_a_fresh_chain()
    {
        var test = await One("try { yaapic.response.to.not.be.notFound; yaapic.response.to.be.ok; } catch (e) { throw new Error('leaked ' + e.message); }");

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
