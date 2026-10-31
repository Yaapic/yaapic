namespace Yaapic.Core.Variables;

/// <summary>The outcome of resolving the placeholders of a text.</summary>
/// <param name="Text">The text with every resolvable placeholder replaced.</param>
/// <param name="Unresolved">The distinct trimmed names left unresolved, in order of first occurrence.</param>
public sealed record ResolveResult(string Text, IReadOnlyList<string> Unresolved);
