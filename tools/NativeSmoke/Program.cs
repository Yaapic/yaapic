using System.Diagnostics;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
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
