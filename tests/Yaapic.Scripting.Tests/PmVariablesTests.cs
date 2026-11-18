using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.QuickJs;

namespace Yaapic.Scripting.Tests;

/// <summary>The JavaScript <c>yaapic</c> variables and info, on the real QuickJS engine over a real <see cref="ScriptVariableOverlay"/>.</summary>
public sealed class PmVariablesTests
{
    private const string Origin = "pre-request · Users API";

    private static readonly Guid RequestId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static ScriptVariableOverlay NewOverlay(bool environment = true, bool collection = true) =>
        new(
            new(
                new(Guid.NewGuid(), "dev", [new("env", "e"), new("shared", "from-env"), new("off", "x", false)]),
                [new("col", "c"), new("shared", "from-col"), new("deep", "from-col")],
                [new("glob", "g"), new("shared", "from-glob"), new("deep", "from-glob")]),
            environment,
            collection);

    private static async Task<ScriptRunResult> Run(string source, ScriptVariableOverlay overlay, string eventName = "prerequest")
    {
        OverlayHost host = new(overlay, new(eventName, "Users", RequestId, Origin));
        return await new QuickJsEngine().RunAsync(
            new(new(ScriptLanguage.JavaScript, source), ScriptPhase.PreRequest, Origin, host, ScriptLimits.Default),
            TestContext.Current.CancellationToken);
    }

    // Logs the value of an expression as JSON and returns it.
    private static async Task<string> Eval(string expression, ScriptVariableOverlay? overlay = null)
    {
        var result = await Run($"console.log(JSON.stringify({expression}) ?? 'undefined')", overlay ?? NewOverlay());
        Assert.True(result.Succeeded, result.Error?.Message);
        return Assert.Single(result.Console).Message;
    }

    [Theory]
    [InlineData("yaapic.environment", "env", "e")]
    [InlineData("yaapic.collectionVariables", "col", "c")]
    [InlineData("yaapic.globals", "glob", "g")]
    public async Task Each_scope_supports_get_set_unset_has_clear_and_toObject(string scope, string existing, string existingValue)
    {
        var overlay = NewOverlay();
        var result = await Run(
            $$"""
            const s = {{scope}};
            const out = [];
            out.push(s.get('{{existing}}'), s.has('{{existing}}'), s.get('missing'), s.has('missing'));
            s.set('n', 'v');
            out.push(s.get('n'), s.has('n'));
            s.unset('n');
            out.push(s.get('n'), s.has('n'));
            out.push(Object.keys(s.toObject()).includes('{{existing}}'));
            s.clear();
            out.push(JSON.stringify(s.toObject()), s.set('z', 1));
            console.log(JSON.stringify(out));
            """,
            overlay);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal($"[\"{existingValue}\",true,null,false,\"v\",true,null,false,true,\"{{}}\",null]", Assert.Single(result.Console).Message);
    }

    [Theory]
    [InlineData("'s'", "s")]
    [InlineData("42", "42")]
    [InlineData("1.5", "1.5")]
    [InlineData("true", "true")]
    [InlineData("12n", "12")]
    [InlineData("null", "")]
    [InlineData("undefined", "")]
    [InlineData("({ a: [1, 2] })", "{\"a\":[1,2]}")]
    [InlineData("[1, 'a']", "[1,\"a\"]")]
    [InlineData("(() => { const o = {}; o.self = o; return o; })()", "[object Object]")]
    public async Task Set_coerces_the_value_to_a_string(string value, string expected)
    {
        var overlay = NewOverlay();
        var result = await Run($"yaapic.environment.set('k', {value});", overlay);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.True(overlay.TryGet(ScriptScope.Environment, "k", out var stored));
        Assert.Equal(expected, stored);
    }

    [Fact]
    public async Task Layered_view_reads_local_then_environment_then_collection_then_globals()
    {
        Assert.Equal("\"from-env\"", await Eval("yaapic.variables.get('shared')"));
        Assert.Equal("\"from-col\"", await Eval("yaapic.variables.get('deep')"));
        Assert.Equal("\"g\"", await Eval("yaapic.variables.get('glob')"));
        Assert.Equal("\"local\"", await Eval("(yaapic.variables.set('shared', 'local'), yaapic.variables.get('shared'))"));
        Assert.Equal("false", await Eval("yaapic.variables.has('off')"));
        Assert.Equal("true", await Eval("yaapic.variables.has('env')"));
    }

    [Fact]
    public async Task Layered_toObject_merges_with_the_highest_layer_winning()
    {
        Assert.Equal(
            "{\"glob\":\"g\",\"shared\":\"local\",\"deep\":\"from-col\",\"col\":\"c\",\"env\":\"e\"}",
            await Eval("(yaapic.variables.set('shared', 'local'), yaapic.variables.toObject())"));
    }

    [Fact]
    public async Task Variables_set_writes_the_local_layer_only()
    {
        var overlay = NewOverlay();
        var result = await Run("yaapic.variables.set('t', 'x'); yaapic.variables.set('env', 'shadow');", overlay);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.True(overlay.TryGet(ScriptScope.Local, "t", out _));
        Assert.False(overlay.TryGet(ScriptScope.Environment, "t", out _));
        Assert.True(overlay.TryGet(ScriptScope.Environment, "env", out var env));
        Assert.Equal("e", env);
        Assert.Empty(overlay.Changes);
    }

    [Fact]
    public async Task Variables_unset_and_clear_act_on_local_only()
    {
        var overlay = NewOverlay();
        var result = await Run(
            "yaapic.variables.set('a', '1'); yaapic.variables.set('b', '2'); yaapic.variables.unset('a'); yaapic.variables.unset('env'); console.log(yaapic.variables.has('a'), yaapic.variables.has('env'), yaapic.variables.has('b')); yaapic.variables.clear(); console.log(yaapic.variables.has('b'), yaapic.variables.has('env'));",
            overlay);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(["false true true", "false true"], result.Console.Select(c => c.Message));
    }

    [Fact]
    public async Task ReplaceIn_resolves_variables_dynamic_variables_and_leaves_unresolved_names()
    {
        Assert.Equal("\"id=e/g/{{nope}}\"", await Eval("yaapic.variables.replaceIn('id={{env}}/{{glob}}/{{nope}}')"));

        var guid = await Eval("yaapic.variables.replaceIn('{{$guid}}')");
        Assert.True(Guid.TryParse(guid.Trim('"'), out _), guid);
    }

    [Fact]
    public async Task Writes_to_scopes_are_recorded_as_changes()
    {
        var overlay = NewOverlay();
        var result = await Run("yaapic.environment.set('a', '1'); yaapic.collectionVariables.set('b', '2'); yaapic.globals.set('c', '3'); yaapic.globals.unset('glob');", overlay);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(4, overlay.Changes.Count);
        Assert.Equal(
            [VariableScopeKind.Environment, VariableScopeKind.Collection, VariableScopeKind.Globals, VariableScopeKind.Globals],
            overlay.Changes.Select(c => c.Scope));
    }

    [Fact]
    public async Task Writing_a_missing_environment_falls_back_to_local_with_a_warning()
    {
        var overlay = NewOverlay(environment: false);
        var result = await Run("yaapic.environment.set('x', '1'); console.log(yaapic.variables.get('x'));", overlay);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("1", Assert.Single(result.Console).Message);
        Assert.Single(overlay.Warnings);
        Assert.Empty(overlay.Changes);
    }

    [Theory]
    [InlineData("yaapic.environment.get(1)", "yaapic.environment.get: name must be a string")]
    [InlineData("yaapic.globals.set(null, 'x')", "yaapic.globals.set: name must be a string")]
    [InlineData("yaapic.collectionVariables.unset({})", "yaapic.collectionVariables.unset: name must be a string")]
    [InlineData("yaapic.variables.has(undefined)", "yaapic.variables.has: name must be a string")]
    public async Task A_non_string_name_throws_a_TypeError(string call, string message)
    {
        var result = await Run($"try {{ {call}; console.log('no throw'); }} catch (e) {{ console.log(e instanceof TypeError, e.message); }}", NewOverlay());

        Assert.Equal($"true {message}", Assert.Single(result.Console).Message);
    }

    [Theory]
    [InlineData("prerequest")]
    [InlineData("test")]
    public async Task Info_exposes_the_event_and_the_request(string eventName)
    {
        var result = await Run("console.log(JSON.stringify(yaapic.info));", NewOverlay(), eventName);

        Assert.Equal(
            $"{{\"eventName\":\"{eventName}\",\"requestName\":\"Users\",\"requestId\":\"{RequestId}\",\"iteration\":0,\"iterationCount\":1}}",
            Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task Legacy_postman_aliases_use_the_environment_and_globals()
    {
        var overlay = NewOverlay();
        var result = await Run(
            "postman.setEnvironmentVariable('pe', 1); postman.setGlobalVariable('pg', 'g2'); console.log(postman.getEnvironmentVariable('pe'), postman.getGlobalVariable('pg'), postman.getEnvironmentVariable('env')); postman.clearEnvironmentVariable('env'); postman.clearGlobalVariable('glob');",
            overlay);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("1 g2 e", Assert.Single(result.Console).Message);
        Assert.False(overlay.TryGet(ScriptScope.Environment, "env", out _));
        Assert.False(overlay.TryGet(ScriptScope.Globals, "glob", out _));
        Assert.True(overlay.TryGet(ScriptScope.Environment, "pe", out var pe));
        Assert.Equal("1", pe);
    }

    [Theory]
    [InlineData("yaapic.execution.setNextRequest", "yaapic.execution.setNextRequest")]
    [InlineData("yaapic.iterationData.get", "yaapic.iterationData.get")]
    [InlineData("yaapic.vault.get", "yaapic.vault.get")]
    [InlineData("yaapic.visualizer.set", "yaapic.visualizer.set")]
    [InlineData("yaapic.require('x')", "yaapic.require")]
    [InlineData("yaapic.environment.name", "yaapic.environment.name")]
    [InlineData("postman.setNextRequest('x')", "postman.setNextRequest")]
    [InlineData("postman.visualizer.set", "postman.visualizer.set")]
    public async Task Unsupported_members_throw_the_exact_message(string access, string path)
    {
        var result = await Run($"try {{ {access}; console.log('no throw'); }} catch (e) {{ console.log(e.message); }}", NewOverlay());

        Assert.Equal($"{path} is not supported in Yaapic", Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task Scripts_cannot_replace_yaapic()
    {
        var result = await Run("yaapic = 1; delete globalThis.yaapic; globalThis.yaapic = 2; yaapic.environment = 3; console.log(typeof yaapic.environment.get, yaapic.info.eventName);", NewOverlay());

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal("function prerequest", Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task Prototypes_are_frozen()
    {
        var result = await Run(
            "const proto = Object.getPrototypeOf(yaapic.globals); proto.get = () => 'hacked'; console.log(Object.isFrozen(proto), Object.isFrozen(yaapic.globals), yaapic.globals.get('glob'));",
            NewOverlay());

        Assert.Equal("true true g", Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task A_second_block_in_the_same_run_sees_the_changes_of_the_first()
    {
        var overlay = NewOverlay();
        var host = new OverlayHost(overlay, new("prerequest", "Users", RequestId, Origin));
        QuickJsEngine engine = new();

        var first = await engine.RunAsync(
            new(new(ScriptLanguage.JavaScript, "yaapic.variables.set('t', 'one'); yaapic.environment.set('e2', 'two');"), ScriptPhase.PreRequest, Origin, host, ScriptLimits.Default),
            TestContext.Current.CancellationToken);
        var second = await engine.RunAsync(
            new(new(ScriptLanguage.JavaScript, "console.log(yaapic.variables.get('t'), yaapic.environment.get('e2'));"), ScriptPhase.PreRequest, Origin, host, ScriptLimits.Default),
            TestContext.Current.CancellationToken);

        Assert.True(first.Succeeded, first.Error?.Message);
        Assert.Equal("one two", Assert.Single(second.Console).Message);
    }

    internal sealed class OverlayHost(ScriptVariableOverlay overlay, ScriptInfo info) : IScriptHost
    {
        public ScriptInfo Info => info;

        public ScriptRequest Request { get; } = new();

        public ScriptResponse? Response => null;

        public IReadOnlyList<ScriptCookie> ResponseCookies => [];

        public bool TryGetVariable(ScriptScope scope, string name, out string value) => overlay.TryGet(scope, name, out value);

        public void SetVariable(ScriptScope scope, string name, string value) => overlay.Set(scope, name, value);

        public bool UnsetVariable(ScriptScope scope, string name)
        {
            var existed = overlay.TryGet(scope, name, out _);
            overlay.Unset(scope, name);
            return existed;
        }

        public void ClearVariables(ScriptScope scope) => overlay.Clear(scope);

        public IReadOnlyList<KeyValueEntry> ListVariables(ScriptScope scope) =>
            [.. overlay.List(scope).Select(p => new KeyValueEntry(p.Key, p.Value))];

        public string ReplaceIn(string template) => overlay.ReplaceIn(template);

        public ScriptSendResult Send(ScriptRequest request) => new(null, "not supported");
    }
}
