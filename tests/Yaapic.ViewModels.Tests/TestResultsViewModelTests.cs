using Yaapic.Core.Http;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.ViewModels.Responses;
using TestResult = Yaapic.Domain.Scripting.TestResult;

namespace Yaapic.ViewModels.Tests;

public sealed class TestResultsViewModelTests
{
    private static TestResult T(string name, bool passed, string? message = null, double ms = 3) =>
        new(name, passed, message, TimeSpan.FromMilliseconds(ms), "request");

    private static ScriptRunResult Run(
        ScriptPhase phase = ScriptPhase.PostResponse,
        ScriptRunStatus status = ScriptRunStatus.Succeeded,
        ScriptError? error = null,
        params TestResult[] tests) =>
        new(phase, "request", ScriptLanguage.JavaScript, status, error, [], tests, TimeSpan.Zero);

    private static ScriptedSendReport Scripted(SendReport? report, ScriptRunResult? aborted, params ScriptRunResult[] runs) =>
        new(report, runs, [], [], aborted, null, []);

    private static SendReport Ok() =>
        new(SendResult.Success(new HttpResponseData(200, "OK", [], [], TimeSpan.FromMilliseconds(5), "https://x.test")), []);

    [Fact]
    public void Counts_summary_and_items_for_a_mix()
    {
        var vm = new TestResultsViewModel();
        vm.Show([Run(tests: [T("a", true), T("b", false, "boom", 7)]), Run(tests: [T("c", true)])]);

        Assert.Equal(2, vm.PassedCount);
        Assert.Equal(1, vm.FailedCount);
        Assert.Equal(3, vm.TotalCount);
        Assert.True(vm.HasTests);
        Assert.False(vm.AllPassed);
        Assert.Equal("2/3", vm.Summary);
        Assert.Equal("2/3", vm.BadgeText);
        Assert.Equal(["a", "b", "c"], vm.Items.Select(i => i.Name));
        var failed = vm.Items[1];
        Assert.Equal("Failed", failed.StatusText);
        Assert.Equal("×", failed.Symbol);
        Assert.Equal("7 ms", failed.DurationText);
        Assert.Equal("boom", failed.Message);
        Assert.Equal("Passed", vm.Items[0].StatusText);
        Assert.Equal("✓", vm.Items[0].Symbol);
    }

    [Fact]
    public void All_passed()
    {
        var vm = new TestResultsViewModel();
        vm.Show([Run(tests: [T("a", true)])]);

        Assert.True(vm.AllPassed);
        Assert.Equal("1/1", vm.Summary);
    }

    [Fact]
    public void Filter_failed_keeps_only_failures_and_notifies()
    {
        var vm = new TestResultsViewModel();
        vm.Show([Run(tests: [T("a", true), T("b", false)])]);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Filter = TestFilter.Failed;

        Assert.Equal(["b"], vm.Items.Select(i => i.Name));
        Assert.Contains(nameof(TestResultsViewModel.Items), changed);
        Assert.Equal(2, vm.TotalCount);

        vm.Filter = TestFilter.All;
        Assert.Equal(2, vm.Items.Count);
    }

    [Fact]
    public void Filter_survives_a_new_show()
    {
        var vm = new TestResultsViewModel { Filter = TestFilter.Failed };
        vm.Show([Run(tests: [T("a", true), T("b", false)])]);

        Assert.Equal(["b"], vm.Items.Select(i => i.Name));
    }

    [Theory]
    [InlineData(ScriptRunStatus.Failed, "Script error")]
    [InlineData(ScriptRunStatus.TimedOut, "Timed out")]
    [InlineData(ScriptRunStatus.MemoryLimit, "Memory limit")]
    [InlineData(ScriptRunStatus.Canceled, "Canceled")]
    [InlineData(ScriptRunStatus.Unavailable, "Engine unavailable")]
    public void Errors_per_status(ScriptRunStatus status, string text)
    {
        var vm = new TestResultsViewModel();
        vm.Show([Run(status: status, error: new ScriptError("bad", 3)), Run()]);

        var error = Assert.Single(vm.Errors);
        Assert.Equal(text, error.StatusText);
        Assert.Equal("bad", error.Message);
        Assert.Equal("line 3", error.Location);
        Assert.Equal("request", error.Origin);
        Assert.True(vm.HasErrors);
        Assert.False(vm.HasTests);
        Assert.Equal("!", vm.BadgeText);
    }

    [Fact]
    public void Error_without_line_has_empty_location_and_tests_win_the_badge()
    {
        var vm = new TestResultsViewModel();
        vm.Show([Run(status: ScriptRunStatus.Failed, error: new ScriptError("bad"), tests: [T("a", true)])]);

        Assert.Equal("", vm.Errors[0].Location);
        Assert.Equal("1/1", vm.BadgeText);
    }

    [Fact]
    public void Clear_empties_everything()
    {
        var vm = new TestResultsViewModel();
        vm.Show([Run(status: ScriptRunStatus.Failed, error: new ScriptError("bad"), tests: [T("a", true)])]);

        vm.Clear();

        Assert.Empty(vm.Items);
        Assert.Empty(vm.Errors);
        Assert.Equal(0, vm.TotalCount);
        Assert.Equal("", vm.BadgeText);
        Assert.False(vm.AllPassed);
    }

    [Fact]
    public void ShowScripted_success_with_tests()
    {
        var panel = new ResponsePanelViewModel();
        panel.ShowScripted(Scripted(Ok(), null, Run(tests: [T("a", true)])));

        Assert.Equal(ResponseState.Completed, panel.State);
        Assert.Equal("1/1", panel.Tests.Summary);
    }

    [Fact]
    public void ShowScripted_post_response_error_keeps_the_response()
    {
        var panel = new ResponsePanelViewModel();
        var failed = Run(status: ScriptRunStatus.Failed, error: new ScriptError("oops", 2));
        panel.ShowScripted(Scripted(Ok(), null, failed));

        Assert.Equal(ResponseState.Completed, panel.State);
        Assert.Null(panel.ErrorMessage);
        Assert.Equal("oops", Assert.Single(panel.Tests.Errors).Message);
    }

    [Theory]
    [InlineData(ScriptRunStatus.Failed, "boom", "Pre-request script failed — request: boom")]
    [InlineData(ScriptRunStatus.TimedOut, null, "Pre-request script failed — request: Timed out")]
    public void ShowScripted_pre_request_abort_is_an_error_state(ScriptRunStatus status, string? message, string expected)
    {
        var panel = new ResponsePanelViewModel();
        var aborted = Run(ScriptPhase.PreRequest, status, message is null ? null : new ScriptError(message));
        panel.ShowScripted(Scripted(null, aborted, aborted));

        Assert.Equal(ResponseState.Failed, panel.State);
        Assert.Equal(expected, panel.ErrorMessage);
        Assert.Equal("", panel.BodyText);
        Assert.True(panel.Tests.HasErrors);
    }

    [Fact]
    public void ShowSending_and_Clear_reset_tests()
    {
        var panel = new ResponsePanelViewModel();
        panel.ShowScripted(Scripted(Ok(), null, Run(tests: [T("a", true)])));
        panel.ShowSending();
        Assert.Equal(0, panel.Tests.TotalCount);

        panel.ShowScripted(Scripted(Ok(), null, Run(tests: [T("a", true)])));
        panel.Clear();
        Assert.False(panel.Tests.HasTests);
    }

    [Fact]
    public void ShowReport_without_scripts_leaves_tests_empty()
    {
        var panel = new ResponsePanelViewModel();
        panel.ShowScripted(Scripted(Ok(), null, Run(tests: [T("a", true)])));
        panel.ShowReport(Ok());

        Assert.False(panel.Tests.HasTests);
        Assert.Empty(panel.Tests.Errors);
    }
}
