using System.Diagnostics;
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

    Console.WriteLine("OK");
    return 0;
}
catch (Exception ex)
{
    return Fail(ex.ToString());
}
