namespace Yaapic.Core.LoadTesting;

/// <summary>A point-in-time view of a running load test.</summary>
/// <param name="Elapsed">Time since the start.</param>
/// <param name="ActiveUsers">Currently active virtual users.</param>
/// <param name="Overall">Statistics over all samples.</param>
/// <param name="LastSecondThroughput">Requests of the last complete second.</param>
/// <param name="Buckets">Per-second buckets, from second 0.</param>
public sealed record LoadSnapshot(
    TimeSpan Elapsed,
    int ActiveUsers,
    LoadStats Overall,
    double LastSecondThroughput,
    IReadOnlyList<LoadBucket> Buckets);
