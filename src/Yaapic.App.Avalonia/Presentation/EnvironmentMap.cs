using Yaapic.Localization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Maps the state of an environment to list presentation; exposed as converters for compiled bindings.</summary>
public static class EnvironmentMap
{
    /// <summary>Converts the active flag to the brush of the dot in front of a name: success when active, tertiary text otherwise.</summary>
    public static FuncValueConverter<bool, IBrush?> Dot { get; } =
        new(isActive => BrushMap.Get(isActive ? "YaapicSuccessBrush" : "YaapicTextTertiaryBrush"));

    /// <summary>Converts the edited flag to the font weight of a list entry: semi-bold when it is the selected one.</summary>
    public static FuncValueConverter<bool, FontWeight> Weight { get; } =
        new(isEdited => isEdited ? FontWeight.SemiBold : FontWeight.Normal);

    /// <summary>Converts the selected flag of the fixed Globals row to its background: raised surface when selected, sidebar otherwise.</summary>
    public static FuncValueConverter<bool, IBrush?> RowBackground { get; } =
        new(isSelected => BrushMap.Get(isSelected ? "YaapicSurfaceRaisedBrush" : "YaapicSurfaceSidebarBrush"));

    /// <summary>Converts a variable count to its caption, for example <c>3 variables</c>.</summary>
    public static FuncValueConverter<int, string?> VariableCount { get; } =
        new(count => count == 1 ? Strings.EnvironmentsVariableOne : Strings.EnvironmentsVariableCount(count));
}
