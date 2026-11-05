using Yaapic.Domain.Scripting;
using TestResult = Yaapic.Domain.Scripting.TestResult;

namespace Yaapic.Domain.Tests;

public sealed class ScriptRunResultTests
{
    private static TestResult T(bool passed) => new("t", passed, passed ? null : "boom", TimeSpan.Zero, "o");

    [Fact]
    public void Skipped_Succeeds_WithNoOutput()
    {
        var r = ScriptRunResult.Skipped(ScriptPhase.PreRequest, "o", ScriptLanguage.Lua);

        Assert.True(r.Succeeded);
        Assert.Null(r.Error);
        Assert.Empty(r.Console);
        Assert.Empty(r.Tests);
        Assert.Equal(TimeSpan.Zero, r.Duration);
        Assert.Equal(ScriptLanguage.Lua, r.Language);
    }

    [Fact]
    public void Unavailable_CarriesReasonAsError()
    {
        var r = ScriptRunResult.Unavailable(ScriptPhase.PostResponse, "o", ScriptLanguage.JavaScript, "no engine");

        Assert.False(r.Succeeded);
        Assert.Equal(ScriptRunStatus.Unavailable, r.Status);
        Assert.Equal("no engine", r.Error?.Message);
        Assert.Equal(ScriptPhase.PostResponse, r.Phase);
    }

    [Theory]
    [InlineData(ScriptRunStatus.Failed)]
    [InlineData(ScriptRunStatus.TimedOut)]
    [InlineData(ScriptRunStatus.MemoryLimit)]
    [InlineData(ScriptRunStatus.Canceled)]
    public void Succeeded_IsFalse_ForOtherStatuses(ScriptRunStatus status)
    {
        var r = ScriptRunResult.Skipped(ScriptPhase.PreRequest, "o", ScriptLanguage.Lua) with { Status = status };

        Assert.False(r.Succeeded);
    }

    [Fact]
    public void TestSummary_None_IsEmpty()
    {
        Assert.Equal((0, 0), TestSummary.Count([]));
        Assert.Equal("", TestSummary.Format([]));
    }

    [Fact]
    public void TestSummary_AllPassed()
    {
        TestResult[] tests = [T(true), T(true), T(true)];

        Assert.Equal((3, 0), TestSummary.Count(tests));
        Assert.Equal("3/3", TestSummary.Format(tests));
    }

    [Fact]
    public void TestSummary_Mixed()
    {
        TestResult[] tests = [T(true), T(false), T(true), T(true), T(false), T(true)];

        Assert.Equal((4, 2), TestSummary.Count(tests));
        Assert.Equal("4/6", TestSummary.Format(tests));
    }
}
