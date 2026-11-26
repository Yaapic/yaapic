namespace Yaapic.Core.LoadTesting;

/// <summary>One completed request of a load test.</summary>
/// <param name="ElapsedMs">Milliseconds since the test started when the request completed.</param>
/// <param name="RequestIndex">Zero-based index of the request in the plan.</param>
/// <param name="DurationMs">Request duration in milliseconds.</param>
/// <param name="StatusCode">HTTP status code, 0 when none was received.</param>
/// <param name="IsError">Whether the sample counts as an error.</param>
public readonly record struct LoadSample(long ElapsedMs, int RequestIndex, int DurationMs, int StatusCode, bool IsError);
