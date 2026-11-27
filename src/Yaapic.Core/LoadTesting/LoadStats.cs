namespace Yaapic.Core.LoadTesting;

/// <summary>Aggregated statistics of a set of load samples.</summary>
/// <param name="Count">Number of samples.</param>
/// <param name="Errors">Number of error samples.</param>
/// <param name="ErrorRate">Errors divided by count, 0 when there is no sample.</param>
/// <param name="MinMs">Fastest duration.</param>
/// <param name="AverageMs">Average duration.</param>
/// <param name="MaxMs">Slowest duration.</param>
/// <param name="P50Ms">50th percentile (nearest rank).</param>
/// <param name="P90Ms">90th percentile (nearest rank).</param>
/// <param name="P95Ms">95th percentile (nearest rank).</param>
/// <param name="P99Ms">99th percentile (nearest rank).</param>
/// <param name="Throughput">Samples per second.</param>
public sealed record LoadStats(
    long Count,
    long Errors,
    double ErrorRate,
    int MinMs,
    double AverageMs,
    int MaxMs,
    int P50Ms,
    int P90Ms,
    int P95Ms,
    int P99Ms,
    double Throughput)
{
    /// <summary>Gets the statistics of an empty set.</summary>
    public static LoadStats Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
