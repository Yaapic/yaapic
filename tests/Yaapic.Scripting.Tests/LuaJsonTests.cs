using System.Diagnostics;
using System.Text;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.Lua;

namespace Yaapic.Scripting.Tests;

/// <summary>The Lua JSON codec and the typed host wrapper, run on the real Lua engine with a fake host.</summary>
public sealed class LuaJsonTests
{
    private static readonly ScriptLimits Fast = ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(20) };

    private static async Task<ScriptRunResult> Run(string source, QuickJsEngineTests.FakeHost? host = null)
    {
        var result = await new LuaEngine().RunAsync(
            new(new(ScriptLanguage.Lua, source), ScriptPhase.PreRequest, "o", host ?? new QuickJsEngineTests.FakeHost(), Fast),
            TestContext.Current.CancellationToken);
        Assert.True(result.Status == ScriptRunStatus.Succeeded, result.Error?.Message);
        return result;
    }

    private static async Task<string> Eval(string expression, QuickJsEngineTests.FakeHost? host = null) =>
        Assert.Single((await Run($"console.log({expression})", host)).Console).Message;

    // Decodes the text and prints it re-encoded (keys sorted) or the decode error.
    private static Task<string> Decoded(string json) =>
        Eval($"(function() local ok, v = pcall(pm.json.decode, [==[{json}]==]) return ok and pm.json.encode(v) or v end)()");

    [Theory]
    [InlineData("[]", "[]")]
    [InlineData("{}", "{}")]
    [InlineData(" [ 1 , 2 ] ", "[1,2]")]
    [InlineData("""{"b":[1,{"c":null}],"a":true}""", """{"a":true,"b":[1,{"c":null}]}""")]
    [InlineData("""["\u00e9\u0041\n\"\\\/"]""", "[\"éA\\n\\\"\\\\/\"]")]
    [InlineData("""["\ud83d\ude00"]""", "[\"😀\"]")]
    [InlineData("9223372036854775807", "9223372036854775807")]
    [InlineData("-9223372036854775808", "-9223372036854775808")]
    [InlineData("1.5", "1.5")]
    [InlineData("0.1", "0.1")]
    [InlineData("1e3", "1000")]
    [InlineData("-0", "0")]
    [InlineData("[1E-2]", "[0.01]")]
    [InlineData("\"\\u0001\"", "\"\\u0001\"")]
    [InlineData("[null,null]", "[null,null]")]
    public async Task Valid_documents_decode(string json, string expected) => Assert.Equal(expected, await Decoded(json));

    [Theory]
    [InlineData("", "invalid JSON at position 1: unexpected end of input")]
    [InlineData("[1,]", "invalid JSON at position 4: unexpected character")]
    [InlineData("""{"a":1,}""", "invalid JSON at position 8: expected a string key")]
    [InlineData("[01]", "invalid JSON at position 2: leading zeros are not allowed")]
    [InlineData("-01", "invalid JSON at position 1: leading zeros are not allowed")]
    [InlineData("\"abc", "invalid JSON at position 1: unterminated string")]
    [InlineData("[1] x", "invalid JSON at position 5: trailing characters")]
    [InlineData("[1 2]", "invalid JSON at position 4: expected ',' or ']'")]
    [InlineData("1.", "invalid JSON at position 3: digits expected after the decimal point")]
    [InlineData("1e", "invalid JSON at position 3: digits expected in the exponent")]
    [InlineData("tru", "invalid JSON at position 1: unexpected character")]
    [InlineData("[\"\\x\"]", "invalid JSON at position 3: invalid escape")]
    [InlineData("[\"\\ud83d\"]", "invalid JSON at position 3: invalid surrogate pair")]
    [InlineData("[\"\\ude00\"]", "invalid JSON at position 3: lone low surrogate")]
    [InlineData("{'a':1}", "invalid JSON at position 2: expected a string key")]
    public async Task Invalid_documents_fail_with_a_position(string json, string expected) => Assert.Equal(expected, await Decoded(json));

    [Fact]
    public async Task Control_characters_in_strings_are_rejected() =>
        Assert.StartsWith("invalid JSON at position 4: control character", await Eval("(function() local ok, e = pcall(pm.json.decode, '[\"a\\nb\"]') return e end)()"), StringComparison.Ordinal);

    [Fact]
    public async Task Depth_100_decodes_and_depth_101_fails()
    {
        Assert.Equal(new string('[', 100) + new string(']', 100), await Decoded(new string('[', 100) + new string(']', 100)));
        Assert.Equal("invalid JSON at position 101: nesting deeper than 100", await Decoded(new string('[', 101) + new string(']', 101)));
    }

    [Fact]
    public async Task Number_types_follow_the_literal()
    {
        Assert.Equal(
            "integer float float float integer float",
            await Eval("table.concat({ math.type(pm.json.decode('1')), math.type(pm.json.decode('1.0')), math.type(pm.json.decode('1e2')), math.type(pm.json.decode('9223372036854775808')), math.type(pm.json.decode('-5')), math.type(pm.json.decode('-1.5')) }, ' ')"));
    }

    [Fact]
    public async Task Arrays_keep_their_length_with_trailing_nulls_and_null_is_the_sentinel()
    {
        var text = await Eval("(function() local a = pm.json.decode('[1,null,null]') return a.n .. ' ' .. tostring(a[3]) .. ' ' .. tostring(a[3] == pm.null) .. ' ' .. pm.json.decode('[]').n end)()");

        Assert.Equal("3 null true 0", text);
    }

    [Fact]
    public async Task The_null_sentinel_is_frozen()
    {
        Assert.Contains("read-only", await Eval("select(2, pcall(function() pm.null.x = 1 end))"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pm.json.encode({ 1, 2, 3 })", "[1,2,3]")]
    [InlineData("pm.json.encode({})", "{}")]
    [InlineData("pm.json.encode(pm.json.array())", "[]")]
    [InlineData("pm.json.encode({ b = 1, a = { 'x', pm.null, true } })", """{"a":["x",null,true],"b":1}""")]
    [InlineData("pm.json.encode({ [5] = 'a', [10] = 'b' })", """{"10":"b","5":"a"}""")]
    [InlineData("pm.json.encode('a\"\\\\\\n\\1é😀')", "\"a\\\"\\\\\\n\\u0001é😀\"")]
    [InlineData("pm.json.encode(1.5)", "1.5")]
    [InlineData("pm.json.encode(0.1)", "0.1")]
    [InlineData("pm.json.encode(1 / 3)", "0.3333333333333333")]
    [InlineData("pm.json.encode(-0.0)", "-0")]
    [InlineData("pm.json.encode(math.maxinteger)", "9223372036854775807")]
    [InlineData("pm.json.encode(nil)", "null")]
    public async Task Encode_produces_compact_json(string expression, string expected) => Assert.Equal(expected, await Eval(expression));

    [Theory]
    [InlineData("pm.json.encode({ 1, 2, a = 3 })", "cannot encode mixed table")]
    [InlineData("pm.json.encode(0/0)", "cannot encode")]
    [InlineData("pm.json.encode(math.huge)", "cannot encode inf")]
    [InlineData("pm.json.encode(print)", "cannot encode function")]
    [InlineData("pm.json.encode(coroutine.create(print))", "cannot encode thread")]
    [InlineData("pm.json.encode('\\xff')", "cannot encode invalid UTF-8")]
    [InlineData("pm.json.encode({ [true] = 1 })", "cannot encode boolean key")]
    public async Task Encode_rejects_what_json_cannot_hold(string expression, string message)
    {
        var text = await Eval($"select(2, pcall(function() return {expression} end))");

        Assert.StartsWith(message, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encode_depth_is_limited_to_100()
    {
        var text = await Eval("(function() local t = {} t.a = t return select(2, pcall(pm.json.encode, t)) end)()");

        Assert.Contains("nesting deeper than 100", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nested_data_round_trips_with_pm_null()
    {
        const string source = """
            local original = { name = 'x', list = pm.json.array({ 1, pm.null, { deep = pm.null } }), empty = pm.json.array(), flag = false, n = 2.5 }
            local text = pm.json.encode(original)
            local again = pm.json.encode(pm.json.decode(text))
            console.log(text == again, text)
            """;

        var message = Assert.Single((await Run(source)).Console).Message;

        Assert.Equal("""true {"empty":[],"flag":false,"list":[1,null,{"deep":null}],"n":2.5,"name":"x"}""", message);
    }

    [Fact]
    public async Task A_one_megabyte_document_decodes_within_a_second()
    {
        StringBuilder sb = new("[");
        for (var i = 0; sb.Length < 1_000_000; i++)
        {
            sb.Append(i == 0 ? "" : ",").Append("{\"id\":").Append(i).Append(",\"name\":\"item ").Append(i).Append("\",\"tags\":[\"a\",\"b\\n\",null],\"score\":").Append(i).Append(".25}");
        }

        sb.Append(']');
        var script = $"local a = pm.json.decode([==[{sb}]==]) console.log(a.n, a[a.n].id)";

        var sw = Stopwatch.StartNew();
        var result = await Run(script);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"took {sw.Elapsed}");
        Assert.Matches(@"^\d+ \d+$", Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task The_host_wrapper_sends_a_table_and_decodes_the_reply()
    {
        QuickJsEngineTests.FakeHost host = new();
        const string source = """
            console.log(tostring(__hostCall('var.set', { scope = 'globals', name = 'k', value = 'v' })))
            console.log(__hostCall('var.get', { scope = 'globals', name = 'k' }))
            console.log(__hostCall('var.has', { scope = 'globals', name = 'nope' }))
            local list = __hostCall('var.list', { scope = 'globals' })
            console.log(list.n)
            """;

        var result = await Run(source, host);

        Assert.Equal(["null", "v", "false", "0"], result.Console.Select(e => e.Message));
        Assert.Equal("v", host.Vars[(ScriptScope.Globals, "k")]);
    }

    [Fact]
    public async Task The_host_wrapper_raises_on_err_with_the_bare_message()
    {
        var text = await Eval("select(2, pcall(__hostCall, 'nope', {}))");

        Assert.Equal("Unknown host op 'nope'", text);
    }
}
