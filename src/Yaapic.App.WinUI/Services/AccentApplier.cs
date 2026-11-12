using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;
using Yaapic.Core.Preferences;
using Yaapic.ViewModels.Theming;

namespace Yaapic.App.WinUI.Services;

/// <summary>Applies the accent colour to the running app: it rewrites the accent entries of the dark and light theme dictionaries.</summary>
internal static class AccentApplier
{
    /// <summary>Applies an accent choice (a preset id, "system" or a custom <c>#RRGGBB</c>).</summary>
    /// <param name="choice">The accent choice.</param>
    public static void Apply(string choice)
    {
        var themes = FindThemeDictionaries(Application.Current.Resources);
        if (themes is null)
        {
            return;
        }

        foreach (var (themeKey, dark) in new[] { ("Default", true), ("Light", false) })
        {
            if (themes.TryGetValue(themeKey, out var themeObject) && themeObject is ResourceDictionary dictionary)
            {
                var tokens = AccentPalette.ForChoice(choice, dark) ?? AccentPalette.Derive(SystemAccent(), dark);
                foreach (var resource in AccentResources.Entries(winUi: true, tokens))
                {
                    Set(dictionary, resource);
                }
            }
        }
    }

    // A brush is changed in place: styles and templates already hold that object, and WinUI repaints them when its colour changes.
    private static void Set(ResourceDictionary dictionary, AccentResource resource)
    {
        var color = Color.FromArgb(0xFF, (byte)(resource.Rgb >> 16), (byte)(resource.Rgb >> 8), (byte)resource.Rgb);
        if (resource.IsBrush)
        {
            if (dictionary.TryGetValue(resource.Key, out var existing) && existing is SolidColorBrush brush)
            {
                brush.Color = color;
            }
            else
            {
                dictionary[resource.Key] = new SolidColorBrush(color);
            }
        }
        else
        {
            dictionary[resource.Key] = color;
        }
    }

    private static int SystemAccent()
    {
        var c = new UISettings().GetColorValue(UIColorType.Accent);
        return (c.R << 16) | (c.G << 8) | c.B;
    }

    private static IDictionary<object, object>? FindThemeDictionaries(ResourceDictionary root)
    {
        if (root.ThemeDictionaries.ContainsKey("Default") && root.ThemeDictionaries["Default"] is ResourceDictionary d && d.ContainsKey("YaapicAccentFillBrush"))
        {
            return root.ThemeDictionaries;
        }

        foreach (var merged in root.MergedDictionaries)
        {
            if (FindThemeDictionaries(merged) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
