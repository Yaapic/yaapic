using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using Windows.UI.Text;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Maps the state of an environment to list presentation; used through <c>x:Bind</c> function binding.</summary>
public static class EnvironmentMap
{
    /// <summary>Gets the brush of the dot in front of an environment name: success when active, tertiary text otherwise.</summary>
    /// <param name="isActive">Whether the environment is the active one.</param>
    /// <returns>The brush from the application resources.</returns>
    public static Brush Dot(bool isActive) =>
        (Brush)Application.Current.Resources[isActive ? "YaapicSuccessBrush" : "YaapicTextTertiaryBrush"];

    /// <summary>Gets the font weight of a list entry: semi-bold when it is the selected one.</summary>
    /// <param name="isEdited">Whether the environment is the edited one.</param>
    /// <returns>The font weight.</returns>
    public static FontWeight Weight(bool isEdited) =>
        isEdited ? FontWeights.SemiBold : FontWeights.Normal;

    /// <summary>Gets the caption under the environment name.</summary>
    /// <param name="count">The number of enabled, non-empty variables.</param>
    /// <returns>For example <c>3 variables</c>.</returns>
    public static string VariableCount(int count) => count == 1 ? "1 variable" : $"{count} variables";
}
