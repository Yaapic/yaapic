namespace Yaapic.Core.LoadTesting;

/// <summary>Per-second aggregate of a load test.</summary>
/// <param name="Second">Zero-based second since the start.</param>
/// <param name="Requests">Requests completed in that second.</param>
/// <param name="Errors">Errors in that second.</param>
/// <param name="AverageMs">Average duration of the requests of that second.</param>
/// <param name="ActiveUsers">Active virtual users during that second.</param>
public sealed record LoadBucket(int Second, int Requests, int Errors, double AverageMs, int ActiveUsers);
