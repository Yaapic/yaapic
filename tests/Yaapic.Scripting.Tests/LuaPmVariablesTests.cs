using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.Lua;

namespace Yaapic.Scripting.Tests;

/// <summary>The Lua <c>pm</c> variables and info, on the real Lua engine over a real <see cref="ScriptVariableOverlay"/>.</summary>
public sealed class LuaPmVariablesTests
{
    private const string Origin = "pre-request · Users API";

    private static readonly Guid RequestId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static ScriptVariableOverlay NewOverlay(bool environment = true) =>
        new(
            new(
                new(Guid.NewGuid(), "dev", [new("env", "e"), new("shared", "from-env"), new("off", "x", false)]),
                [new("col", "c"), new("shared", "from-col"), new("deep", "from-col")],
                [new("glob", "g"), new("shared", "from-glob"), new("deep", "from-glob")]),
            environment,
            true);

    private static PmVariablesTests.OverlayHost NewHost(ScriptVariableOverlay overlay, string eventName = "prerequest") =>
        new(overlay, new(eventName, "Users", RequestId, Origin));

    private static Task<ScriptRunResult> Run(string source, ScriptVariableOverlay overlay, string eventName = "prerequest") =>
        new LuaEngine().RunAsync(
            new(new(ScriptLanguage.Lua, source), ScriptPhase.PreRequest, Origin, NewHost(overlay, eventName), ScriptLimits.Default),
            TestContext.Current.CancellationToken);

    private static async Task<string> Eval(string expression, ScriptVariableOverlay? overlay = null)
    {
        var result = await Run($"console.log({expression})", overlay ?? NewOverlay());
        Assert.True(result.Succeeded, result.Error?.Message);
        return Assert.Single(result.Console).Message;
    }

    private static async Task<string> ErrorOf(string call)
    {
        var result = await Run($"local ok, e = pcall(function() {call} end) console.log(ok, e)", NewOverlay());
        Assert.True(result.Succeeded, result.Error?.Message);
        return Assert.Single(result.Console).Message;
    }

    [Theory]
    [InlineData("pm.environment", "env", "e")]
    [InlineData("pm.collectionVariables", "col", "c")]
    [InlineData("pm.globals", "glob", "g")]
    public async Task Each_scope_supports_get_set_unset_has_clear_and_to_object(string scope, string existing, string existingValue)
    {
        var overlay = NewOverlay();
        var result = await Run(
            $$"""
            local s = {{scope}}
            local out = {}
            out[#out + 1] = tostring(s.get('{{existing}}')) .. tostring(s.has('{{existing}}')) .. tostring(s.get('missing')) .. tostring(s.has('missing'))
            s.set('n', 'v')
            out[#out + 1] = tostring(s.get('n')) .. tostring(s.has('n'))
            s.unset('n')
            out[#out + 1] = tostring(s.get('n')) .. tostring(s.has('n'))
            out[#out + 1] = tostring(s.to_object()['{{existing}}'] ~= nil) .. tostring(s.toObject == s.to_object)
            s.clear()
            out[#out + 1] = pm.json.encode(s.to_object())
            console.log(table.concat(out, '|'))
            """,
            overlay);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal($"{existingValue}truenilfalse|vtrue|nilfalse|truetrue|{{}}", Assert.Single(result.Console).Message);
    }

    [Theory]
    [InlineData("'s'", "s")]
    [InlineData("42", "42")]
    [InlineData("-7", "-7")]
    [InlineData("1.5", "1.5")]
    [InlineData("10 / 2", "5")]
    [InlineData("1 // 1", "1")]
    [InlineData("0/0", "NaN")]
    [InlineData("1/0", "Infinity")]
    [InlineData("-1/0", "-Infinity")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("nil", "")]
    [InlineData("pm.null", "")]
    [InlineData("{ a = { 1, 2 } }", "{\"a\":[1,2]}")]
    [InlineData("{ 1, 'a' }", "[1,\"a\"]")]
    [InlineData("pm.json.array({})", "[]")]
    public async Task Set_coerces_the_value_to_a_string(string value, string expected)
    {
        var overlay = NewOverlay();
        var result = await Run($"pm.environment.set('k', {value})", overlay);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.True(overlay.TryGet(ScriptScope.Environment, "k", out var stored));
        Assert.Equal(expected, stored);
    }

    [Fact]
    public async Task Yaapic_is_the_same_object_as_pm()
    {
        var result = await Run("console.log(yaapic == pm, yaapic.variables.get('shared') == pm.variables.get('shared'))", NewOverlay());
        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("true true", Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task Set_rejects_functions_and_tables_that_cannot_be_encoded()
    {
        Assert.Contains("cannot store value of type function", await ErrorOf("pm.environment.set('k', print)"), StringComparison.Ordinal);
        Assert.Contains("cannot encode function", await ErrorOf("pm.environment.set('k', { f = print })"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Layered_view_reads_local_then_environment_then_collection_then_globals()
    {
        Assert.Equal("from-env", await Eval("pm.variables.get('shared')"));
        Assert.Equal("from-col", await Eval("pm.variables.get('deep')"));
        Assert.Equal("g", await Eval("pm.variables.get('glob')"));
        Assert.Equal("local", await Eval("(function() pm.variables.set('shared', 'local') return pm.variables.get('shared') end)()"));
        Assert.Equal("false", await Eval("pm.variables.has('off')"));
        Assert.Equal("true", await Eval("pm.variables.has('env')"));
    }

    [Fact]
    public async Task Layered_to_object_merges_with_the_highest_layer_winning()
    {
        Assert.Equal(
            "{\"col\":\"c\",\"deep\":\"from-col\",\"env\":\"e\",\"glob\":\"g\",\"shared\":\"local\"}",
            await Eval("(function() pm.variables.set('shared', 'local') return pm.json.encode(pm.variables.toObject()) end)()"));
    }

    [Fact]
    public async Task Variables_write_unset_and_clear_act_on_the_local_layer_only()
    {
        var overlay = NewOverlay();
        var result = await Run(
            "pm.variables.set('a', '1') pm.variables.set('b', '2') pm.variables.set('env', 'shadow') pm.variables.unset('a') pm.variables.unset('env') console.log(pm.variables.has('a'), pm.variables.has('env'), pm.variables.has('b')) pm.variables.clear() console.log(pm.variables.has('b'), pm.variables.has('env'))",
            overlay);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(["false true true", "false true"], result.Console.Select(c => c.Message));
        Assert.True(overlay.TryGet(ScriptScope.Environment, "env", out var env));
        Assert.Equal("e", env);
        Assert.Empty(overlay.Changes);
    }

    [Fact]
    public async Task Replace_in_resolves_variables_dynamic_variables_and_leaves_unresolved_names()
    {
        Assert.Equal("id=e/g/{{nope}}", await Eval("pm.variables.replace_in('id={{env}}/{{glob}}/{{nope}}')"));
        Assert.Equal("e", await Eval("pm.variables.replaceIn('{{env}}')"));
        Assert.True(Guid.TryParse(await Eval("pm.variables.replace_in('{{$guid}}')"), out _));
        Assert.Equal("nil", await Eval("pm.environment.replace_in"));
    }

    [Fact]
    public async Task Writes_to_scopes_are_recorded_as_changes()
    {
        var overlay = NewOverlay();
        var result = await Run("pm.environment.set('a', '1') pm.collectionVariables.set('b', '2') pm.globals.set('c', '3') pm.globals.unset('glob')", overlay);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(
            [VariableScopeKind.Environment, VariableScopeKind.Collection, VariableScopeKind.Globals, VariableScopeKind.Globals],
            overlay.Changes.Select(c => c.Scope));
    }

    [Fact]
    public async Task Writing_a_missing_environment_falls_back_to_local_with_a_warning()
    {
        var overlay = NewOverlay(environment: false);
        var result = await Run("pm.environment.set('x', '1') console.log(pm.variables.get('x'))", overlay);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("1", Assert.Single(result.Console).Message);
        Assert.Single(overlay.Warnings);
        Assert.Empty(overlay.Changes);
    }

    [Theory]
    [InlineData("pm.environment.get(1)", "'get' (string expected, got number)")]
    [InlineData("pm.globals.set(nil, 'x')", "'set' (string expected, got nil)")]
    [InlineData("pm.collectionVariables.unset({})", "'unset' (string expected, got table)")]
    [InlineData("pm.variables.has(true)", "'has' (string expected, got boolean)")]
    [InlineData("pm.variables.replace_in(5)", "'replace_in' (string expected, got number)")]
    public async Task A_non_string_argument_raises_a_bad_argument_error(string call, string detail)
    {
        var message = await ErrorOf(call);

        Assert.StartsWith("false ", message, StringComparison.Ordinal);
        Assert.EndsWith("bad argument #1 to " + detail, message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("prerequest")]
    [InlineData("test")]
    public async Task Info_exposes_the_event_and_the_request_with_both_spellings(string eventName)
    {
        var result = await Run(
            "local i = pm.info console.log(i.event_name, i.eventName, i.request_name, i.requestName, i.request_id, i.requestId, i.iteration, i.iteration_count, i.iterationCount)",
            NewOverlay(),
            eventName);

        Assert.Equal(
            $"{eventName} {eventName} Users Users {RequestId} {RequestId} 0 1 1",
            Assert.Single(result.Console).Message);
    }

    [Theory]
    [InlineData("local _ = pm.execution.setNextRequest", "pm.execution.setNextRequest")]
    [InlineData("local _ = pm.iterationData.get", "pm.iterationData.get")]
    [InlineData("local _ = pm.vault.get", "pm.vault.get")]
    [InlineData("local _ = pm.visualizer.set", "pm.visualizer.set")]
    [InlineData("pm.visualizer.x = 1", "pm.visualizer.x")]
    [InlineData("pm.require('x')", "pm.require")]
    [InlineData("local _ = pm.environment.name", "pm.environment.name")]
    public async Task Unsupported_members_raise_the_exact_message(string statement, string path)
    {
        var message = await ErrorOf(statement);

        Assert.EndsWith($"{path} is not supported in Yaapic", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Legacy_postman_aliases_are_not_provided_in_Lua() =>
        Assert.Equal("nil", await Eval("postman"));

    [Fact]
    public async Task Scripts_cannot_overwrite_pm_members()
    {
        Assert.EndsWith("pm is read-only", await ErrorOf("pm.environment = 3"), StringComparison.Ordinal);
        Assert.EndsWith("pm is read-only", await ErrorOf("pm.extra = 3"), StringComparison.Ordinal);
        Assert.Equal("pm is protected", await Eval("getmetatable(pm)"));
        Assert.Equal("function Users", await Eval("type(pm.environment.get) .. ' ' .. pm.info.request_name"));
    }

    [Fact]
    public async Task Rawset_on_the_global_only_affects_the_script_itself()
    {
        var overlay = NewOverlay();
        var first = await Run("rawset(_G, 'pm', 1)", overlay);
        var second = await Run("console.log(type(pm))", overlay);

        Assert.True(first.Succeeded, first.Error?.Message);
        Assert.Equal("table", Assert.Single(second.Console).Message);
    }

    [Fact]
    public async Task A_second_block_in_the_same_run_sees_the_changes_of_the_first()
    {
        var overlay = NewOverlay();
        var host = NewHost(overlay);
        LuaEngine engine = new();

        var first = await engine.RunAsync(
            new(new(ScriptLanguage.Lua, "pm.variables.set('t', 'one') pm.environment.set('e2', 'two')"), ScriptPhase.PreRequest, Origin, host, ScriptLimits.Default),
            TestContext.Current.CancellationToken);
        var second = await engine.RunAsync(
            new(new(ScriptLanguage.Lua, "console.log(pm.variables.get('t'), pm.environment.get('e2'))"), ScriptPhase.PreRequest, Origin, host, ScriptLimits.Default),
            TestContext.Current.CancellationToken);

        Assert.True(first.Succeeded, first.Error?.Message);
        Assert.Equal("one two", Assert.Single(second.Console).Message);
    }
}
