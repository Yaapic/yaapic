namespace Yaapic.ViewModels.Palette;

/// <summary>Provides palette items; queried each time the palette opens so the data is fresh.</summary>
public interface IPaletteSource
{
    /// <summary>Gets the current items.</summary>
    /// <returns>The items, in display order.</returns>
    IReadOnlyList<PaletteItem> GetItems();
}
