namespace Yaapic.Domain.Monitors;

/// <summary>One failing request of a monitor run.</summary>
/// <param name="RequestName">The request name.</param>
/// <param name="Outcome">The request outcome, as text.</param>
/// <param name="StatusCode">The status code, or <see langword="null"/> when no response was received.</param>
/// <param name="Detail">The first failed test, or the error.</param>
public sealed record MonitorFailure(string RequestName, string Outcome, int? StatusCode, string Detail);
