using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;

namespace Yaapic.Scripting;

/// <summary>Accumulates the console entries and test results of one script run, within the limits of ADR 0009.</summary>
/// <param name="limits">The console limits.</param>
/// <param name="time">The clock stamping the entries.</param>
/// <param name="origin">The origin text copied onto every entry and test.</param>
internal sealed class RunCollector(ScriptLimits limits, TimeProvider time, string origin)
{
    private readonly List<ConsoleEntry> _console = [];
    private readonly List<TestResult> _tests = [];
    private bool _truncated;

    /// <summary>Gets a snapshot of the console entries.</summary>
    public IReadOnlyList<ConsoleEntry> Console => [.. _console];

    /// <summary>Gets a snapshot of the test results.</summary>
    public IReadOnlyList<TestResult> Tests => [.. _tests];

    /// <summary>Adds a script console line, truncating it, or drops it once the entry cap is reached.</summary>
    /// <param name="level">The severity.</param>
    /// <param name="text">The message.</param>
    public void AddConsole(ConsoleLevel level, string text)
    {
        if (_truncated)
        {
            return;
        }

        if (_console.Count >= limits.MaxConsoleEntries)
        {
            _truncated = true;
            _console.Add(new(time.GetUtcNow(), ConsoleLevel.Warn, ConsoleSource.System, "Console output truncated", origin));
            return;
        }

        var max = limits.MaxConsoleEntryLength;
        var message = text.Length > max ? string.Concat(text.AsSpan(0, Math.Max(0, max - 1)), "…") : text;
        _console.Add(new(time.GetUtcNow(), level, ConsoleSource.Script, message, origin));
    }

    /// <summary>Records a test result.</summary>
    /// <param name="name">The test name.</param>
    /// <param name="passed">Whether it passed.</param>
    /// <param name="message">The failure message, or null.</param>
    /// <param name="ms">The duration in milliseconds.</param>
    public void AddTest(string name, bool passed, string? message, double ms) =>
        _tests.Add(new(name, passed, message, TimeSpan.FromMilliseconds(ms), origin));
}
