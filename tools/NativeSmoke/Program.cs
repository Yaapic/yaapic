using System.Diagnostics;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.Lua;
using Yaapic.Scripting.QuickJs;

// Proves under Native AOT that the QuickJS-NG bindings load and run; prints OK and exits 0, or names the failed check.
static int Fail(string check)
{
    Console.Error.WriteLine($"FAILED: {check}");
    return 1;
}

try
{
    using (var runtime = new QuickJsRuntime())
    using (var context = runtime.CreateContext())
    {
        if (context.Eval("1+1").Value != "2")
        {
            return Fail("1+1");
        }

        context.DefineHostFunction("host", (a, b) => a + b);
        if (context.Eval("(function (x) { return host(x, '!'); })('hi')").Value != "hi!")
        {
            return Fail("host callback");
        }

        runtime.Interrupt.SetTimeout(TimeSpan.FromMilliseconds(200));
        var watch = Stopwatch.StartNew();
        if (context.Eval("while (true) {}").Error?.Kind != JsErrorKind.Interrupted || watch.Elapsed > TimeSpan.FromSeconds(10))
        {
            return Fail("interrupt");
        }

        if (context.LiveValues != 0)
        {
            return Fail("leaked values");
        }
    }

    using (var runtime = new QuickJsRuntime(memoryLimit: 4 * 1024 * 1024))
    using (var context = runtime.CreateContext())
    {
        if (context.Eval("var all = []; while (true) { all.push(new Uint8Array(1 << 20)); }").Error?.Kind != JsErrorKind.OutOfMemory)
        {
            return Fail("memory limit");
        }
    }

    var host = new SmokeHost();
    var run = await new QuickJsEngine().RunAsync(
        new(
            new(ScriptLanguage.JavaScript, "console.log({ n: 1 }); __host('var.set', { scope: 'local', name: 'k', value: 'v' }); await Promise.resolve();"),
            ScriptPhase.PreRequest,
            "smoke",
            host,
            ScriptLimits.Default),
        CancellationToken.None);
    if (run.Status != ScriptRunStatus.Succeeded || run.Console.Count != 1 || run.Console[0].Message != "{\"n\":1}" || host.Value != "v")
    {
        return Fail($"QuickJsEngine run: {run.Status} {run.Error?.Message}");
    }

    var loop = await new QuickJsEngine().RunAsync(
        new(new(ScriptLanguage.JavaScript, "while (true) {}"), ScriptPhase.PreRequest, "smoke", host, ScriptLimits.Default with { Timeout = TimeSpan.FromMilliseconds(300) }),
        CancellationToken.None);
    if (loop.Status != ScriptRunStatus.TimedOut)
    {
        return Fail($"QuickJsEngine timeout: {loop.Status}");
    }

    var pm = await new QuickJsEngine().RunAsync(
        new(
            new(ScriptLanguage.JavaScript, "pm.environment.set('p', 7); console.log(pm.info.eventName, pm.environment.get('p')); try { pm.vault.get; } catch (e) { console.log(e.message); }"),
            ScriptPhase.PreRequest,
            "smoke",
            host,
            ScriptLimits.Default),
        CancellationToken.None);
    if (pm.Status != ScriptRunStatus.Succeeded || host.Value != "7" || pm.Console.Count != 2 || pm.Console[0].Message != "prerequest 7"
        || pm.Console[1].Message != "pm.vault.get is not supported in Yaapic")
    {
        return Fail($"pm prelude run: {pm.Status} {pm.Error?.Message}");
    }

    var req = await new QuickJsEngine().RunAsync(
        new(
            new(ScriptLanguage.JavaScript, "pm.request.method = 'post'; pm.request.url.query.add({ key: 'a', value: 'b c' }); pm.request.headers.add({ key: 'X', value: '1' }); try { pm.response; } catch (e) { console.log(e.message); }"),
            ScriptPhase.PreRequest,
            "smoke",
            host,
            ScriptLimits.Default),
        CancellationToken.None);
    if (req.Status != ScriptRunStatus.Succeeded || host.Request.Method != "POST" || host.Request.Url != "?a=b%20c" || host.Request.Headers.Count != 1
        || req.Console.Count != 1 || req.Console[0].Message != "pm.response is only available in post-response scripts")
    {
        return Fail($"pm request run: {req.Status} {req.Error?.Message} {host.Request.Url}");
    }

    var tests = await new QuickJsEngine().RunAsync(
        new(
            new(ScriptLanguage.JavaScript, "pm.test('ok', () => { pm.expect([1, 2]).to.eql([1, 2]); }); pm.test('bad', () => { pm.expect(409).to.equal(204); }); pm.test('async', async () => { await null; });"),
            ScriptPhase.PostResponse,
            "smoke",
            host,
            ScriptLimits.Default),
        CancellationToken.None);
    if (tests.Status != ScriptRunStatus.Succeeded || tests.Tests.Count != 3 || !tests.Tests[0].Passed || tests.Tests[1].Passed
        || tests.Tests[1].Message != "AssertionError: expected 409 to equal 204" || !tests.Tests[2].Passed)
    {
        return Fail($"pm test run: {tests.Status} {tests.Error?.Message}");
    }

    var more = await new QuickJsEngine().RunAsync(
        new(
            new(ScriptLanguage.JavaScript, "pm.test('chain', () => { pm.expect({ a: { b: [{ c: 3 }] } }).to.have.nested.property('a.b[0].c').that.equals(3); pm.expect([1, 2]).to.include.members([2]); pm.expect(() => { throw new TypeError('x'); }).to.throw(TypeError, 'x'); }); pm.test('bad', () => { pm.expect({ a: 1 }).to.have.keys('b'); });"),
            ScriptPhase.PostResponse,
            "smoke",
            host,
            ScriptLimits.Default),
        CancellationToken.None);
    if (more.Status != ScriptRunStatus.Succeeded || more.Tests.Count != 2 || !more.Tests[0].Passed || more.Tests[1].Passed
        || more.Tests[1].Message != "AssertionError: expected { a: 1 } to have key 'b'")
    {
        return Fail($"pm expect collections run: {more.Status} {more.Error?.Message} {more.Tests[0].Message}");
    }

    var send = await new QuickJsEngine().RunAsync(
        new(
            new(ScriptLanguage.JavaScript, "pm.sendRequest('http://x', (err) => { console.log(err.message, pm.cookies.count()); throw new Error('cb'); });"),
            ScriptPhase.PostResponse,
            "smoke",
            host,
            ScriptLimits.Default),
        CancellationToken.None);
    if (send.Status != ScriptRunStatus.Failed || send.Error?.Message != "cb" || send.Console.Count != 1 || send.Console[0].Message != "not supported 0")
    {
        return Fail($"pm sendRequest run: {send.Status} {send.Error?.Message}");
    }

    using (var lua = new LuaState())
    {
        var seen = "";
        lua.DefineHost((op, args) =>
        {
            seen = op + args;
            return args + "!";
        });
        if (!lua.Run("local r = __host('a', 'hi'); __host('b', r)", "smoke").IsSuccess || seen != "bhi!")
        {
            return Fail($"Lua host callback: {seen}");
        }

        if (lua.Run("1+1", "smoke").Error?.Kind != LuaErrorKind.Syntax || lua.Run("error('x')", "smoke").Error?.Line != 1)
        {
            return Fail("Lua errors");
        }

        lua.DefineHost((_, _) => throw new InvalidOperationException("boom"));
        if (lua.Run("local ok = pcall(__host, 'a', 'b'); assert(not ok)", "smoke").Error is not null)
        {
            return Fail("Lua throwing host callback");
        }

        lua.SetTimeout(TimeSpan.FromMilliseconds(200));
        if (lua.Run("while true do end", "smoke").Error?.Kind != LuaErrorKind.Timeout)
        {
            return Fail("Lua timeout");
        }
    }

    using (var lua = new LuaState(4 * 1024 * 1024))
    {
        if (lua.Run("local s = string.rep('x', 1e8)", "smoke").Error?.Kind != LuaErrorKind.OutOfMemory)
        {
            return Fail("Lua memory limit");
        }
    }

    var luaRun = await new LuaEngine().RunAsync(
        new(
            new(ScriptLanguage.Lua, "console.log({ n = 1 }, 'x') __host('var.set', '{\"scope\":\"local\",\"name\":\"k\",\"value\":\"lua\"}') print(pcall(__host, 'nope', '{}'))"),
            ScriptPhase.PreRequest,
            "smoke",
            host,
            ScriptLimits.Default),
        CancellationToken.None);
    if (luaRun.Status != ScriptRunStatus.Succeeded || host.Value != "lua" || luaRun.Console.Count != 2 || luaRun.Console[0].Message != "{ n = 1 } x"
        || luaRun.Console[1].Message != "false Unknown host op 'nope'")
    {
        return Fail($"LuaEngine run: {luaRun.Status} {luaRun.Error?.Message}");
    }

    Console.WriteLine("OK");
    return 0;
}
catch (Exception ex)
{
    return Fail(ex.ToString());
}

// A host that only records the one variable the smoke script sets.
sealed class SmokeHost : IScriptHost
{
    public string? Value { get; private set; }

    public ScriptInfo Info { get; } = new("prerequest", "smoke", Guid.Empty, "smoke");

    public ScriptRequest Request { get; } = new();

    public ScriptResponse? Response => null;

    public IReadOnlyList<ScriptCookie> ResponseCookies => [];

    public bool TryGetVariable(ScriptScope scope, string name, out string value)
    {
        value = Value ?? "";
        return Value is not null;
    }

    public void SetVariable(ScriptScope scope, string name, string value) => Value = value;

    public bool UnsetVariable(ScriptScope scope, string name) => false;

    public void ClearVariables(ScriptScope scope)
    {
    }

    public IReadOnlyList<KeyValueEntry> ListVariables(ScriptScope scope) => [];

    public string ReplaceIn(string template) => template;

    public ScriptSendResult Send(ScriptRequest request) => new(null, "not supported");
}
