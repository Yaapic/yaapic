using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Yaapic.Core.Preferences;
using Yaapic.ViewModels.Theming;

namespace Yaapic.App.Avalonia.Services;

/// <summary>Applies the accent colour to the running app: it rewrites the accent entries of the dark and light theme dictionaries.</summary>
internal static class AccentApplier
{
    /// <summary>Applies an accent choice (a preset id, "system" or a custom <c>#RRGGBB</c>).</summary>
    /// <param name="app">The application.</param>
    /// <param name="choice">The accent choice.</param>
    public static void Apply(Application app, string choice)
    {
        foreach (var (variant, dark) in new[] { (ThemeVariant.Dark, true), (ThemeVariant.Light, false) })
        {
            if (Find(app.Resources, variant) is not { } dictionary)
            {
                continue;
            }

            var tokens = AccentPalette.ForChoice(choice, dark) ?? AccentPalette.Derive(SystemAccent(app), dark);
            foreach (var resource in AccentResources.Entries(winUi: false, tokens))
            {
                var color = Color.FromRgb((byte)(resource.Rgb >> 16), (byte)(resource.Rgb >> 8), (byte)resource.Rgb);
                // Replacing the entry (not editing it) raises the resources-changed notification the DynamicResource bindings listen to.
                dictionary[resource.Key] = resource.IsBrush ? new SolidColorBrush(color) : color;
            }
        }
    }

    private static int SystemAccent(Application app)
    {
        var c = app.PlatformSettings?.GetColorValues().AccentColor1 ?? Color.Parse("#1FC7D4");
        return (c.R << 16) | (c.G << 8) | c.B;
    }

    private static ResourceDictionary? Find(IResourceDictionary root, ThemeVariant variant)
    {
        if (root is ResourceDictionary own
            && own.ThemeDictionaries.TryGetValue(variant, out var provider)
            && provider is ResourceDictionary themed
            && themed.ContainsKey("YaapicAccentFillBrush"))
        {
            return themed;
        }

        foreach (var merged in root.MergedDictionaries)
        {
            if (merged is IResourceDictionary child && Find(child, variant) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
