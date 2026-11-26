using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Runner;

/// <summary>The result of one request in one iteration.</summary>
/// <param name="RequestId">The request identifier.</param>
/// <param name="Name">The request name.</param>
/// <param name="Path">The folder path of the request.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Url">The request's URL template.</param>
/// <param name="Outcome">How the request ended.</param>
/// <param name="StatusCode">The status code, or <see langword="null"/> when no response was received.</param>
/// <param name="Duration">The response time.</param>
/// <param name="SizeBytes">The response body size.</param>
/// <param name="Tests">The test results.</param>
/// <param name="Console">The console entries.</param>
/// <param name="Error">The error text, or <see langword="null"/>.</param>
public sealed record RunRequestResult(
    Guid RequestId,
    string Name,
    string Path,
    HttpMethodKind Method,
    string Url,
    RunRequestOutcome Outcome,
    int? StatusCode,
    TimeSpan Duration,
    long SizeBytes,
    IReadOnlyList<TestResult> Tests,
    IReadOnlyList<ConsoleEntry> Console,
    string? Error);
