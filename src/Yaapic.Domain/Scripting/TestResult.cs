namespace Yaapic.Domain.Scripting;

/// <summary>The outcome of one test declared by a script.</summary>
/// <param name="Name">The test name.</param>
/// <param name="Passed">Whether the test passed.</param>
/// <param name="Message">The assertion message of a failed test, null when it passed.</param>
/// <param name="Duration">How long the test took.</param>
/// <param name="Origin">Where the test was declared, for example "post-response · Users API".</param>
public sealed record TestResult(string Name, bool Passed, string? Message, TimeSpan Duration, string Origin);
