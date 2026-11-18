using System.Text;
using Yaapic.Core.Diff;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Responses;

namespace Yaapic.ViewModels.Tests;

public sealed class ResponseDiffViewModelTests
{
    private static readonly DateTimeOffset At = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static HttpResponseData Text(string body, int status = 200, string reason = "OK", params KeyValueEntry[] headers) =>
        new(status, reason, [new KeyValueEntry("Content-Type", "text/plain"), .. headers], Encoding.UTF8.GetBytes(body), TimeSpan.FromMilliseconds(1), "https://x.test");

    private static string Lines(int count, int changeAt = -1, string changed = "X")
    {
        var sb = new StringBuilder();
        for (var i = 1; i <= count; i++)
        {
            sb.Append(i == changeAt ? changed : "line" + i).Append('\n');
        }

        return sb.ToString();
    }

    [Fact]
    public void First_response_has_no_comparison_and_second_compares_with_previous()
    {
        var vm = new ResponseDiffViewModel();
        vm.Record(Text("a\nb\n"), At);
        Assert.False(vm.HasComparison);
        Assert.Empty(vm.BodyLines);

        vm.Record(Text("a\nc\n"), At);

        Assert.True(vm.HasComparison);
        Assert.Contains(vm.BodyLines, l => l.Op == DiffOp.Removed && l.Text == "b");
        Assert.Contains(vm.BodyLines, l => l.Op == DiffOp.Added && l.Text == "c" && l.NewNumber == "2" && l.OldNumber == "");
        Assert.Equal("1 added, 1 removed, 0 header changes", vm.SummaryText);
        Assert.Equal("", vm.StatusLine);
    }

    [Fact]
    public void Identical_responses_report_no_differences()
    {
        var vm = new ResponseDiffViewModel();
        vm.Record(Text("a\n"), At);
        vm.Record(Text("a\n"), At);

        Assert.True(vm.HasComparison);
        Assert.Equal("No differences", vm.SummaryText);
        Assert.Empty(vm.BodyLines);
    }

    [Fact]
    public void Status_change_and_header_changes_are_exposed()
    {
        var vm = new ResponseDiffViewModel();
        vm.Record(Text("a", 200, "OK", new KeyValueEntry("X-A", "1")), At);
        vm.Record(Text("a", 404, "Not Found", new KeyValueEntry("X-A", "2")), At);

        Assert.Equal("200 OK -> 404 Not Found", vm.StatusLine);
        var header = Assert.Single(vm.Headers);
        Assert.Equal(new HeaderDiffItem("X-A", HeaderDiffKind.Changed, "1", "2"), header);
        Assert.Equal("0 added, 0 removed, 1 header changes", vm.SummaryText);
    }

    [Fact]
    public void Pin_compares_the_next_response_with_the_baseline_and_unpin_goes_back()
    {
        var vm = new ResponseDiffViewModel();
        Assert.False(vm.PinCommand.CanExecute(null));
        vm.Record(Text("one\n"), At);
        Assert.True(vm.PinCommand.CanExecute(null));
        vm.PinCommand.Execute(null);
        Assert.True(vm.HasBaseline);
        Assert.Contains("Baseline pinned at", vm.PinnedText);
        vm.Record(Text("two\n"), At);
        vm.Record(Text("one\n"), At);

        vm.UseBaseline = true;
        Assert.Equal("No differences", vm.SummaryText);

        vm.UseBaseline = false;
        Assert.Equal("1 added, 1 removed, 0 header changes", vm.SummaryText);

        vm.UseBaseline = true;
        vm.UnpinCommand.Execute(null);
        Assert.False(vm.HasBaseline);
        Assert.False(vm.UseBaseline);
        Assert.Equal("", vm.PinnedText);
        Assert.False(vm.UnpinCommand.CanExecute(null));
    }

    [Fact]
    public void UseBaseline_is_refused_without_baseline()
    {
        var vm = new ResponseDiffViewModel();
        vm.UseBaseline = true;
        Assert.False(vm.UseBaseline);
    }

    [Fact]
    public void Options_recompute()
    {
        var vm = new ResponseDiffViewModel();
        vm.Record(Text("a\n  b\n", 200, "OK", new KeyValueEntry("Date", "x")), At);
        vm.Record(Text("a\nb\n", 200, "OK", new KeyValueEntry("Date", "y")), At);
        Assert.Empty(vm.Headers);
        Assert.Equal("1 added, 1 removed, 0 header changes", vm.SummaryText);

        vm.IgnoreWhitespace = true;
        Assert.Equal("No differences", vm.SummaryText);

        vm.IgnoreVolatileHeaders = false;
        Assert.Single(vm.Headers);
        Assert.Equal("0 added, 0 removed, 1 header changes", vm.SummaryText);
    }

    [Fact]
    public void Long_equal_runs_are_folded_with_three_lines_of_context()
    {
        var vm = new ResponseDiffViewModel();
        vm.Record(Text(Lines(30)), At);
        vm.Record(Text(Lines(30, 15)), At);

        // 14 equal, change (removed + added), 15 equal: fold + 3 context, the change, 3 context + fold.
        var fold = vm.BodyLines.Where(l => l.IsFold).ToList();
        Assert.Equal(2, fold.Count);
        Assert.Equal("11 unchanged lines", fold[0].Text);
        Assert.Equal("12 unchanged lines", fold[1].Text);
        Assert.Equal(10, vm.BodyLines.Count);
        Assert.True(vm.BodyLines[0].IsFold);
        Assert.Equal("line12", vm.BodyLines[1].Text);
        Assert.Equal("12", vm.BodyLines[1].OldNumber);

        vm.ExpandFolds = true;
        Assert.DoesNotContain(vm.BodyLines, l => l.IsFold);
        Assert.Equal(31, vm.BodyLines.Count);
    }

    [Fact]
    public void Short_equal_runs_and_edge_runs_follow_the_threshold()
    {
        var vm = new ResponseDiffViewModel();
        vm.Record(Text(Lines(10)), At);
        vm.Record(Text(Lines(10, 5)), At);

        // Runs of 4 and 5 lines are not folded.
        Assert.DoesNotContain(vm.BodyLines, l => l.IsFold);

        vm.Record(Text(Lines(10, 8) + "tail\n"), At);
        Assert.DoesNotContain(vm.BodyLines, l => l.IsFold);
    }

    [Fact]
    public void Binary_response_keeps_snapshots_and_sets_the_reason()
    {
        var vm = new ResponseDiffViewModel();
        vm.Record(Text("a"), At);
        vm.Record(Text("b"), At);
        var binary = new HttpResponseData(
            200, "OK", [new KeyValueEntry("Content-Type", "application/octet-stream")], [0, 1, 2, 0xFF, 0], TimeSpan.Zero, "https://x.test");

        vm.Record(binary, At);

        Assert.Equal("Too large or binary to compare", vm.UnavailableReason);
        Assert.Equal("a", vm.Previous!.BodyText);
        Assert.Equal("b", vm.Current!.BodyText);
        Assert.True(vm.HasComparison);

        vm.Record(Text("c"), At);
        Assert.Null(vm.UnavailableReason);
    }

    [Fact]
    public void Too_many_lines_set_the_reason()
    {
        var vm = new ResponseDiffViewModel();
        vm.Record(Text(Lines(LineDiff.MaxTotalLines)), At);
        vm.Record(Text(Lines(10)), At);

        Assert.False(vm.HasComparison);
        Assert.Equal("Too large or binary to compare", vm.UnavailableReason);
    }

    [Fact]
    public void Clear_forgets_snapshots_but_keeps_the_baseline()
    {
        var vm = new ResponseDiffViewModel();
        vm.Record(Text("a"), At);
        vm.PinCommand.Execute(null);
        vm.Record(Text("b"), At);

        vm.Clear();

        Assert.Null(vm.Previous);
        Assert.Null(vm.Current);
        Assert.False(vm.HasComparison);
        Assert.True(vm.HasBaseline);
        Assert.False(vm.PinCommand.CanExecute(null));
    }

    private static SendReport Ok(string body) => new(SendResult.Success(Text(body)), []);

    [Fact]
    public void Panel_records_successes_only_and_keeps_snapshots_across_sends()
    {
        var panel = new ResponsePanelViewModel(null, new FixedClock(At));

        panel.ShowSending();
        Assert.Null(panel.Diff.Current);
        panel.ShowReport(Ok("a"));
        panel.ShowSending();
        Assert.NotNull(panel.Diff.Current);
        panel.ShowReport(new SendReport(SendResult.Fail(SendFailureKind.Timeout, "boom"), []));
        Assert.Equal("a", panel.Diff.Current!.BodyText);
        Assert.Null(panel.Diff.Previous);

        panel.ShowReport(Ok("b"));
        Assert.True(panel.Diff.HasComparison);
        Assert.Equal(At, panel.Diff.Current!.At);

        panel.Diff.PinCommand.Execute(null);
        panel.Clear();
        Assert.Null(panel.Diff.Current);
        Assert.True(panel.Diff.HasBaseline);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
