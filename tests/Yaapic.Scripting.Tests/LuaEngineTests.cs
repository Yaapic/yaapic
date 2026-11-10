using System.Diagnostics;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.Lua;

namespace Yaapic.Scripting.Tests;

/// <summary>Runs <see cref="LuaEngine"/> against the real native library and a fake host.</summary>
public sealed class LuaEngineTests
{
    private const string Origin = "pre-request · Users API";

    private static readonly ScriptLimits Fast = ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(10) };

    private static Task<ScriptRunResult> Run(
        string source,
        ScriptLimits? limits = null,
        QuickJsEngineTests.FakeHost? host = null,
        CancellationToken? ct = null,
        string origin = Origin) =>
        new LuaEngine().RunAsync(
            new(new(ScriptLanguage.Lua, source), ScriptPhase.PreRequest, origin, host ?? new QuickJsEngineTests.FakeHost(), limits ?? Fast),
            ct ?? TestContext.Current.CancellationToken);

    [Fact]
    public async Task Console_entries_keep_their_level_and_origin()
    {
        var result = await Run("console.log('a') console.info('b') console.warn('c') console.error('d') console.debug('e') print('f')");

        Assert.Equal(ScriptRunStatus.Succeeded, result.Status);
        Assert.Equal(
            [ConsoleLevel.Log, ConsoleLevel.Info, ConsoleLevel.Warn, ConsoleLevel.Error, ConsoleLevel.Debug, ConsoleLevel.Log],
            result.Console.Select(e => e.Level));
        Assert.Equal(["a", "b", "c", "d", "e", "f"], result.Console.Select(e => e.Message));
        Assert.All(result.Console, e => Assert.Equal(Origin, e.Origin));
        Assert.All(result.Console, e => Assert.Equal(ConsoleSource.Script, e.Source));
    }

    [Theory]
    [InlineData("console.log('x', 1, true, nil, 2.5)", "x 1 true nil 2.5")]
    [InlineData("console.log(nil)", "nil")]
    [InlineData("console.log({})", "{}")]
    [InlineData("console.log({ a = 1, b = { 2, 3 } })", "{ a = 1, b = { 2, 3 } }")]
    [InlineData("console.log({ 1, 2, z = 1, a = 's' })", "{ 1, 2, a = \"s\", z = 1 }")]
    [InlineData("console.log({ a = { b = { c = { d = 1 } } } })", "{ a = { b = { c = ... } } }")]
    [InlineData("local t = {} t.self = t console.log(t)", "{ self = <cycle> }")]
    [InlineData("local s = { 1 } console.log({ s, s })", "{ { 1 }, { 1 } }")]
    [InlineData("console.log(setmetatable({}, { __tostring = function() return 'custom' end }))", "custom")]
    [InlineData("console.log('a\\nb', 'c')", "a\nb c")]
    public async Task Console_formats_its_arguments(string source, string expected)
    {
        var result = await Run(source);

        Assert.Equal(ScriptRunStatus.Succeeded, result.Status);
        Assert.Equal(expected, Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task Console_is_capped_at_1000_entries_with_a_truncation_warning()
    {
        var result = await Run("for i = 0, 9999 do console.log('line ' .. i) end");

        Assert.Equal(ScriptRunStatus.Succeeded, result.Status);
        Assert.Equal(1001, result.Console.Count);
        var last = result.Console[^1];
        Assert.Equal(ConsoleLevel.Warn, last.Level);
        Assert.Equal(ConsoleSource.System, last.Source);
        Assert.Equal("line 999", result.Console[999].Message);
    }

    [Fact]
    public async Task Console_message_is_truncated_at_the_entry_length()
    {
        var result = await Run("console.log(string.rep('x', 20000))");

        var message = Assert.Single(result.Console).Message;
        Assert.Equal(ScriptLimits.Default.MaxConsoleEntryLength, message.Length);
        Assert.EndsWith("…", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_thrown_error_reports_message_and_exact_user_line()
    {
        var result = await Run("local a = 1\nlocal b = 2\n  error('bad')");

        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.NotNull(result.Error);
        Assert.Equal(Origin + ":3: bad", result.Error.Message);
        Assert.Equal(3, result.Error.Line);
        Assert.Contains(Origin + ":3", result.Error.Stack, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_thrown_table_fails_the_run()
    {
        var result = await Run("error({ code = 1 })");

        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.Contains("table", result.Error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_syntax_error_reports_its_line()
    {
        var result = await Run("local a = 1\nlocal = = 2");

        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.Equal(2, result.Error?.Line);
        Assert.StartsWith(Origin + ":2:", result.Error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_infinite_loop_times_out()
    {
        var watch = Stopwatch.StartNew();

        var result = await Run("while true do end", Fast with { Timeout = TimeSpan.FromMilliseconds(300) });

        Assert.Equal(ScriptRunStatus.TimedOut, result.Status);
        Assert.True(watch.ElapsedMilliseconds < 800, $"took {watch.ElapsedMilliseconds} ms");
        Assert.Equal("Script timed out after 0.3 s", result.Error?.Message);
    }

    [Fact]
    public async Task A_catching_script_cannot_swallow_the_timeout()
    {
        var result = await Run("pcall(function() while true do end end) while true do end", Fast with { Timeout = TimeSpan.FromMilliseconds(200) });

        Assert.Equal(ScriptRunStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task Cancellation_stops_the_script_promptly()
    {
        using CancellationTokenSource cts = new();
        var task = Run("while true do end", ct: cts.Token);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        var watch = Stopwatch.StartNew();

        await cts.CancelAsync();
        var result = await task;

        Assert.Equal(ScriptRunStatus.Canceled, result.Status);
        Assert.True(watch.ElapsedMilliseconds < 500, $"took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task An_already_canceled_token_cancels_without_running()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        var result = await Run("console.log('never')", ct: cts.Token);

        Assert.Equal(ScriptRunStatus.Canceled, result.Status);
        Assert.Empty(result.Console);
    }

    [Fact]
    public async Task Runaway_allocation_hits_the_memory_limit()
    {
        var result = await Run(
            "local all = {} while true do all[#all + 1] = string.rep('x', 1024 * 1024) end",
            Fast with { MemoryLimitBytes = 8L * 1024 * 1024 });

        Assert.Equal(ScriptRunStatus.MemoryLimit, result.Status);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("os")]
    [InlineData("require")]
    [InlineData("dofile")]
    [InlineData("loadfile")]
    [InlineData("load")]
    [InlineData("package")]
    [InlineData("debug")]
    public async Task Host_globals_are_not_defined(string name)
    {
        var result = await Run($"console.log(type({name}))");

        Assert.Equal("nil", Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task The_context_global_carries_the_phase_and_the_origin()
    {
        var result = await Run("console.log(__yaapic.phase, __yaapic.origin)", origin: "a\"b\\c\nd");

        Assert.Equal("preRequest a\"b\\c\nd", Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task Two_sequential_runs_share_nothing()
    {
        var first = await Run("leak = 1 string.leak = 2 console.log('set')");
        var second = await Run("console.log(type(leak), type(string.leak))");

        Assert.Equal(ScriptRunStatus.Succeeded, first.Status);
        Assert.Equal("nil nil", Assert.Single(second.Console).Message);
    }

    [Fact]
    public async Task Host_calls_reach_the_host()
    {
        QuickJsEngineTests.FakeHost host = new();
        const string Key = "'{\"scope\":\"environment\",\"name\":\"k\"";

        var result = await Run(
            $"__host('var.set', {Key},\"value\":\"v\"}}')\n"
            + $"console.log(__host('var.get', {Key}}}'))\n"
            + "console.log(__host('var.get', '{\"scope\":\"environment\",\"name\":\"missing\"}'))",
            host: host);

        Assert.Equal(ScriptRunStatus.Succeeded, result.Status);
        Assert.Equal("v", host.Vars[(ScriptScope.Environment, "k")]);
        Assert.Equal(["{\"ok\":\"v\"}", "{\"ok\":null}"], result.Console.Select(e => e.Message));
    }

    [Fact]
    public async Task An_err_result_raises_a_lua_error_that_pcall_catches()
    {
        var result = await Run(
            "local ok, e = pcall(__host, 'nope', '{}')\nconsole.log(ok, e)\n__host('var.get', '{\"scope\":\"bad\",\"name\":\"x\"}')");

        Assert.Equal("false Unknown host op 'nope'", result.Console[0].Message);
        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.Equal(Origin + ":3: Unknown scope 'bad'", result.Error?.Message);
        Assert.Equal(3, result.Error?.Line);
    }

    [Fact]
    public async Task Engines_running_concurrently_do_not_interfere()
    {
        var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(
            () => Run($"me = {i} local n = 0 for k = 0, 200000 do n = n + k % 3 end console.log('me ' .. me)"),
            TestContext.Current.CancellationToken)).ToArray();

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < results.Length; i++)
        {
            Assert.Equal(ScriptRunStatus.Succeeded, results[i].Status);
            Assert.Equal($"me {i}", Assert.Single(results[i].Console).Message);
        }
    }

    [Fact]
    public async Task The_run_happens_on_a_dedicated_thread_and_reports_a_duration()
    {
        QuickJsEngineTests.FakeHost host = new();

        var result = await Run("__host('var.set', '{\"scope\":\"local\",\"name\":\"t\",\"value\":\"x\"}')", host: host);

        Assert.Equal("yaapic-lua", host.ThreadName);
        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task An_unavailable_library_is_reported_with_its_reason()
    {
        LuaEngine engine = new(TimeProvider.System, () => "lua54 library not found in X");

        Assert.False(engine.IsAvailable(out var reason));
        Assert.Equal("lua54 library not found in X", reason);
        var result = await engine.RunAsync(
            new(new(ScriptLanguage.Lua, "1"), ScriptPhase.PreRequest, Origin, new QuickJsEngineTests.FakeHost(), Fast),
            TestContext.Current.CancellationToken);
        Assert.Equal(ScriptRunStatus.Unavailable, result.Status);
        Assert.Equal("lua54 library not found in X", result.Error?.Message);
    }

    [Fact]
    public void The_real_library_is_available()
    {
        LuaEngine engine = new();

        Assert.True(engine.IsAvailable(out var reason), reason);
        Assert.Null(reason);
        Assert.Equal(ScriptLanguage.Lua, engine.Language);
    }
}
