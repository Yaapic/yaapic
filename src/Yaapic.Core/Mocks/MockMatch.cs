using Yaapic.Domain.Mocks;

namespace Yaapic.Core.Mocks;

/// <summary>A route that matched a request, with the captured path parameters.</summary>
/// <param name="Route">The matching route.</param>
/// <param name="Params">The captured parameters; the rest of the path is under <c>*</c>.</param>
public sealed record MockMatch(MockRoute Route, IReadOnlyDictionary<string, string> Params);
