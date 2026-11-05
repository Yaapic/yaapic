namespace Yaapic.ViewModels.Palette;

/// <summary>A palette item that matched the current query.</summary>
/// <param name="Item">The matching item.</param>
/// <param name="TitleMatches">Ascending positions of the matched characters in the title (empty for a blank query).</param>
public sealed record PaletteResult(PaletteItem Item, IReadOnlyList<int> TitleMatches);
