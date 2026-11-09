using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.QuickJs;
using Yaapic.Scripting.Tests.Conformance;

namespace Yaapic.Scripting.Tests;

/// <summary><c>pm.test</c> and the core <c>pm.expect</c> assertions on the real QuickJS engine, recording tests on a fake host.</summary>
public sealed class PmTestExpectTests
{
    private const string Origin = "post-response · Users API";

    private static readonly ScriptLimits Fast = ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(10) };

    private static Task<ScriptRunResult> Run(string source, ScriptLimits? limits = null) =>
        new QuickJsEngine().RunAsync(
            new(new(ScriptLanguage.JavaScript, source), ScriptPhase.PostResponse, Origin, new QuickJsEngineTests.FakeHost(), limits ?? Fast),
            TestContext.Current.CancellationToken);

    /// <summary>Runs the statements inside one pm.test and returns its result.</summary>
    private static async Task<Yaapic.Domain.Scripting.TestResult> One(string statements)
    {
        var result = await Run($"pm.test('t', () => {{ {statements} }});");
        Assert.True(result.Succeeded, result.Error?.Message);
        return Assert.Single(result.Tests);
    }

    [Theory]
    [InlineData("pm.expect(0).to.be.ok", "AssertionError: expected 0 to be truthy")]
    [InlineData("pm.expect(new Set()).to.not.be.empty", "AssertionError: expected Set(0) {} not to be empty")]
    [InlineData("pm.expect([1]).to.have.lengthOf.above(2)", "AssertionError: expected [ 1 ] to have a length above 2 but got 1")]
    [InlineData("pm.expect([1, 2, 3]).to.have.lengthOf.below(2)", "AssertionError: expected [ 1, 2, 3 ] to have a length below 2 but got 3")]
    [InlineData("pm.expect([1, 2, 3]).to.have.lengthOf.within(1, 2)", "AssertionError: expected [ 1, 2, 3 ] to have a length within 1..2")]
    [InlineData("throw new TypeError('x is not a function')", "TypeError: x is not a function")]
    [InlineData("throw 'plain'", "plain")]
    public async Task A_failing_assertion_reports_the_exact_chai_message(string statements, string message)
    {
        var test = await One(statements);

        Assert.False(test.Passed);
        Assert.Equal(message, test.Message);
    }

    [Theory]
    [MemberData(nameof(ExpectMessages.JavaScript), MemberType = typeof(ExpectMessages))]
    public async Task A_shared_failing_assertion_reports_the_same_message_as_lua(string statements, string message)
    {
        var test = await One(statements);

        Assert.False(test.Passed);
        Assert.Equal(message, test.Message);
    }

    [Theory]
    [InlineData("pm.expect(204).to.equal(204)")]
    [InlineData("pm.expect(1).to.not.equal(2)")]
    [InlineData("pm.expect(NaN).to.eql(NaN)")]
    [InlineData("pm.expect(NaN).to.not.equal(NaN)")]
    [InlineData("pm.expect({ a: [1, { b: 2 }] }).to.eql({ a: [1, { b: 2 }] })")]
    [InlineData("pm.expect(new Date(5)).to.eql(new Date(5))")]
    [InlineData("pm.expect(/a+/g).to.eql(/a+/g)")]
    [InlineData("pm.expect(new Map([[1, { a: 1 }]])).to.eql(new Map([[1, { a: 1 }]]))")]
    [InlineData("pm.expect(new Set([1, 2])).to.deep.equal(new Set([2, 1]))")]
    [InlineData("pm.expect({ a: 1 }).to.not.eql({ a: 2 })")]
    [InlineData("pm.expect([1]).to.not.eql({ 0: 1 })")]
    [InlineData("pm.expect(1).to.be.ok; pm.expect('').to.not.be.ok")]
    [InlineData("pm.expect(true).to.be.true; pm.expect(false).to.be.false; pm.expect(null).to.be.null; pm.expect(undefined).to.be.undefined")]
    [InlineData("pm.expect(NaN).to.be.NaN; pm.expect(0).to.exist; pm.expect(null).to.not.exist")]
    [InlineData("pm.expect('').to.be.empty; pm.expect([]).to.be.empty; pm.expect({}).to.be.empty; pm.expect(new Map()).to.be.empty; pm.expect([0]).to.not.be.empty")]
    [InlineData("pm.expect('a').to.be.a('string'); pm.expect(1).to.be.a('number'); pm.expect(true).to.be.a('boolean'); pm.expect([]).to.be.an('array')")]
    [InlineData("pm.expect({}).to.be.an('object'); pm.expect(null).to.be.a('null'); pm.expect(undefined).to.be.an('undefined'); pm.expect(() => 1).to.be.a('function')")]
    [InlineData("pm.expect(new Date()).to.be.a('date'); pm.expect(/a/).to.be.a('RegExp'); pm.expect(new Error('x')).to.be.an('error'); pm.expect(new Map()).to.be.a('map'); pm.expect(new Set()).to.be.a('set')")]
    [InlineData("pm.expect(6).to.be.above(5).and.below(7).and.at.least(6).and.at.most(6); pm.expect(6).to.be.gt(5).and.lt(7).and.gte(6).and.lte(6)")]
    [InlineData("pm.expect(5).to.not.be.above(5); pm.expect(5).to.be.greaterThan(4); pm.expect(5).to.be.lessThan(6)")]
    [InlineData("pm.expect(3).to.be.within(1, 5); pm.expect(1).to.be.within(1, 5); pm.expect(9).to.not.be.within(1, 5)")]
    [InlineData("pm.expect(1).to.be.closeTo(1.4, 0.5); pm.expect(1).to.be.approximately(1, 0); pm.expect(1).to.not.be.closeTo(3, 1)")]
    [InlineData("pm.expect([1, 2]).to.have.length(2); pm.expect('abc').to.have.lengthOf(3); pm.expect(new Set([1])).to.have.lengthOf(1); pm.expect([1]).to.not.have.length(2)")]
    [InlineData("pm.expect([1, 2, 3]).to.have.lengthOf.above(2).and.below(4); pm.expect([1, 2]).to.have.lengthOf.within(1, 3); pm.expect([1]).to.have.lengthOf.at.least(1).and.at.most(1)")]
    [InlineData("pm.expect(1).to.be.that.is.which.has.have.with.at.of.same.but.does.still.also.been.equal(1)")]
    [InlineData("pm.expect(new Date(2)).to.be.above(new Date(1))")]
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
            "try { pm.expect(1).to.equal(2); } catch (e) { console.log(JSON.stringify([e.name, e.message, e.actual, e.expected, e.showDiff, e instanceof AssertionError, e instanceof Error])); }");

        Assert.Equal("[\"AssertionError\",\"expected 1 to equal 2\",1,2,true,true,true]", Assert.Single(result.Console).Message);
    }

    [Theory]
    [InlineData("pm.expect({ 'a-b': 'x', c: null, d: undefined }).to.equal(1)", "AssertionError: expected { 'a-b': 'x', c: null, d: undefined } to equal 1")]
    [InlineData("const o = { n: 1 }; o.self = o; pm.expect(o).to.equal(1)", "AssertionError: expected { n: 1, self: [Circular] } to equal 1")]
    [InlineData("pm.expect(new Map([['a', 1]])).to.equal(1)", "AssertionError: expected Map(1) { 'a' => 1 } to equal 1")]
    [InlineData("pm.expect(new Date(0)).to.equal(1)", "AssertionError: expected 1970-01-01T00:00:00.000Z to equal 1")]
    [InlineData("pm.expect(function named() {}).to.equal(1)", "AssertionError: expected [Function: named] to equal 1")]
    public async Task Values_are_inspected_like_chai(string statements, string message)
    {
        var test = await One(statements);

        Assert.Equal(message, test.Message);
    }

    [Fact]
    public async Task Tests_are_recorded_once_in_order_and_an_error_does_not_stop_the_next_test()
    {
        var result = await Run(
            """
            pm.test('first', () => { pm.expect(1).to.equal(1); });
            pm.test('second', () => { pm.expect(409).to.equal(204); });
            pm.test('third', () => { null.x; });
            pm.test('first', () => {});
            pm.test(42);
            """);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(["first", "second", "third", "first", "42"], result.Tests.Select(t => t.Name));
        Assert.Equal([true, false, false, true, true], result.Tests.Select(t => t.Passed));
        Assert.Equal("AssertionError: expected 409 to equal 204", result.Tests[1].Message);
        Assert.StartsWith("TypeError: ", result.Tests[2].Message);
        Assert.All(result.Tests, t => Assert.Equal(Origin, t.Origin));
    }

    [Fact]
    public async Task A_test_without_a_function_passes_and_a_non_function_fails()
    {
        var result = await Run("pm.test('none'); pm.test('nothing', undefined); pm.test('bad', 5);");

        Assert.Equal([true, true, false], result.Tests.Select(t => t.Passed));
        Assert.Equal("TypeError: 5 is not a function", result.Tests[2].Message);
    }

    [Fact]
    public async Task Done_style_tests_pass_or_fail_through_the_callback()
    {
        var result = await Run(
            """
            pm.test('ok', (done) => { done(); });
            pm.test('err', (done) => { done(new Error('late')); });
            pm.test('text', (done) => { done('why'); });
            pm.test('twice', (done) => { done(); done(new Error('ignored')); });
            pm.test('throws', (done) => { throw new Error('sync'); });
            pm.test('assertion', (done) => { try { pm.expect(1).to.equal(2); done(); } catch (e) { done(e); } });
            """);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal([true, false, false, true, false, false], result.Tests.Select(t => t.Passed));
        Assert.Equal("Error: late", result.Tests[1].Message);
        Assert.Equal("Error: why", result.Tests[2].Message);
        Assert.Equal("Error: sync", result.Tests[4].Message);
        Assert.Equal("AssertionError: expected 1 to equal 2", result.Tests[5].Message);
    }

    [Fact]
    public async Task Promise_tests_are_recorded_when_they_settle()
    {
        var result = await Run(
            """
            pm.test('resolved', () => Promise.resolve(1));
            pm.test('rejected', () => Promise.reject(new Error('no')));
            pm.test('async fail', async () => { await null; pm.expect(1).to.equal(2); });
            pm.test('async ok', async () => { await null; });
            pm.test('sync after', () => {});
            """);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("sync after", result.Tests[0].Name);
        Assert.Equal(5, result.Tests.Count);
        Assert.True(result.Tests.Single(t => t.Name == "resolved").Passed);
        Assert.Equal("Error: no", result.Tests.Single(t => t.Name == "rejected").Message);
        Assert.Equal("AssertionError: expected 1 to equal 2", result.Tests.Single(t => t.Name == "async fail").Message);
        Assert.True(result.Tests.Single(t => t.Name == "async ok").Passed);
    }

    [Theory]
    [InlineData("pm.test('never', () => new Promise(() => {}));")]
    [InlineData("pm.test('never', (done) => {});")]
    public async Task A_test_that_never_completes_records_nothing_and_times_out(string source)
    {
        var result = await Run($"pm.test('before', () => {{}}); {source}");

        Assert.Equal(ScriptRunStatus.TimedOut, result.Status);
        Assert.Equal(["before"], result.Tests.Select(t => t.Name));
    }

    [Fact]
    public async Task Skip_logs_an_info_line_and_records_nothing_and_index_counts_the_results()
    {
        var result = await Run(
            """
            pm.test.skip('later', () => { throw new Error('x'); });
            pm.test('a', () => {});
            pm.test('b', () => {});
            console.log(pm.test.index());
            """);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(["a", "b"], result.Tests.Select(t => t.Name));
        Assert.Equal(["skipped later", "2"], result.Console.Select(e => e.Message));
        Assert.Equal(ConsoleLevel.Info, result.Console[0].Level);
    }

    [Fact]
    public async Task A_statement_throwing_outside_a_test_fails_the_run_but_keeps_the_earlier_tests()
    {
        var result = await Run("pm.test('kept', () => {}); pm.test('also', () => { pm.expect(1).to.equal(2); }); throw new Error('outside');");

        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.Equal("outside", result.Error?.Message);
        Assert.Equal(["kept", "also"], result.Tests.Select(t => t.Name));
    }

    [Fact]
    public async Task An_assertion_outside_a_test_fails_the_run()
    {
        var result = await Run("pm.expect(1).to.equal(2);");

        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.Equal("expected 1 to equal 2", result.Error?.Message);
    }
}
