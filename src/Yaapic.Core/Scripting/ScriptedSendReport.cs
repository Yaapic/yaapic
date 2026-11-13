using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Scripting;

/// <summary>The outcome of sending a request with its scripts.</summary>
/// <param name="Report">The send report, or <see langword="null"/> when a script aborted the send.</param>
/// <param name="Runs">Every script run executed, in order, the failed one included.</param>
/// <param name="Changes">The changes scripts made to persisted scopes; empty when the send was aborted.</param>
/// <param name="Warnings">The warnings raised while scripts wrote variables.</param>
/// <param name="AbortedBy">The first run that did not succeed, or <see langword="null"/>.</param>
/// <param name="SentDefinition">The definition after pre-request mutation; <see langword="null"/> when aborted.</param>
/// <param name="NetworkLog">The network console lines: the <c>yaapic.sendRequest</c> sub-requests in order, then the main request unless the send was aborted.</param>
public sealed record ScriptedSendReport(
    SendReport? Report,
    IReadOnlyList<ScriptRunResult> Runs,
    IReadOnlyList<VariableChange> Changes,
    IReadOnlyList<string> Warnings,
    ScriptRunResult? AbortedBy,
    HttpRequestDefinition? SentDefinition,
    IReadOnlyList<ConsoleEntry> NetworkLog)
{
    /// <summary>Gets the console entries of every run, in order.</summary>
    public IReadOnlyList<ConsoleEntry> Console => [.. Runs.SelectMany(r => r.Console)];

    /// <summary>Gets the test results of every run, in order.</summary>
    public IReadOnlyList<TestResult> Tests => [.. Runs.SelectMany(r => r.Tests)];
}
