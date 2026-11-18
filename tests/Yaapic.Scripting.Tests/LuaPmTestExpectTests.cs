using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.Lua;
using Yaapic.Scripting.Tests.Conformance;

namespace Yaapic.Scripting.Tests;

/// <summary>The Lua <c>yaapic.test</c> and core <c>yaapic.expect</c> assertions on the real Lua engine, recording tests on a fake host.</summary>
public sealed class LuaPmTestExpectTests
{
    private const string Origin = "post-response · Users API";

    private static readonly ScriptLimits Fast = ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(10) };

    private static Task<ScriptRunResult> Run(string source, ScriptLimits? limits = null) =>
        new LuaEngine().RunAsync(
            new(new(ScriptLanguage.Lua, source), ScriptPhase.PostResponse, Origin, new QuickJsEngineTests.FakeHost(), limits ?? Fast),
            TestContext.Current.CancellationToken);

    /// <summary>Runs the statements inside one yaapic.test and returns its result.</summary>
    private static async Task<Yaapic.Domain.Scripting.TestResult> One(string statements)
    {
        var result = await Run($"yaapic.test('t', function() {statements} end)");
        Assert.True(result.Succeeded, result.Error?.Message);
        return Assert.Single(result.Tests);
    }

    [Theory]
    [MemberData(nameof(ExpectMessages.Lua), MemberType = typeof(ExpectMessages))]
    public async Task A_failing_assertion_reports_the_same_message_as_javascript(string statements, string message)
    {
        var test = await One(statements);

        Assert.False(test.Passed);
        Assert.Equal(message, test.Message);
    }

    [Theory]
    [InlineData("yaapic.expect(204).to.equal(204)")]
    [InlineData("yaapic.expect(1).to.is_not.equal(2) yaapic.expect(1).to.equal(1.0) yaapic.expect('a').to.equals('a') yaapic.expect(1).to.eq(1)")]
    [InlineData("local t = {} yaapic.expect(t).to.equal(t) yaapic.expect({}).to.is_not.equal({})")]
    [InlineData("yaapic.expect(0/0).to.is_not.equal(0/0) yaapic.expect(0/0).to.is_not.eql(0/0)")]
    [InlineData("yaapic.expect({ a = { 1, { b = 2 } } }).to.eql({ a = { 1, { b = 2 } } }) yaapic.expect({ a = 1 }).to.eqls({ a = 1 })")]
    [InlineData("yaapic.expect({ 1, 2 }).to.deep.equal({ 1, 2 }) yaapic.expect({ a = 1 }).to.is_not.eql({ a = 2 }) yaapic.expect({ 1 }).to.is_not.eql({ a = 1 })")]
    [InlineData("yaapic.expect(yaapic.null).to.eql(yaapic.null) yaapic.expect(yaapic.null).to.is_not.eql({}) yaapic.expect(yaapic.null).to.is_not.eql(nil)")]
    [InlineData("yaapic.expect(yaapic.json.decode('[1,[2,{\"a\":null}]]')).to.eql(yaapic.json.decode('[1,[2,{\"a\":null}]]'))")]
    [InlineData("yaapic.expect(yaapic.json.decode('[]')).to.is_not.eql({}) yaapic.expect(yaapic.json.decode('[]')).to.eql(yaapic.json.array({}))")]
    [InlineData("local a = {} a.self = a local b = {} b.self = b yaapic.expect(a).to.eql(b)")]
    [InlineData("yaapic.expect(1).to.be.ok() yaapic.expect(0).to.be.ok() yaapic.expect('').to.be.ok() yaapic.expect(false).to.is_not.be.ok() yaapic.expect(nil).to.be.is_not.ok()")]
    [InlineData("yaapic.expect(true).to.be.is_true() yaapic.expect(false).to.be.is_false() yaapic.expect(1).to.is_not.be.is_true() yaapic.expect(nil).to.is_not.be.is_false()")]
    [InlineData("yaapic.expect(yaapic.null).to.be.is_null() yaapic.expect(nil).to.be.is_nil() yaapic.expect(nil).to.is_not.be.is_null() yaapic.expect(yaapic.null).to.is_not.be.is_nil()")]
    [InlineData("yaapic.expect(0/0).to.be.is_nan() yaapic.expect(1).to.is_not.be.is_nan() yaapic.expect('x').to.is_not.be.is_nan()")]
    [InlineData("yaapic.expect(0).to.exist() yaapic.expect(false).to.exist() yaapic.expect(nil).to.is_not.exist() yaapic.expect(yaapic.null).to.is_not.exist()")]
    [InlineData("yaapic.expect('').to.be.empty() yaapic.expect({}).to.be.empty() yaapic.expect(yaapic.json.array({})).to.be.empty() yaapic.expect({ 0 }).to.is_not.be.empty() yaapic.expect({ a = 1 }).to.is_not.be.empty() yaapic.expect('a').to.is_not.be.empty()")]
    [InlineData("yaapic.expect('a').to.be.a('string') yaapic.expect(1).to.be.a('number') yaapic.expect(true).to.be.a('boolean') yaapic.expect(print).to.be.a('function')")]
    [InlineData("yaapic.expect(1).to.be.an('integer') yaapic.expect(1.5).to.be.a('float') yaapic.expect(2.0).to.be.a('float') yaapic.expect(1.5).to.is_not.be.an('integer')")]
    [InlineData("yaapic.expect({ 1, 2 }).to.be.an('array') yaapic.expect(yaapic.json.array({})).to.be.an('array') yaapic.expect({ a = 1 }).to.be.an('object') yaapic.expect({}).to.be.an('object') yaapic.expect({ 1 }).to.is_not.be.an('object')")]
    [InlineData("yaapic.expect({}).to.be.a('table') yaapic.expect({ 1 }).to.be.a('table') yaapic.expect(yaapic.null).to.is_not.be.a('table') yaapic.expect(yaapic.null).to.is_not.be.an('object') yaapic.expect(yaapic.null).to.be.a('null')")]
    [InlineData("yaapic.expect(nil).to.be.a('nil') yaapic.expect('a').to.be.a('STRING') yaapic.expect(1).to.is_not.be.a('string')")]
    [InlineData("yaapic.expect(6).to.be.above(5).also.below(7).also.at.least(6).also.at.most(6) yaapic.expect(6).to.be.gt(5).also.lt(7).also.gte(6).also.lte(6)")]
    [InlineData("yaapic.expect(5).to.is_not.be.above(5) yaapic.expect(5.5).to.be.above(5)")]
    [InlineData("yaapic.expect(3).to.be.within(1, 5) yaapic.expect(1).to.be.within(1, 5) yaapic.expect(9).to.is_not.be.within(1, 5)")]
    [InlineData("yaapic.expect(1).to.be.close_to(1.4, 0.5) yaapic.expect(1).to.be.close_to(1, 0) yaapic.expect(1).to.is_not.be.close_to(3, 1) yaapic.expect(1).to.be.closeTo(1, 0)")]
    [InlineData("yaapic.expect({ 1, 2 }).to.have.length(2) yaapic.expect('abc').to.have.length_of(3) yaapic.expect({ 1 }).to.is_not.have.length(2) yaapic.expect({}).to.have.length(0) yaapic.expect(yaapic.json.decode('[1,2,3]')).to.have.lengthOf(3)")]
    [InlineData("yaapic.expect(1).to.be.that.is.which.has.have.with.at.of.same.but.does.still.also.been.equal(1)")]
    [InlineData("local a = yaapic.expect(1) yaapic.expect(a.to).to.equal(a) yaapic.expect(a.equal(1)).to.equal(a)")]
    public async Task A_passing_assertion_does_not_throw(string statements)
    {
        var test = await One(statements);

        Assert.True(test.Passed, test.Message);
        Assert.Null(test.Message);
    }

    [Fact]
    public async Task Assertion_error_carries_its_details()
    {
        var result = await Run(
            "local ok, e = pcall(function() yaapic.expect(1).to.equal(2) end) console.log(type(e), e.name, e.message, e.actual, e.expected, tostring(e), getmetatable(e).__tostring ~= nil)");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("table AssertionError expected 1 to equal 2 1 2 AssertionError: expected 1 to equal 2 true", Assert.Single(result.Console).Message);
    }

    [Theory]
    [InlineData("yaapic.expect({ ['a-b'] = 'x', c = yaapic.null, z = { 1 } }).to.equal(1)", "AssertionError: expected { 'a-b': 'x', c: null, z: [ 1 ] } to equal 1")]
    [InlineData("yaapic.expect({ b = 1, a = 2 }).to.equal(1)", "AssertionError: expected { a: 2, b: 1 } to equal 1")]
    [InlineData("yaapic.expect(nil).to.equal(1)", "AssertionError: expected undefined to equal 1")]
    [InlineData("yaapic.expect(yaapic.null).to.equal(1)", "AssertionError: expected null to equal 1")]
    [InlineData("yaapic.expect(1.5).to.equal(1)", "AssertionError: expected 1.5 to equal 1")]
    [InlineData("yaapic.expect(2.0).to.equal(1)", "AssertionError: expected 2 to equal 1")]
    [InlineData("yaapic.expect(1/0).to.equal(1)", "AssertionError: expected Infinity to equal 1")]
    [InlineData("yaapic.expect(-1/0).to.equal(1)", "AssertionError: expected -Infinity to equal 1")]
    [InlineData("yaapic.expect('a\\\\b').to.equal(1)", "AssertionError: expected 'a\\\\b' to equal 1")]
    [InlineData("yaapic.expect({}).to.equal(1)", "AssertionError: expected {} to equal 1")]
    [InlineData("yaapic.expect(yaapic.json.array({})).to.equal(1)", "AssertionError: expected [] to equal 1")]
    [InlineData("yaapic.expect(true).to.equal(1)", "AssertionError: expected true to equal 1")]
    [InlineData("yaapic.expect(print).to.equal(1)", "AssertionError: expected [Function (anonymous)] to equal 1")]
    [InlineData("local o = { n = 1 } o.self = o yaapic.expect(o).to.equal(1)", "AssertionError: expected { n: 1, self: [Circular] } to equal 1")]
    [InlineData("yaapic.expect(yaapic.json.decode('[1,{\"a\":[2]}]')).to.equal(1)", "AssertionError: expected [ 1, { a: [ 2 ] } ] to equal 1")]
    [InlineData("local e = yaapic.json.array({ 1 }) yaapic.expect({ e, e }).to.equal(1)", "AssertionError: expected [ [ 1 ], [ 1 ] ] to equal 1")]
    [InlineData("yaapic.expect({ [5] = 'a' }).to.equal(1)", "AssertionError: expected { '5': 'a' } to equal 1")]
    public async Task Values_are_inspected_like_the_javascript_engine(string statements, string message)
    {
        var test = await One(statements);

        Assert.Equal(message, test.Message);
    }

    [Theory]
    [InlineData("yaapic.expect(1).to.above('x')", "bad argument #1 to 'above' (number expected, got string)")]
    [InlineData("yaapic.expect(1).to.be.within(1)", "bad argument #2 to 'within' (number expected, got nil)")]
    [InlineData("yaapic.expect(1).to.be.close_to(1)", "bad argument #2 to 'close_to' (number expected, got nil)")]
    [InlineData("yaapic.expect(1).to.have.length('x')", "bad argument #1 to 'length' (number expected, got string)")]
    [InlineData("yaapic.expect(1).to.be.unknown()", "yaapic.expect(...).unknown is not supported in Yaapic")]
    [InlineData("yaapic.expect(1).to.equal = 5", "yaapic.expect(...).equal cannot be assigned")]
    [InlineData("yaapic.expect(1).to.be.empty()", ".empty was passed non-string primitive 1")]
    [InlineData("yaapic.expect(nil).to.be.empty()", ".empty was passed undefined")]
    [InlineData("yaapic.expect(yaapic.null).to.be.empty()", ".empty was passed null")]
    [InlineData("yaapic.expect(1).to.have.length(1)", "AssertionError: expected 1 to have a length")]
    public async Task Misuse_of_an_assertion_fails_the_test_with_a_clear_message(string statements, string message)
    {
        var test = await One(statements);

        Assert.False(test.Passed);
        Assert.Equal(message, test.Message);
    }

    [Fact]
    public async Task Tests_are_recorded_once_in_order_and_an_error_does_not_stop_the_next_test()
    {
        var result = await Run(
            """
            yaapic.test('first', function() yaapic.expect(1).to.equal(1) end)
            yaapic.test('second', function() yaapic.expect(409).to.equal(204) end)
            yaapic.test('third', function() local x = nil; return x.y end)
            yaapic.test('first', function() end)
            yaapic.test(42, function() end)
            """);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(["first", "second", "third", "first", "42"], result.Tests.Select(t => t.Name));
        Assert.Equal([true, false, false, true, true], result.Tests.Select(t => t.Passed));
        Assert.Equal("AssertionError: expected 409 to equal 204", result.Tests[1].Message);
        Assert.StartsWith("attempt to index a nil value", result.Tests[2].Message);
        Assert.All(result.Tests, t => Assert.Equal(Origin, t.Origin));
    }

    [Fact]
    public async Task A_runtime_error_loses_its_position_in_the_message_and_keeps_it_in_the_console()
    {
        var result = await Run("yaapic.test('boom', function() error('bad thing') end)\nyaapic.test('plain', function() error('no position', 0) end)\nyaapic.test('value', function() error({ code = 5 }) end)");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(["bad thing", "no position", "{ code: 5 }"], result.Tests.Select(t => t.Message));
        var line = Assert.Single(result.Console);
        Assert.Equal(ConsoleLevel.Error, line.Level);
        Assert.Equal($"test 'boom' failed at {Origin}:1: bad thing", line.Message);
    }

    [Fact]
    public async Task A_test_without_a_function_passes_and_a_non_function_fails()
    {
        var result = await Run("yaapic.test('none') yaapic.test('nothing', nil) yaapic.test('bad', 5) yaapic.test('table', {})");

        Assert.Equal([true, true, false, false], result.Tests.Select(t => t.Passed));
        Assert.Equal("TypeError: 5 is not a function", result.Tests[2].Message);
        Assert.Equal("TypeError: {} is not a function", result.Tests[3].Message);
    }

    [Fact]
    public async Task Skip_logs_an_info_line_and_records_nothing_and_index_counts_the_results()
    {
        var result = await Run(
            """
            yaapic.test.skip('later', function() error('x') end)
            yaapic.test('a', function() end)
            yaapic.test('b', function() end)
            console.log(yaapic.test.index())
            """);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(["a", "b"], result.Tests.Select(t => t.Name));
        Assert.Equal(["skipped later", "2"], result.Console.Select(e => e.Message));
        Assert.Equal(ConsoleLevel.Info, result.Console[0].Level);
    }

    [Fact]
    public async Task A_statement_failing_outside_a_test_fails_the_run_but_keeps_the_earlier_tests()
    {
        var result = await Run("yaapic.test('kept', function() end) yaapic.test('also', function() yaapic.expect(1).to.equal(2) end) error('outside')");

        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.EndsWith("outside", result.Error?.Message);
        Assert.Equal(["kept", "also"], result.Tests.Select(t => t.Name));
    }

    [Fact]
    public async Task An_assertion_outside_a_test_fails_the_run()
    {
        var result = await Run("yaapic.expect(1).to.equal(2)");

        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.Equal("AssertionError: expected 1 to equal 2", result.Error?.Message);
    }

    [Fact]
    public async Task An_assertion_error_can_be_caught_by_the_script()
    {
        var result = await Run("local ok, e = pcall(yaapic.expect(1).to.equal, 2) console.log(ok, tostring(e))");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("false AssertionError: expected 1 to equal 2", Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task A_test_that_loops_forever_times_out_instead_of_failing_and_keeps_the_earlier_tests()
    {
        var result = await Run("yaapic.test('before', function() end) yaapic.test('never', function() while true do end end)", Fast with { Timeout = TimeSpan.FromMilliseconds(300) });

        Assert.Equal(ScriptRunStatus.TimedOut, result.Status);
        Assert.Equal(["before"], result.Tests.Select(t => t.Name));
    }

    [Fact]
    public async Task Test_and_expect_are_read_only()
    {
        var result = await Run("local function msg(f) local ok, e = pcall(f) return (tostring(e):gsub('^.-:%d+: ', '')) end console.log(msg(function() yaapic.test.foo = 1 end), msg(function() yaapic.expect.foo = 1 end), msg(function() yaapic.test = 1 end))");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("yaapic.test is read-only yaapic.expect is read-only yaapic is read-only", Assert.Single(result.Console).Message);
    }
}
