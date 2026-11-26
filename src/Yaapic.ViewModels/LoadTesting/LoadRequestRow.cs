namespace Yaapic.ViewModels.LoadTesting;

/// <summary>One line of the per-request results table of a load test.</summary>
/// <param name="Name">The request name.</param>
/// <param name="Method">The HTTP method text.</param>
/// <param name="Count">The number of completed requests.</param>
/// <param name="Errors">The number of errors.</param>
/// <param name="AverageText">The average duration.</param>
/// <param name="P90Text">The 90th percentile.</param>
/// <param name="P95Text">The 95th percentile.</param>
/// <param name="P99Text">The 99th percentile.</param>
/// <param name="MaxText">The slowest duration.</param>
public sealed record LoadRequestRow(
    string Name,
    string Method,
    long Count,
    long Errors,
    string AverageText,
    string P90Text,
    string P95Text,
    string P99Text,
    string MaxText);
