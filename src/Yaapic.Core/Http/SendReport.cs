using Yaapic.Domain.Http;

namespace Yaapic.Core.Http;

/// <summary>The outcome of a send, with the variables that could not be resolved.</summary>
/// <param name="Result">The transport result.</param>
/// <param name="UnresolvedVariables">The distinct unresolved variable names, in order of first occurrence.</param>
public sealed record SendReport(SendResult Result, IReadOnlyList<string> UnresolvedVariables);
