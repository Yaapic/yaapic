using System.Diagnostics;
using Yaapic.Scripting.Lua;

namespace Yaapic.Scripting.Tests;

/// <summary>Runs the Lua bindings and the C shim against the real native library.</summary>
public sealed class LuaBindingsTests
{
    private static LuaResult Run(string source, Func<string, string, string>? host = null, long memoryLimit = 0)
    {
        using var state = new LuaState(memoryLimit);
        if (host is not null)
        {
            state.DefineHost(host);
        }

        return state.Run(source, "test");
    }

    [Fact]
    public void Run_ValidChunk_Succeeds() => Assert.True(Run("local x = 1 + 1").IsSuccess);

    [Fact]
    public void Run_SyntaxError_CarriesMessageAndLine()
    {
        var error = Run("local a = 1\nlocal = = 2").Error;

        Assert.NotNull(error);
        Assert.Equal(LuaErrorKind.Syntax, error.Kind);
        Assert.Equal(2, error.Line);
        Assert.Contains("test:2:", error.Message);
    }

    [Fact]
    public void Run_RuntimeError_CarriesMessageLineAndTraceback()
    {
        var error = Run("local a = 1\n\nerror('boom')").Error;

        Assert.NotNull(error);
        Assert.Equal(LuaErrorKind.Runtime, error.Kind);
        Assert.Equal(3, error.Line);
        Assert.Equal("test:3: boom", error.Message);
        Assert.StartsWith("stack traceback:", error.Traceback);
    }

    [Fact]
    public void Run_ErrorWithTable_IsReportedWithoutCrashing()
    {
        var error = Run("error({ code = 7 })").Error;

        Assert.NotNull(error);
        Assert.Equal(LuaErrorKind.Runtime, error.Kind);
        Assert.Contains("table", error.Message);
    }

    [Fact]
    public void Host_RoundTrip_KeepsUnicode()
    {
        var seen = new List<string>();
        var result = Run(
            "local r = __host('echo', 'héllo ✓ é \U0001F600'); __host('out', r)",
            (op, args) =>
            {
                if (op == "out")
                {
                    seen.Add(args);
                }

                return op == "echo" ? args + "!" : "";
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(["héllo ✓ é \U0001F600!"], seen);
    }

    [Fact]
    public void Host_RoundTrip_HandlesAOneMegabyteString()
    {
        var big = new string('x', 1024 * 1024);
        var length = 0;
        var result = Run(
            "__host('out', __host('big', ''))",
            (op, args) =>
            {
                if (op == "out")
                {
                    length = args.Length;
                    return "";
                }

                return big;
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(big.Length, length);
    }

    [Fact]
    public void Host_ThrowingCallback_RaisesACatchableLuaError()
    {
        var seen = new List<string>();
        var result = Run(
            "local ok, err = pcall(__host, 'x', 'y'); __host('out', tostring(ok) .. '|' .. err)",
            (op, args) => op == "out" ? Record(seen, args) : throw new InvalidOperationException("boom"));

        Assert.True(result.IsSuccess);
        Assert.Contains("boom", Assert.Single(seen));
        Assert.StartsWith("false|", seen[0]);
    }

    [Fact]
    public void Host_ThrowingCallback_Uncaught_IsARuntimeError()
    {
        var error = Run("__host('x', 'y')", (_, _) => throw new InvalidOperationException("boom")).Error;

        Assert.NotNull(error);
        Assert.Equal(LuaErrorKind.Runtime, error.Kind);
        Assert.Contains("boom", error.Message);
    }

    [Fact]
    public void Run_InfiniteLoop_TimesOut()
    {
        using var state = new LuaState();
        state.SetTimeout(TimeSpan.FromMilliseconds(200));
        var watch = Stopwatch.StartNew();

        var error = state.Run("while true do end", "loop").Error;

        Assert.Equal(LuaErrorKind.Timeout, error?.Kind);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Run_TimeoutCannotBeSwallowedWithPcall()
    {
        using var state = new LuaState();
        state.SetTimeout(TimeSpan.FromMilliseconds(100));

        var error = state.Run("while true do pcall(function() while true do end end) end", "loop").Error;

        Assert.Equal(LuaErrorKind.Timeout, error?.Kind);
    }

    [Fact]
    public void Run_CancelFlag_Cancels()
    {
        using var state = new LuaState();
        state.Cancel();

        Assert.Equal(LuaErrorKind.Canceled, state.Run("while true do end", "loop").Error?.Kind);
    }

    [Fact]
    public void Run_AfterTimeout_StateIsReusableOnceLimitsAreReset()
    {
        using var state = new LuaState();
        state.SetTimeout(TimeSpan.FromMilliseconds(50));
        Assert.Equal(LuaErrorKind.Timeout, state.Run("while true do end", "a").Error?.Kind);

        state.ResetLimits();

        Assert.True(state.Run("local x = 1", "b").IsSuccess);
    }

    [Fact]
    public void Run_MemoryLimit_ReportsOutOfMemory()
    {
        var error = Run("local s = string.rep('x', 1e8)", memoryLimit: 4 * 1024 * 1024).Error;

        Assert.Equal(LuaErrorKind.OutOfMemory, error?.Kind);
    }

    [Fact]
    public void Run_MemoryLimit_CaughtByPcall_StillReported()
    {
        var error = Run("pcall(string.rep, 'x', 1e8); error('later')", memoryLimit: 4 * 1024 * 1024).Error;

        Assert.Equal(LuaErrorKind.OutOfMemory, error?.Kind);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("os")]
    [InlineData("package")]
    [InlineData("debug")]
    [InlineData("dofile")]
    [InlineData("loadfile")]
    [InlineData("loadstring")]
    [InlineData("require")]
    [InlineData("load")]
    [InlineData("print")]
    [InlineData("warn")]
    [InlineData("string.dump")]
    public void RestrictedGlobals_AreNil(string name)
    {
        var seen = new List<string>();

        Run($"__host('out', tostring({name}))", (op, args) => Record(seen, args));

        Assert.Equal(["nil"], seen);
    }

    [Fact]
    public void AllowedLibraries_Exist()
    {
        var seen = new List<string>();

        Run(
            "__host('out', type(string.rep) .. type(table.concat) .. type(math.random) .. type(utf8.char) .. type(coroutine.create) .. type(pcall))",
            (op, args) => Record(seen, args));

        Assert.Equal(["functionfunctionfunctionfunctionfunctionfunction"], seen);
    }

    [Fact]
    public void CollectGarbage_OnlyCount()
    {
        var seen = new List<string>();

        Run(
            "__host('out', type(collectgarbage('count'))); __host('out', tostring(pcall(collectgarbage, 'collect')))",
            (op, args) => Record(seen, args));

        Assert.Equal(["number", "false"], seen);
    }

    [Fact]
    public void Run_BinaryChunk_IsRefused()
    {
        using var state = new LuaState();

        var error = state.Run("\u001bLua", "bin").Error;

        Assert.Equal(LuaErrorKind.Syntax, error?.Kind);
    }

    [Fact]
    public void ThousandSequentialStates_DoNotGrowMemory()
    {
        // A leak grows with every batch, whereas the allocator keeping freed memory in reserve (glibc arenas) settles
        // after the first batch: so the growth of the second batch is what is bounded, not the first.
        RunStates(50);
        RunStates(1000);
        GC.Collect();
        var before = Process.GetCurrentProcess().PrivateMemorySize64;

        RunStates(1000);
        GC.Collect();
        var growth = Process.GetCurrentProcess().PrivateMemorySize64 - before;

        Assert.True(growth < 20 * 1024 * 1024, $"Private memory grew by {growth} bytes.");

        static void RunStates(int count)
        {
            for (var i = 0; i < count; i++)
            {
                using var state = new LuaState(8 * 1024 * 1024);
                state.DefineHost((_, args) => args);
                Assert.True(state.Run("local t = {} for i = 1, 1000 do t[i] = tostring(i) end __host('x', t[10])", "s").IsSuccess);
            }
        }
    }

    [Fact]
    public async Task TwoStatesOnTwoThreads_RunConcurrently()
    {
        static LuaErrorKind? Work(int id)
        {
            using var state = new LuaState();
            var total = 0;
            state.DefineHost((_, args) =>
            {
                total += int.Parse(args);
                return args;
            });
            var error = state.Run($"for i = 1, 200 do __host('add', '{id}') end", "t").Error;
            return error?.Kind ?? (total == 200 * id ? null : LuaErrorKind.Runtime);
        }

        var results = await Task.WhenAll(Task.Run(() => Work(1)), Task.Run(() => Work(2)));

        Assert.All(results, Assert.Null);
    }

    private static string Record(List<string> seen, string value)
    {
        seen.Add(value);
        return "";
    }
}
