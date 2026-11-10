using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.Lua;
using Yaapic.Scripting.Tests.Conformance;

namespace Yaapic.Scripting.Tests;

/// <summary>The Lua <c>pm.test</c> and core <c>pm.expect</c> assertions on the real Lua engine, recording tests on a fake host.</summary>
public sealed class LuaPmTestExpectTests
{
    private const string Origin = "post-response · Users API";

    private static readonly ScriptLimits Fast = ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(10) };

    private static Task<ScriptRunResult> Run(string source, ScriptLimits? limits = null) =>
        new LuaEngine().RunAsync(
            new(new(ScriptLanguage.Lua, source), ScriptPhase.PostResponse, Origin, new QuickJsEngineTests.FakeHost(), limits ?? Fast),
            TestContext.Current.CancellationToken);

    /// <summary>Runs the statements inside one pm.test and returns its result.</summary>
    private static async Task<Yaapic.Domain.Scripting.TestResult> One(string statements)
    {
        var result = await Run($"pm.test('t', function() {statements} end)");
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
    [InlineData("pm.expect(204).to.equal(204)")]
    [InlineData("pm.expect(1).to.is_not.equal(2) pm.expect(1).to.equal(1.0) pm.expect('a').to.equals('a') pm.expect(1).to.eq(1)")]
    [InlineData("local t = {} pm.expect(t).to.equal(t) pm.expect({}).to.is_not.equal({})")]
    [InlineData("pm.expect(0/0).to.is_not.equal(0/0) pm.expect(0/0).to.is_not.eql(0/0)")]
    [InlineData("pm.expect({ a = { 1, { b = 2 } } }).to.eql({ a = { 1, { b = 2 } } }) pm.expect({ a = 1 }).to.eqls({ a = 1 })")]
    [InlineData("pm.expect({ 1, 2 }).to.deep.equal({ 1, 2 }) pm.expect({ a = 1 }).to.is_not.eql({ a = 2 }) pm.expect({ 1 }).to.is_not.eql({ a = 1 })")]
    [InlineData("pm.expect(pm.null).to.eql(pm.null) pm.expect(pm.null).to.is_not.eql({}) pm.expect(pm.null).to.is_not.eql(nil)")]
    [InlineData("pm.expect(pm.json.decode('[1,[2,{\"a\":null}]]')).to.eql(pm.json.decode('[1,[2,{\"a\":null}]]'))")]
    [InlineData("pm.expect(pm.json.decode('[]')).to.is_not.eql({}) pm.expect(pm.json.decode('[]')).to.eql(pm.json.array({}))")]
    [InlineData("local a = {} a.self = a local b = {} b.self = b pm.expect(a).to.eql(b)")]
    [InlineData("pm.expect(1).to.be.ok() pm.expect(0).to.be.ok() pm.expect('').to.be.ok() pm.expect(false).to.is_not.be.ok() pm.expect(nil).to.be.is_not.ok()")]
    [InlineData("pm.expect(true).to.be.is_true() pm.expect(false).to.be.is_false() pm.expect(1).to.is_not.be.is_true() pm.expect(nil).to.is_not.be.is_false()")]
    [InlineData("pm.expect(pm.null).to.be.is_null() pm.expect(nil).to.be.is_nil() pm.expect(nil).to.is_not.be.is_null() pm.expect(pm.null).to.is_not.be.is_nil()")]
    [InlineData("pm.expect(0/0).to.be.is_nan() pm.expect(1).to.is_not.be.is_nan() pm.expect('x').to.is_not.be.is_nan()")]
    [InlineData("pm.expect(0).to.exist() pm.expect(false).to.exist() pm.expect(nil).to.is_not.exist() pm.expect(pm.null).to.is_not.exist()")]
    [InlineData("pm.expect('').to.be.empty() pm.expect({}).to.be.empty() pm.expect(pm.json.array({})).to.be.empty() pm.expect({ 0 }).to.is_not.be.empty() pm.expect({ a = 1 }).to.is_not.be.empty() pm.expect('a').to.is_not.be.empty()")]
    [InlineData("pm.expect('a').to.be.a('string') pm.expect(1).to.be.a('number') pm.expect(true).to.be.a('boolean') pm.expect(print).to.be.a('function')")]
    [InlineData("pm.expect(1).to.be.an('integer') pm.expect(1.5).to.be.a('float') pm.expect(2.0).to.be.a('float') pm.expect(1.5).to.is_not.be.an('integer')")]
    [InlineData("pm.expect({ 1, 2 }).to.be.an('array') pm.expect(pm.json.array({})).to.be.an('array') pm.expect({ a = 1 }).to.be.an('object') pm.expect({}).to.be.an('object') pm.expect({ 1 }).to.is_not.be.an('object')")]
    [InlineData("pm.expect({}).to.be.a('table') pm.expect({ 1 }).to.be.a('table') pm.expect(pm.null).to.is_not.be.a('table') pm.expect(pm.null).to.is_not.be.an('object') pm.expect(pm.null).to.be.a('null')")]
    [InlineData("pm.expect(nil).to.be.a('nil') pm.expect('a').to.be.a('STRING') pm.expect(1).to.is_not.be.a('string')")]
    [InlineData("pm.expect(6).to.be.above(5).also.below(7).also.at.least(6).also.at.most(6) pm.expect(6).to.be.gt(5).also.lt(7).also.gte(6).also.lte(6)")]
    [InlineData("pm.expect(5).to.is_not.be.above(5) pm.expect(5.5).to.be.above(5)")]
    [InlineData("pm.expect(3).to.be.within(1, 5) pm.expect(1).to.be.within(1, 5) pm.expect(9).to.is_not.be.within(1, 5)")]
    [InlineData("pm.expect(1).to.be.close_to(1.4, 0.5) pm.expect(1).to.be.close_to(1, 0) pm.expect(1).to.is_not.be.close_to(3, 1) pm.expect(1).to.be.closeTo(1, 0)")]
    [InlineData("pm.expect({ 1, 2 }).to.have.length(2) pm.expect('abc').to.have.length_of(3) pm.expect({ 1 }).to.is_not.have.length(2) pm.expect({}).to.have.length(0) pm.expect(pm.json.decode('[1,2,3]')).to.have.lengthOf(3)")]
    [InlineData("pm.expect(1).to.be.that.is.which.has.have.with.at.of.same.but.does.still.also.been.equal(1)")]
    [InlineData("local a = pm.expect(1) pm.expect(a.to).to.equal(a) pm.expect(a.equal(1)).to.equal(a)")]
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
            "local ok, e = pcall(function() pm.expect(1).to.equal(2) end) console.log(type(e), e.name, e.message, e.actual, e.expected, tostring(e), getmetatable(e).__tostring ~= nil)");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("table AssertionError expected 1 to equal 2 1 2 AssertionError: expected 1 to equal 2 true", Assert.Single(result.Console).Message);
    }

    [Theory]
    [InlineData("pm.expect({ ['a-b'] = 'x', c = pm.null, z = { 1 } }).to.equal(1)", "AssertionError: expected { 'a-b': 'x', c: null, z: [ 1 ] } to equal 1")]
    [InlineData("pm.expect({ b = 1, a = 2 }).to.equal(1)", "AssertionError: expected { a: 2, b: 1 } to equal 1")]
    [InlineData("pm.expect(nil).to.equal(1)", "AssertionError: expected undefined to equal 1")]
    [InlineData("pm.expect(pm.null).to.equal(1)", "AssertionError: expected null to equal 1")]
    [InlineData("pm.expect(1.5).to.equal(1)", "AssertionError: expected 1.5 to equal 1")]
    [InlineData("pm.expect(2.0).to.equal(1)", "AssertionError: expected 2 to equal 1")]
    [InlineData("pm.expect(1/0).to.equal(1)", "AssertionError: expected Infinity to equal 1")]
    [InlineData("pm.expect(-1/0).to.equal(1)", "AssertionError: expected -Infinity to equal 1")]
    [InlineData("pm.expect('a\\\\b').to.equal(1)", "AssertionError: expected 'a\\\\b' to equal 1")]
    [InlineData("pm.expect({}).to.equal(1)", "AssertionError: expected {} to equal 1")]
    [InlineData("pm.expect(pm.json.array({})).to.equal(1)", "AssertionError: expected [] to equal 1")]
    [InlineData("pm.expect(true).to.equal(1)", "AssertionError: expected true to equal 1")]
    [InlineData("pm.expect(print).to.equal(1)", "AssertionError: expected [Function (anonymous)] to equal 1")]
    [InlineData("local o = { n = 1 } o.self = o pm.expect(o).to.equal(1)", "AssertionError: expected { n: 1, self: [Circular] } to equal 1")]
    [InlineData("pm.expect(pm.json.decode('[1,{\"a\":[2]}]')).to.equal(1)", "AssertionError: expected [ 1, { a: [ 2 ] } ] to equal 1")]
    [InlineData("local e = pm.json.array({ 1 }) pm.expect({ e, e }).to.equal(1)", "AssertionError: expected [ [ 1 ], [ 1 ] ] to equal 1")]
    [InlineData("pm.expect({ [5] = 'a' }).to.equal(1)", "AssertionError: expected { '5': 'a' } to equal 1")]
    public async Task Values_are_inspected_like_the_javascript_engine(string statements, string message)
    {
        var test = await One(statements);

        Assert.Equal(message, test.Message);
    }

    [Theory]
    [InlineData("pm.expect(1).to.above('x')", "bad argument #1 to 'above' (number expected, got string)")]
    [InlineData("pm.expect(1).to.be.within(1)", "bad argument #2 to 'within' (number expected, got nil)")]
    [InlineData("pm.expect(1).to.be.close_to(1)", "bad argument #2 to 'close_to' (number expected, got nil)")]
    [InlineData("pm.expect(1).to.have.length('x')", "bad argument #1 to 'length' (number expected, got string)")]
    [InlineData("pm.expect(1).to.be.unknown()", "pm.expect(...).unknown is not supported in Yaapic")]
    [InlineData("pm.expect(1).to.equal = 5", "pm.expect(...).equal cannot be assigned")]
    [InlineData("pm.expect(1).to.be.empty()", ".empty was passed non-string primitive 1")]
    [InlineData("pm.expect(nil).to.be.empty()", ".empty was passed undefined")]
    [InlineData("pm.expect(pm.null).to.be.empty()", ".empty was passed null")]
    [InlineData("pm.expect(1).to.have.length(1)", "AssertionError: expected 1 to have a length")]
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
            pm.test('first', function() pm.expect(1).to.equal(1) end)
            pm.test('second', function() pm.expect(409).to.equal(204) end)
            pm.test('third', function() local x = nil; return x.y end)
            pm.test('first', function() end)
            pm.test(42, function() end)
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
        var result = await Run("pm.test('boom', function() error('bad thing') end)\npm.test('plain', function() error('no position', 0) end)\npm.test('value', function() error({ code = 5 }) end)");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(["bad thing", "no position", "{ code: 5 }"], result.Tests.Select(t => t.Message));
        var line = Assert.Single(result.Console);
        Assert.Equal(ConsoleLevel.Error, line.Level);
        Assert.Equal($"test 'boom' failed at {Origin}:1: bad thing", line.Message);
    }

    [Fact]
    public async Task A_test_without_a_function_passes_and_a_non_function_fails()
    {
        var result = await Run("pm.test('none') pm.test('nothing', nil) pm.test('bad', 5) pm.test('table', {})");

        Assert.Equal([true, true, false, false], result.Tests.Select(t => t.Passed));
        Assert.Equal("TypeError: 5 is not a function", result.Tests[2].Message);
        Assert.Equal("TypeError: {} is not a function", result.Tests[3].Message);
    }

    [Fact]
    public async Task Skip_logs_an_info_line_and_records_nothing_and_index_counts_the_results()
    {
        var result = await Run(
            """
            pm.test.skip('later', function() error('x') end)
            pm.test('a', function() end)
            pm.test('b', function() end)
            console.log(pm.test.index())
            """);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(["a", "b"], result.Tests.Select(t => t.Name));
        Assert.Equal(["skipped later", "2"], result.Console.Select(e => e.Message));
        Assert.Equal(ConsoleLevel.Info, result.Console[0].Level);
    }

    [Fact]
    public async Task A_statement_failing_outside_a_test_fails_the_run_but_keeps_the_earlier_tests()
    {
        var result = await Run("pm.test('kept', function() end) pm.test('also', function() pm.expect(1).to.equal(2) end) error('outside')");

        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.EndsWith("outside", result.Error?.Message);
        Assert.Equal(["kept", "also"], result.Tests.Select(t => t.Name));
    }

    [Fact]
    public async Task An_assertion_outside_a_test_fails_the_run()
    {
        var result = await Run("pm.expect(1).to.equal(2)");

        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.Equal("AssertionError: expected 1 to equal 2", result.Error?.Message);
    }

    [Fact]
    public async Task An_assertion_error_can_be_caught_by_the_script()
    {
        var result = await Run("local ok, e = pcall(pm.expect(1).to.equal, 2) console.log(ok, tostring(e))");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("false AssertionError: expected 1 to equal 2", Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task A_test_that_loops_forever_times_out_instead_of_failing_and_keeps_the_earlier_tests()
    {
        var result = await Run("pm.test('before', function() end) pm.test('never', function() while true do end end)", Fast with { Timeout = TimeSpan.FromMilliseconds(300) });

        Assert.Equal(ScriptRunStatus.TimedOut, result.Status);
        Assert.Equal(["before"], result.Tests.Select(t => t.Name));
    }

    [Fact]
    public async Task Test_and_expect_are_read_only()
    {
        var result = await Run("local function msg(f) local ok, e = pcall(f) return (tostring(e):gsub('^.-:%d+: ', '')) end console.log(msg(function() pm.test.foo = 1 end), msg(function() pm.expect.foo = 1 end), msg(function() pm.test = 1 end))");

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("pm.test is read-only pm.expect is read-only pm is read-only", Assert.Single(result.Console).Message);
    }
}
