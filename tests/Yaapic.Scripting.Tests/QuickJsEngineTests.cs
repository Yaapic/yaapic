using System.Diagnostics;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.QuickJs;

namespace Yaapic.Scripting.Tests;

/// <summary>Runs <see cref="QuickJsEngine"/> against the real native library and a fake host.</summary>
public sealed class QuickJsEngineTests
{
    private const string Origin = "pre-request · Users API";

    private static readonly ScriptLimits Fast = ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(10) };

    private static Task<ScriptRunResult> Run(string source, ScriptLimits? limits = null, FakeHost? host = null, CancellationToken? ct = null) =>
        new QuickJsEngine().RunAsync(
            new(new(ScriptLanguage.JavaScript, source), ScriptPhase.PreRequest, Origin, host ?? new FakeHost(), limits ?? Fast),
            ct ?? TestContext.Current.CancellationToken);

    [Fact]
    public async Task Console_entries_keep_their_level_and_origin()
    {
        var result = await Run("console.log('a'); console.info('b'); console.warn('c'); console.error('d'); console.debug('e');");

        Assert.Equal(ScriptRunStatus.Succeeded, result.Status);
        Assert.Equal(
            [ConsoleLevel.Log, ConsoleLevel.Info, ConsoleLevel.Warn, ConsoleLevel.Error, ConsoleLevel.Debug],
            result.Console.Select(e => e.Level));
        Assert.Equal(["a", "b", "c", "d", "e"], result.Console.Select(e => e.Message));
        Assert.All(result.Console, e => Assert.Equal(Origin, e.Origin));
        Assert.All(result.Console, e => Assert.Equal(ConsoleSource.Script, e.Source));
    }

    [Theory]
    [InlineData("console.log('x', 1, true, null, undefined)", "x 1 true null undefined")]
    [InlineData("console.log({ a: 1, b: [1, 2] })", "{\"a\":1,\"b\":[1,2]}")]
    [InlineData("console.log([1, 'a'])", "[1,\"a\"]")]
    [InlineData("const o = { n: 1 }; o.self = o; console.log(o)", "{\"n\":1,\"self\":\"[Circular]\"}")]
    public async Task Console_formats_its_arguments(string source, string expected)
    {
        var result = await Run(source);

        Assert.Equal(ScriptRunStatus.Succeeded, result.Status);
        Assert.Equal(expected, Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task Console_formats_an_error_as_message_plus_first_stack_line()
    {
        var result = await Run("console.log(new Error('boom'))");

        var message = Assert.Single(result.Console).Message;
        Assert.StartsWith("boom ", message, StringComparison.Ordinal);
        Assert.Contains(Origin, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Console_is_capped_at_1000_entries_with_a_truncation_warning()
    {
        var result = await Run("for (let i = 0; i < 10000; i++) console.log('line ' + i);");

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
        var result = await Run("console.log('x'.repeat(20000))");

        var message = Assert.Single(result.Console).Message;
        Assert.Equal(ScriptLimits.Default.MaxConsoleEntryLength, message.Length);
        Assert.EndsWith("…", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_thrown_error_reports_message_and_exact_user_position()
    {
        var result = await Run("let a = 1;\nlet b = 2;\n  throw new Error('bad');");

        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.NotNull(result.Error);
        Assert.Equal("bad", result.Error.Message);
        Assert.Equal(3, result.Error.Line);
        Assert.Equal(13, result.Error.Column); // QuickJS points at the Error constructor call
        Assert.Contains(Origin + ":3", result.Error.Stack, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_syntax_error_fails_the_run()
    {
        var result = await Run("let = ;");

        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.NotNull(result.Error);
        Assert.NotEmpty(result.Error.Message);
    }

    [Fact]
    public async Task An_infinite_loop_times_out()
    {
        var watch = Stopwatch.StartNew();

        var result = await Run("while (true) {}", Fast with { Timeout = TimeSpan.FromMilliseconds(300) });

        Assert.Equal(ScriptRunStatus.TimedOut, result.Status);
        Assert.True(watch.ElapsedMilliseconds < 800, $"took {watch.ElapsedMilliseconds} ms");
        Assert.Equal("Script timed out after 0.3 s", result.Error?.Message);
    }

    [Fact]
    public async Task A_catching_script_cannot_swallow_the_timeout()
    {
        var result = await Run("try { while (true) {} } catch (e) {} while (true) {}", Fast with { Timeout = TimeSpan.FromMilliseconds(200) });

        Assert.Equal(ScriptRunStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task Cancellation_stops_the_script_promptly()
    {
        using CancellationTokenSource cts = new();
        var task = Run("while (true) {}", ct: cts.Token);
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
            "const all = []; while (true) { all.push(new Uint8Array(1 << 20)); }",
            Fast with { MemoryLimitBytes = 8L * 1024 * 1024 });

        Assert.Equal(ScriptRunStatus.MemoryLimit, result.Status);
    }

    [Fact]
    public async Task Await_of_a_resolved_promise_completes()
    {
        var result = await Run("const v = await Promise.resolve(42); console.log('got ' + v); await null; console.log('end');");

        Assert.Equal(ScriptRunStatus.Succeeded, result.Status);
        Assert.Equal(["got 42", "end"], result.Console.Select(e => e.Message));
    }

    [Fact]
    public async Task Promise_callbacks_run_before_the_run_ends()
    {
        var result = await Run("Promise.resolve(1).then(v => console.log('then ' + v));");

        Assert.Equal("then 1", Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task A_rejected_top_level_await_fails_with_the_user_position()
    {
        var result = await Run("\nawait Promise.reject(new Error('nope'));");

        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.Equal("nope", result.Error?.Message);
        Assert.Equal(2, result.Error?.Line);
    }

    [Fact]
    public async Task A_never_resolved_promise_times_out()
    {
        var result = await Run("await new Promise(() => {});");

        Assert.Equal(ScriptRunStatus.TimedOut, result.Status);
    }

    [Theory]
    [InlineData("process")]
    [InlineData("require")]
    [InlineData("setTimeout")]
    [InlineData("fetch")]
    [InlineData("module")]
    [InlineData("std")]
    [InlineData("os")]
    public async Task Host_globals_are_not_defined(string name)
    {
        var result = await Run($"console.log(typeof {name});");

        Assert.Equal("undefined", Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task The_raw_bridge_function_is_not_reachable_and_yaapic_is_frozen()
    {
        var result = await Run("console.log(typeof __hostRaw, Object.isFrozen(__yaapic), __yaapic.phase, __yaapic.origin);");

        Assert.Equal("undefined true preRequest " + Origin, Assert.Single(result.Console).Message);
    }

    [Fact]
    public async Task Two_sequential_runs_share_nothing()
    {
        var first = await Run("globalThis.leak = 1; console.log('set');");
        var second = await Run("console.log(typeof leak);");

        Assert.Equal(ScriptRunStatus.Succeeded, first.Status);
        Assert.Equal("undefined", Assert.Single(second.Console).Message);
    }

    [Fact]
    public async Task Host_calls_reach_the_host()
    {
        FakeHost host = new();

        var result = await Run(
            "__host('var.set', { scope: 'environment', name: 'k', value: 'v' });"
            + "console.log(__host('var.get', { scope: 'environment', name: 'k' }));"
            + "console.log(__host('var.get', { scope: 'environment', name: 'missing' }));",
            host: host);

        Assert.Equal(ScriptRunStatus.Succeeded, result.Status);
        Assert.Equal("v", host.Vars[(ScriptScope.Environment, "k")]);
        Assert.Equal(["v", "null"], result.Console.Select(e => e.Message));
    }

    [Fact]
    public async Task An_err_result_throws_inside_the_script()
    {
        var result = await Run(
            "try { __host('nope', {}); } catch (e) { console.log('caught: ' + e.message); }\n__host('var.get', { scope: 'bad', name: 'x' });");

        Assert.Equal("caught: Unknown host op 'nope'", result.Console[0].Message);
        Assert.Equal(ScriptRunStatus.Failed, result.Status);
        Assert.Equal("Unknown scope 'bad'", result.Error?.Message);
        Assert.Equal(2, result.Error?.Line);
    }

    [Fact]
    public async Task Two_engines_running_concurrently_do_not_interfere()
    {
        var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(
            () => Run($"globalThis.me = {i}; let n = 0; for (let k = 0; k < 200000; k++) n += k % 3; console.log('me ' + me);"),
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
        FakeHost host = new();

        var result = await Run("__host('var.set', { scope: 'local', name: 't', value: 'x' });", host: host);

        Assert.Equal("yaapic-js", host.ThreadName);
        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task An_unavailable_library_is_reported_with_its_reason()
    {
        QuickJsEngine engine = new(TimeProvider.System, () => "quickjs-ng library not found in X");

        Assert.False(engine.IsAvailable(out var reason));
        Assert.Equal("quickjs-ng library not found in X", reason);
        var result = await engine.RunAsync(
            new(new(ScriptLanguage.JavaScript, "1"), ScriptPhase.PreRequest, Origin, new FakeHost(), Fast),
            TestContext.Current.CancellationToken);
        Assert.Equal(ScriptRunStatus.Unavailable, result.Status);
        Assert.Equal("quickjs-ng library not found in X", result.Error?.Message);
    }

    [Fact]
    public void The_real_library_is_available()
    {
        QuickJsEngine engine = new();

        Assert.True(engine.IsAvailable(out var reason), reason);
        Assert.Null(reason);
        Assert.Equal(ScriptLanguage.JavaScript, engine.Language);
    }

    private sealed class FakeHost : IScriptHost
    {
        public Dictionary<(ScriptScope, string), string> Vars { get; } = [];

        public string? ThreadName { get; private set; }

        public ScriptInfo Info { get; } = new("prerequest", "Users", Guid.Empty, Origin);

        public ScriptRequest Request { get; } = new();

        public ScriptResponse? Response => null;

        public IReadOnlyList<ScriptCookie> ResponseCookies => [];

        public bool TryGetVariable(ScriptScope scope, string name, out string value) => Vars.TryGetValue((scope, name), out value!);

        public void SetVariable(ScriptScope scope, string name, string value)
        {
            ThreadName = Thread.CurrentThread.Name;
            Vars[(scope, name)] = value;
        }

        public bool UnsetVariable(ScriptScope scope, string name) => Vars.Remove((scope, name));

        public void ClearVariables(ScriptScope scope) => Vars.Clear();

        public IReadOnlyList<KeyValueEntry> ListVariables(ScriptScope scope) => [];

        public string ReplaceIn(string template) => template;

        public ScriptSendResult Send(ScriptRequest request) => new(null, "not supported");
    }
}
