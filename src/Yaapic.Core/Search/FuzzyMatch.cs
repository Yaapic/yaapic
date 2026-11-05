namespace Yaapic.Core.Search;

/// <summary>The result of a successful fuzzy match.</summary>
/// <param name="Score">Higher is better.</param>
/// <param name="Indices">Ascending positions in the text of the matched characters.</param>
public sealed record FuzzyMatch(int Score, IReadOnlyList<int> Indices);
