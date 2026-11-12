using Yaapic.ViewModels.Tabs;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Palette;

/// <summary>Offers switching to an open tab.</summary>
public sealed class TabsPaletteSource : IPaletteSource
{
    private readonly TabsViewModel _tabs;

    /// <summary>Creates the source.</summary>
    /// <param name="tabs">The open tabs.</param>
    public TabsPaletteSource(TabsViewModel tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        _tabs = tabs;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PaletteItem> GetItems() =>
        [.. _tabs.Tabs.Select(tab => new PaletteItem(
            $"tab:{tab.Id}",
            Strings.PaletteGoToTab(tab.DisplayName),
            $"{tab.MethodText}  {tab.Url}",
            Strings.PaletteCategoryTab,
            null,
            () =>
            {
                _tabs.ActiveTab = tab;
                return Task.CompletedTask;
            }))];
}
