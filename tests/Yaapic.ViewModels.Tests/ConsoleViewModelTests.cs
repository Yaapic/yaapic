using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Console;
using TestResult = Yaapic.Domain.Scripting.TestResult;

namespace Yaapic.ViewModels.Tests;

public sealed class ConsoleViewModelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 2, 10, 0, 0, TimeSpan.FromHours(5));

    private static ConsoleEntry E(int ms, ConsoleLevel level, string message, ConsoleSource source = ConsoleSource.Script, string? origin = null) =>
        new(T0.AddMilliseconds(ms), level, source, message, origin);

    private static ScriptRunResult Run(
        ScriptPhase phase = ScriptPhase.PostResponse,
        ScriptRunStatus status = ScriptRunStatus.Succeeded,
        ScriptError? error = null,
        ConsoleEntry[]? console = null,
        TestResult[]? tests = null) =>
        new(phase, "request", ScriptLanguage.JavaScript, status, error, console ?? [], tests ?? [], TimeSpan.Zero);

    private static TestResult T(string name, bool passed, string? message = null) =>
        new(name, passed, message, TimeSpan.Zero, "request");

    private static List<string> Messages(ConsoleViewModel vm) => [.. vm.Lines.Select(l => l.Message)];

    [Fact]
    public void Run_with_logs_tests_and_summary()
    {
        var vm = new ConsoleViewModel();
        vm.AppendRun(Run(
            console: [E(5, ConsoleLevel.Warn, "w"), E(0, ConsoleLevel.Log, "a"), E(9, ConsoleLevel.Debug, "d"), E(7, ConsoleLevel.Info, "i")],
            tests: [T("ok", true), T("bad", false, "nope")]));

        Assert.Equal(
            ["Post-response · request", "a", "w", "i", "d", "ok", "bad — nope", "Tests: 1 passed, 1 failed"],
            Messages(vm));
        Assert.Equal(
            [ConsoleLineKind.Header, ConsoleLineKind.Log, ConsoleLineKind.Warn, ConsoleLineKind.Info, ConsoleLineKind.Log, ConsoleLineKind.TestPassed, ConsoleLineKind.TestFailed, ConsoleLineKind.Info],
            vm.Lines.Select(l => l.Kind));
        Assert.Equal(["›", "·", "!", "i", "·", "✓", "×", "i"], vm.Lines.Select(l => l.Symbol));
        Assert.All(vm.Lines, l => Assert.Equal("request", l.Origin));
        Assert.Equal(8, vm.TotalCount);
        Assert.Equal(1, vm.WarnCount);
        Assert.Equal(1, vm.ErrorCount);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public void Silent_successful_run_adds_nothing()
    {
        var vm = new ConsoleViewModel();
        vm.AppendRun(Run());
        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Lines);
    }

    [Fact]
    public void Pre_request_header_text()
    {
        var vm = new ConsoleViewModel();
        vm.AppendRun(Run(ScriptPhase.PreRequest, console: [E(0, ConsoleLevel.Log, "x")]));
        Assert.Equal("Pre-request · request", vm.Lines[0].Message);
    }

    [Fact]
    public void Failed_run_adds_error_line_with_location()
    {
        var vm = new ConsoleViewModel();
        vm.AppendRun(Run(status: ScriptRunStatus.Failed, error: new ScriptError("boom", 3)));
        Assert.Equal(["Post-response · request", "Script error: boom (line 3)"], Messages(vm));
        Assert.True(vm.Lines[1].IsError);
        Assert.Equal(1, vm.ErrorCount);
    }

    [Fact]
    public void Timed_out_run_adds_error_line_without_location()
    {
        var vm = new ConsoleViewModel();
        vm.AppendRun(Run(status: ScriptRunStatus.TimedOut, error: new ScriptError("too slow")));
        Assert.Equal("Timed out: too slow", vm.Lines[1].Message);
    }

    [Fact]
    public void Network_entries_split_the_method_and_flag_errors()
    {
        var vm = new ConsoleViewModel();
        vm.AppendNetwork([
            E(0, ConsoleLevel.Info, "GET https://x.test   200 · 5 ms", ConsoleSource.Network, "pre"),
            E(1, ConsoleLevel.Error, "POST https://x.test   boom", ConsoleSource.Network)]);

        Assert.Equal(["GET", "POST"], vm.Lines.Select(l => l.Method));
        Assert.Equal("https://x.test   200 · 5 ms", vm.Lines[0].Message);
        Assert.Equal("pre", vm.Lines[0].Origin);
        Assert.Equal("↗", vm.Lines[0].Symbol);
        Assert.False(vm.Lines[0].IsError);
        Assert.True(vm.Lines[1].IsError);
        Assert.Equal(1, vm.ErrorCount);
    }

    [Fact]
    public void Report_orders_pre_runs_then_network_by_time_then_post_runs()
    {
        var pre = Run(ScriptPhase.PreRequest, console: [E(0, ConsoleLevel.Log, "pre-log")]);
        var post = Run(console: [E(50, ConsoleLevel.Log, "post-log")]);
        var net = new[]
        {
            E(20, ConsoleLevel.Info, "GET b", ConsoleSource.Network),
            E(10, ConsoleLevel.Info, "GET a", ConsoleSource.Network),
            E(20, ConsoleLevel.Info, "GET c", ConsoleSource.Network),
        };
        var vm = new ConsoleViewModel();
        vm.AppendReport(new ScriptedSendReport(null, [post, pre], [], [], null, null, net));

        Assert.Equal(
            ["Pre-request · request", "pre-log", "a", "b", "c", "Post-response · request", "post-log"],
            Messages(vm));
    }

    [Fact]
    public void Filters_and_search_combine_with_counts_over_all_lines()
    {
        var vm = new ConsoleViewModel();
        vm.AppendRun(Run(
            console: [E(0, ConsoleLevel.Log, "Hello"), E(1, ConsoleLevel.Warn, "careful"), E(2, ConsoleLevel.Error, "HELLO bad")],
            tests: [T("t", true)]));
        vm.AppendNetwork([E(3, ConsoleLevel.Error, "GET fail", ConsoleSource.Network)]);

        vm.Filter = ConsoleFilter.Warn;
        Assert.Equal(["careful"], Messages(vm));

        vm.Filter = ConsoleFilter.Error;
        Assert.Equal(["HELLO bad", "fail"], Messages(vm));

        vm.Filter = ConsoleFilter.Log;
        Assert.Equal(["Hello", "t", "Tests: 1 passed, 0 failed"], Messages(vm));

        vm.Filter = ConsoleFilter.All;
        vm.Search = "hello";
        Assert.Equal(["Hello", "HELLO bad"], Messages(vm));

        vm.Filter = ConsoleFilter.Error;
        Assert.Equal(["HELLO bad"], Messages(vm));

        vm.Search = "REQUEST"; // matches the origin
        Assert.Equal(["HELLO bad"], Messages(vm));

        Assert.Equal(7, vm.TotalCount);
        Assert.Equal(1, vm.WarnCount);
        Assert.Equal(2, vm.ErrorCount);
    }

    [Fact]
    public void Oldest_lines_are_dropped_beyond_capacity_and_counts_follow()
    {
        var vm = new ConsoleViewModel(capacity: 3);
        vm.AppendRun(Run(console: [E(0, ConsoleLevel.Error, "e"), E(1, ConsoleLevel.Warn, "w1"), E(2, ConsoleLevel.Warn, "w2")]));

        Assert.Equal(["e", "w1", "w2"], Messages(vm));
        Assert.Equal(3, vm.TotalCount);
        Assert.Equal(1, vm.ErrorCount);

        vm.AppendNetwork([E(3, ConsoleLevel.Info, "GET z", ConsoleSource.Network)]);
        Assert.Equal(["w1", "w2", "z"], Messages(vm));
        Assert.Equal(0, vm.ErrorCount);
        Assert.Equal(2, vm.WarnCount);
    }

    [Fact]
    public void Clear_empties_the_console()
    {
        var vm = new ConsoleViewModel();
        vm.AppendRun(Run(console: [E(0, ConsoleLevel.Warn, "w")]));
        vm.ClearCommand.Execute(null);

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Lines);
        Assert.Equal(0, vm.TotalCount);
        Assert.Equal(0, vm.WarnCount);
    }

    [Fact]
    public async Task Copy_sends_the_visible_lines_to_the_clipboard()
    {
        var dialogs = new FakeDialogService();
        var vm = new ConsoleViewModel(dialogs);
        vm.AppendRun(Run(console: [E(0, ConsoleLevel.Log, "one"), E(1, ConsoleLevel.Warn, "two")]));
        vm.Filter = ConsoleFilter.Warn;

        await vm.CopyCommand.ExecuteAsync(null);

        var expected = $"{T0.AddMilliseconds(1).ToLocalTime():HH:mm:ss.fff} ! two [request]";
        Assert.Equal([expected], dialogs.ClipboardTexts);
    }

    [Fact]
    public async Task Copy_without_a_dialog_service_does_nothing()
    {
        var vm = new ConsoleViewModel();
        vm.AppendRun(Run(console: [E(0, ConsoleLevel.Log, "one")]));
        await vm.CopyCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Lines.Count);
    }

    [Fact]
    public void Time_text_is_the_local_time_of_the_entry()
    {
        var vm = new ConsoleViewModel();
        vm.AppendRun(Run(console: [E(123, ConsoleLevel.Log, "x")]));
        var expected = T0.AddMilliseconds(123).ToLocalTime().ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, vm.Lines[1].TimeText);
        Assert.Matches(@"^\d\d:\d\d:\d\d\.\d{3}$", vm.Lines[1].TimeText);
    }
}
