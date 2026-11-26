using Yaapic.Core.Http;
using Yaapic.Core.Runner;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using TestResult = Yaapic.Domain.Scripting.TestResult;

namespace Yaapic.Core.Tests.Runner;

public class RunModelTests
{
    private static CollectionNode Req(string name) => CollectionNode.ForRequest(HttpRequestDefinition.Create(name));

    private static CollectionNode Folder(string name, params CollectionNode[] children)
        => CollectionNode.ForFolder(FolderDefinition.Create(name), children);

    private static IterationDataRow Row(string value) => new([new KeyValueEntry("k", value)]);

    private static RunPlan PlanWith(params IterationDataRow[] data)
        => new("p", [], RunOptions.Default, data);

    [Fact]
    public void ListRequests_walks_depth_first_with_paths()
    {
        var root = Folder("Root", Req("b"), Req("a"), Folder("Sub", Req("c"), Folder("Deep", Req("d"))), Folder("Empty"));

        var items = RunPlanBuilder.ListRequests(root);

        // Folders sort before requests, so the subfolders come first.
        Assert.Equal(["d", "c", "a", "b"], items.Select(i => i.Request.Name));
        Assert.Equal(["Root/Sub/Deep", "Root/Sub", "Root", "Root"], items.Select(i => i.Path));
    }

    [Fact]
    public void ListRequests_of_an_empty_folder_is_empty()
        => Assert.Empty(RunPlanBuilder.ListRequests(Folder("Root")));

    [Fact]
    public void ListRequests_rejects_a_request_node()
        => Assert.Throws<ArgumentException>(() => RunPlanBuilder.ListRequests(Req("a")));

    [Fact]
    public void RowFor_without_data_is_empty()
        => Assert.Same(IterationDataRow.Empty, PlanWith().RowFor(3));

    [Fact]
    public void RowFor_returns_the_row_then_the_last_one_past_the_end()
    {
        var plan = PlanWith(Row("x"), Row("y"));
        Assert.Equal("x", plan.RowFor(0).Values[0].Value);
        Assert.Equal("y", plan.RowFor(1).Values[0].Value);
        Assert.Equal("y", plan.RowFor(5).Values[0].Value);
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(10000, 60000, true)]
    [InlineData(0, 0, false)]
    [InlineData(10001, 0, false)]
    [InlineData(1, -1, false)]
    [InlineData(1, 60001, false)]
    public void Validate_checks_the_bounds(int iterations, int delay, bool valid)
    {
        var options = new RunOptions(iterations, delay);
        if (valid)
        {
            options.Validate();
        }
        else
        {
            Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
        }
    }

    private static RunRequestResult Result(RunRequestOutcome outcome, int? status, int ms, params TestResult[] tests)
        => new(Guid.NewGuid(), "r", "p", HttpMethodKind.Get, "u", outcome, status, TimeSpan.FromMilliseconds(ms), 0, tests, [], null);

    [Fact]
    public void Summary_counts_every_outcome()
    {
        var pass = new TestResult("ok", true, null, TimeSpan.Zero, "o");
        var fail = new TestResult("ko", false, "m", TimeSpan.Zero, "o");
        var result = new RunResult("run", DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(2), RunStatus.Completed, null,
            RunOptions.Default, 0,
            [
                new(0, [Result(RunRequestOutcome.Passed, 200, 100, pass), Result(RunRequestOutcome.Failed, 500, 300, pass, fail)]),
                new(1, [Result(RunRequestOutcome.Error, null, 0), Result(RunRequestOutcome.Skipped, null, 0)]),
            ]);

        var s = RunSummary.From(result);

        Assert.Equal(2, s.Sent);
        Assert.Equal(1, s.Skipped);
        Assert.Equal(1, s.Errors);
        Assert.Equal(1, s.FailedRequests);
        Assert.Equal(2, s.TestsPassed);
        Assert.Equal(1, s.TestsFailed);
        Assert.Equal(TimeSpan.FromMilliseconds(200), s.AverageResponseTime);
        Assert.Equal(TimeSpan.FromSeconds(2), s.TotalDuration);
    }

    [Fact]
    public void Summary_average_is_zero_without_responses()
    {
        var result = new RunResult("run", DateTimeOffset.UnixEpoch, TimeSpan.Zero, RunStatus.Completed, null, RunOptions.Default, 0, []);
        Assert.Equal(TimeSpan.Zero, RunSummary.From(result).AverageResponseTime);
    }

    private static ScriptRunResult Run(ScriptRunStatus status = ScriptRunStatus.Succeeded, params TestResult[] tests)
        => new(ScriptPhase.PostResponse, "o", ScriptLanguage.JavaScript, status, null, [], tests, TimeSpan.Zero);

    private static SendReport Sent(bool ok) => new(
        ok ? SendResult.Success(new HttpResponseData(200, "OK", [], [], TimeSpan.Zero, "u"))
           : SendResult.Fail(SendFailureKind.Timeout, "t"), []);

    private static ScriptedSendReport Scripted(SendReport? report, ScriptRunResult[] runs, ScriptRunResult? aborted = null, bool skipped = false)
        => new(report, runs, [], [], aborted, null, []) { Skipped = skipped };

    [Fact]
    public void Outcomes_map_each_case()
    {
        var failedTest = new TestResult("t", false, "m", TimeSpan.Zero, "o");
        Assert.Equal(RunRequestOutcome.Skipped, RunOutcomes.From(Scripted(null, [], skipped: true)));
        Assert.Equal(RunRequestOutcome.Error, RunOutcomes.From(Scripted(null, [], Run(ScriptRunStatus.Failed))));
        Assert.Equal(RunRequestOutcome.Error, RunOutcomes.From(Scripted(Sent(false), [])));
        Assert.Equal(RunRequestOutcome.Failed, RunOutcomes.From(Scripted(Sent(true), [Run(tests: failedTest)])));
        Assert.Equal(RunRequestOutcome.Passed, RunOutcomes.From(Scripted(Sent(true), [Run()])));
    }
}
