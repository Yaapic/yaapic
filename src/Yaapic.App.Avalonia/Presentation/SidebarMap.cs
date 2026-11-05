using Avalonia.Data.Converters;
using Yaapic.ViewModels.Shell;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Converters that map the sidebar mode to control state, without reflection.</summary>
public static class SidebarMap
{
    /// <summary>Converts the sidebar mode to <see langword="true"/> while the collection tree is shown.</summary>
    public static FuncValueConverter<SidebarMode, bool> IsCollections { get; } = new(m => m == SidebarMode.Collections);

    /// <summary>Converts the sidebar mode to <see langword="true"/> while the request history is shown.</summary>
    public static FuncValueConverter<SidebarMode, bool> IsHistory { get; } = new(m => m == SidebarMode.History);
}
