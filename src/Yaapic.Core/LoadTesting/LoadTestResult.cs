namespace Yaapic.Core.LoadTesting;

/// <summary>The outcome of a load test.</summary>
/// <param name="Plan">The plan that ran.</param>
/// <param name="StartedAt">When the test started.</param>
/// <param name="Duration">How long the test actually ran.</param>
/// <param name="Canceled">Whether the caller canceled the test before its duration was over.</param>
/// <param name="Overall">Statistics over all samples.</param>
/// <param name="PerRequest">Statistics of each request of the plan, in plan order.</param>
/// <param name="Buckets">Per-second buckets, from second 0.</param>
/// <param name="ErrorMessages">The first distinct error messages.</param>
/// <param name="SamplesTruncated">Whether some durations were not stored, so percentiles are approximate.</param>
public sealed record LoadTestResult(
    LoadTestPlan Plan,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    bool Canceled,
    LoadStats Overall,
    IReadOnlyList<LoadStats> PerRequest,
    IReadOnlyList<LoadBucket> Buckets,
    IReadOnlyList<string> ErrorMessages,
    bool SamplesTruncated);
