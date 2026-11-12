using Yaapic.Core.Preferences;
using Yaapic.UI.Abstractions.Theming;

namespace Yaapic.ViewModels.Theming;

/// <summary>One resource the accent colour replaces.</summary>
/// <param name="Key">The resource key.</param>
/// <param name="Rgb">The colour as <c>0xRRGGBB</c>.</param>
/// <param name="IsBrush"><see langword="true"/> for a <c>SolidColorBrush</c>, <see langword="false"/> for a <c>Color</c>.</param>
public readonly record struct AccentResource(string Key, int Rgb, bool IsBrush);

/// <summary>Lists the resources to replace in a theme dictionary for an accent, shared by both front-ends.</summary>
public static class AccentResources
{
    /// <summary>Lists every resource an accent sets.</summary>
    /// <param name="winUi"><see langword="true"/> for the WinUI keys, <see langword="false"/> for the Avalonia ones.</param>
    /// <param name="tokens">The accent tokens of the theme.</param>
    /// <returns>The resources, brushes and colours.</returns>
    public static IReadOnlyList<AccentResource> Entries(bool winUi, AccentTokens tokens)
    {
        var keys = winUi ? AccentResourceKeys.WinUI : AccentResourceKeys.Avalonia;
        var result = new List<AccentResource>();
        Add(keys.Fill, tokens.Fill);
        Add(keys.Hover, tokens.Hover);
        Add(keys.Pressed, tokens.Pressed);
        Add(keys.Border, tokens.Border);
        Add(keys.Text, tokens.Text);
        Add(keys.On, tokens.On);
        return result;

        void Add(AccentRoleKeys role, int rgb)
        {
            result.AddRange(role.Brushes.Select(k => new AccentResource(k, rgb, IsBrush: true)));
            result.AddRange(role.Colors.Select(k => new AccentResource(k, rgb, IsBrush: false)));
        }
    }
}
