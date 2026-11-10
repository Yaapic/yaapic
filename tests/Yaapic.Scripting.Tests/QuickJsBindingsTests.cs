using System.Diagnostics;
using Yaapic.Scripting.QuickJs;

namespace Yaapic.Scripting.Tests;

/// <summary>Runs the QuickJS-NG bindings against the real native library.</summary>
public sealed class QuickJsBindingsTests
{
    [Theory]
    [InlineData("1 + 1", "2")]
    [InlineData("'héllo ✓ \U0001F600'", "héllo ✓ \U0001F600")]
    [InlineData("({ a: [1, 2], b: 'xé' })", "{\"a\":[1,2],\"b\":\"xé\"}")]
    [InlineData("JSON.stringify({ k: 1 })", "{\"k\":1}")]
    [InlineData("true", "true")]
    public void Eval_ReturnsTheValueAsText(string source, string expected)
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();

        var result = context.Eval(source);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
        Assert.Equal(0, context.LiveValues);
    }

    [Fact]
    public void Eval_Undefined_HasNoValue()
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();

        var result = context.Eval("undefined");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Eval_ThrownError_CarriesMessageAndLine()
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();

        var result = context.Eval("\n\nthrow new Error('boom é');", "script.js");

        Assert.Equal(JsErrorKind.Exception, result.Error!.Kind);
        Assert.Equal("boom é", result.Error.Message);
        Assert.Equal(3, result.Error.Line);
        Assert.Equal(0, context.LiveValues);
    }

    [Fact]
    public void Eval_SyntaxError_CarriesMessageAndLine()
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();

        var result = context.Eval("var a = 1;\nvar b = ;", "script.js");

        Assert.Equal(JsErrorKind.Exception, result.Error!.Kind);
        Assert.False(string.IsNullOrEmpty(result.Error.Message));
        Assert.Equal(2, result.Error.Line);
        Assert.Equal(0, context.LiveValues);
    }

    [Fact]
    public void Eval_ThrownNonError_UsesItsText()
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();

        var result = context.Eval("throw 'plain'");

        Assert.Equal("plain", result.Error!.Message);
        Assert.Equal(0, context.LiveValues);
    }

    [Fact]
    public void HostFunction_ReceivesAndReturnsStrings()
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();
        context.DefineHostFunction("host", (a, b) => $"{a}|{b}|é\U0001F600");

        Assert.Equal("xé|y|é\U0001F600", context.Eval("host('xé', 'y')").Value);
        Assert.Equal("|||é\U0001F600".Replace("|||", "||"), context.Eval("host()").Value);
        Assert.Equal(0, context.LiveValues);
    }

    [Fact]
    public void HostFunction_HandlesOneMegabytePayloads()
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();
        var payload = new string('a', 1 << 20);
        context.DefineHostFunction("echo", (a, _) => a + a.Length);

        var result = context.Eval("echo('a'.repeat(1 << 20)).length");

        Assert.Equal((payload.Length + (1 << 20).ToString().Length).ToString(), result.Value);
        Assert.Equal(0, context.LiveValues);
    }

    [Fact]
    public void HostFunction_ThrowBecomesAJavaScriptError()
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();
        context.DefineHostFunction("fail", (_, _) => throw new InvalidOperationException("nope"));

        Assert.Equal("caught nope", context.Eval("try { fail(); } catch (e) { 'caught ' + e.message }").Value);
        Assert.Equal("nope", context.Eval("fail()").Error!.Message);
        Assert.Equal(0, context.LiveValues);
    }

    [Fact]
    public void Deadline_InterruptsAnInfiniteLoop()
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();
        runtime.Interrupt.SetTimeout(TimeSpan.FromMilliseconds(200));
        var watch = Stopwatch.StartNew();

        var result = context.Eval("while (true) {}");

        Assert.Equal(JsErrorKind.Interrupted, result.Error!.Kind);
        Assert.InRange(watch.Elapsed, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10));
        runtime.Interrupt.Reset();
        Assert.Equal("2", context.Eval("1 + 1").Value);
        Assert.Equal(0, context.LiveValues);
    }

    [Fact]
    public async Task Cancellation_InterruptsAnInfiniteLoop()
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();
        var canceller = Task.Run(async () =>
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            runtime.Interrupt.Cancel();
        }, TestContext.Current.CancellationToken);

        var result = context.Eval("while (true) {}");
        await canceller;

        Assert.Equal(JsErrorKind.Interrupted, result.Error!.Kind);
        Assert.Equal(0, context.LiveValues);
    }

    [Fact]
    public void MemoryLimit_StopsARunawayAllocation()
    {
        using var runtime = new QuickJsRuntime(memoryLimit: 4 * 1024 * 1024);
        using var context = runtime.CreateContext();

        var result = context.Eval("var all = []; while (true) { all.push(new Uint8Array(1 << 20)); }");

        Assert.Equal(JsErrorKind.OutOfMemory, result.Error!.Kind);
        Assert.Equal("2", context.Eval("all = null; 1 + 1").Value);
        Assert.Equal(0, context.LiveValues);
    }

    [Fact]
    public void Promise_ThenRunsThroughPendingJobs()
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();
        context.Eval("globalThis.r = 0; Promise.resolve(41).then(v => { globalThis.r = v + 1; });");

        Assert.Equal("0", context.Eval("r").Value);
        Assert.Null(runtime.RunPendingJobs());
        Assert.Equal("42", context.Eval("r").Value);
        Assert.Equal(0, context.LiveValues);
    }

    [Fact]
    public void PendingJobs_TreatARejectionAsDataNotAFailure()
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();
        context.Eval("Promise.resolve().then(() => { throw new Error('late'); });");

        Assert.Null(runtime.RunPendingJobs());
        Assert.Equal(0, context.LiveValues);
    }

    [Fact]
    public void Dispose_CanRepeatWithoutCrashing()
    {
        for (var i = 0; i < 25; i++)
        {
            var runtime = new QuickJsRuntime();
            var context = runtime.CreateContext();
            context.DefineHostFunction("h", (a, _) => a);
            Assert.Equal("ok", context.Eval("h('ok')").Value);
            context.Dispose();
            context.Dispose();
            runtime.Dispose();
            runtime.Dispose();
        }

        using var left = new QuickJsRuntime();
        left.CreateContext(); // freed with the runtime
    }

    [Fact]
    public void Eval_AfterDispose_Throws()
    {
        using var runtime = new QuickJsRuntime();
        var context = runtime.CreateContext();
        context.Dispose();

        Assert.Throws<ObjectDisposedException>(() => context.Eval("1"));
    }

    [Fact]
    public void Context_HasNoStdOsOrRequire()
    {
        using var runtime = new QuickJsRuntime();
        using var context = runtime.CreateContext();

        Assert.Equal("undefined,undefined,undefined", context.Eval("[typeof std, typeof os, typeof require].join()").Value);
        Assert.Equal("function,function,function,function,function,object", context.Eval("[typeof Map, typeof Proxy, typeof RegExp, typeof Uint8Array, typeof Promise, typeof JSON].join()").Value);
    }
}
