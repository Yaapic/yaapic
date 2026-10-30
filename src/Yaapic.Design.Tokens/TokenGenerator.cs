using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Yaapic.Design.Tokens;

/// <summary>
/// Generates the WinUI and Avalonia colour dictionaries from the DTCG token file. Output uses LF line endings.
/// Only <see cref="JsonDocument"/> is used, so the generator is trim and AOT safe without reflection.
/// </summary>
public static class TokenGenerator
{
    private const string ThemesKey = "dev.yaapic.themes";
    private const string XamlKey = "dev.yaapic.xaml";
    private const string WinUiNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string AvaloniaNs = "https://github.com/avaloniaui";
    private const string XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Generates the WinUI resource dictionary.</summary>
    /// <param name="tokensJson">The DTCG token file contents.</param>
    /// <returns>The XAML of the resource dictionary, with LF line endings.</returns>
    public static string GenerateWinUI(string tokensJson)
    {
        using var doc = JsonDocument.Parse(tokensJson);
        var root = doc.RootElement;
        var tokens = ReadColorTokens(root, "winui");
        var sb = new StringBuilder();
        sb.Append($"<ResourceDictionary xmlns=\"{WinUiNs}\" xmlns:x=\"{XNs}\">\n");
        sb.Append("  <ResourceDictionary.ThemeDictionaries>\n");
        AppendTheme(sb, "Default", Theme.Dark, tokens, systemAccent: true);
        AppendTheme(sb, "Light", Theme.Light, tokens, systemAccent: true);
        AppendTheme(sb, "HighContrast", Theme.HighContrast, tokens);
        sb.Append("  </ResourceDictionary.ThemeDictionaries>\n");
        sb.Append($"  <CornerRadius x:Key=\"ControlCornerRadius\">{Px(Get(root, "radius", "control"), "windows")}</CornerRadius>\n");
        sb.Append($"  <CornerRadius x:Key=\"OverlayCornerRadius\">{Px(Get(root, "radius", "overlay"))}</CornerRadius>\n");
        AppendMetrics(sb, root);
        sb.Append($"  <CornerRadius x:Key=\"YaapicRadiusControl\">{Px(Get(root, "radius", "control"), "windows")}</CornerRadius>\n");
        sb.Append($"  <x:Double x:Key=\"YaapicTitleBarHeight\">{Px(Get(root, "size", "titlebar", "windows"))}</x:Double>\n");
        sb.Append("  <FontFamily x:Key=\"YaapicMonoFont\">ms-appx:///Assets/Fonts/JetBrainsMono.ttf#JetBrains Mono</FontFamily>\n");
        sb.Append($"  <x:Double x:Key=\"YaapicBodyFontSize\">{Px(Get(root, "type", "body")!.Value.GetProperty("$value").GetProperty("fontSize"))}</x:Double>\n");
        sb.Append($"  <x:Double x:Key=\"YaapicCodeFontSize\">{Px(Get(root, "type", "code")!.Value.GetProperty("$value").GetProperty("fontSize"))}</x:Double>\n");
        sb.Append("</ResourceDictionary>\n");
        return sb.ToString();
    }

    /// <summary>Generates the Avalonia resource dictionary.</summary>
    /// <param name="tokensJson">The DTCG token file contents.</param>
    /// <returns>The AXAML of the resource dictionary, with LF line endings.</returns>
    public static string GenerateAvalonia(string tokensJson)
    {
        using var doc = JsonDocument.Parse(tokensJson);
        var root = doc.RootElement;
        var tokens = ReadColorTokens(root, "avalonia");
        var sb = new StringBuilder();
        sb.Append($"<ResourceDictionary xmlns=\"{AvaloniaNs}\" xmlns:x=\"{XNs}\">\n");
        sb.Append("  <ResourceDictionary.ThemeDictionaries>\n");
        AppendTheme(sb, "Dark", Theme.Dark, tokens);
        AppendTheme(sb, "Light", Theme.Light, tokens);
        sb.Append("  </ResourceDictionary.ThemeDictionaries>\n");
        sb.Append("  <!-- Contraste élevé : ThemeVariant personnalisée « HighContrast » déclarée dans App.axaml -->\n");
        sb.Append($"  <CornerRadius x:Key=\"YaapicRadiusControl\">{Px(Get(root, "radius", "control"))}</CornerRadius>\n");
        sb.Append($"  <CornerRadius x:Key=\"YaapicRadiusOverlay\">{Px(Get(root, "radius", "overlay"))}</CornerRadius>\n");
        AppendMetrics(sb, root);
        sb.Append("  <FontFamily x:Key=\"YaapicMonoFont\">avares://Yaapic/Assets/Fonts#JetBrains Mono</FontFamily>\n");
        foreach (var level in new[] { "2", "3" })
        {
            var s = Get(root, "elevation", level)!.Value.GetProperty("$value");
            sb.Append($"  <BoxShadows x:Key=\"YaapicElevation{level}\">{Px(s.GetProperty("offsetX"))} {Px(s.GetProperty("offsetY"))} {Px(s.GetProperty("blur"))} {Px(s.GetProperty("spread"))} {ToXamlColor(s.GetProperty("color").GetString()!)}</BoxShadows>\n");
        }
        sb.Append("</ResourceDictionary>\n");
        return sb.ToString();
    }

    private enum Theme { Dark = 0, Light = 1, HighContrast = 2 }

    private sealed record ColorToken(string Key, string[] Values, string[] Aliases);

    private static void AppendTheme(StringBuilder sb, string key, Theme theme, List<ColorToken> tokens, bool systemAccent = false)
    {
        sb.Append($"    <ResourceDictionary x:Key=\"{key}\">\n");
        foreach (var t in tokens)
        {
            var color = ToXamlColor(t.Values[(int)theme]);
            sb.Append($"      <SolidColorBrush x:Key=\"{t.Key}\" Color=\"{color}\"/>\n");
            // High contrast keeps the OS system colours, so Fluent aliases are not emitted there.
            if (theme != Theme.HighContrast)
                foreach (var alias in t.Aliases)
                    sb.Append($"      <SolidColorBrush x:Key=\"{alias}\" Color=\"{color}\"/>\n");
            sb.Append($"      <Color x:Key=\"{t.Key[..^"Brush".Length]}Color\">{color}</Color>\n");
        }
        if (systemAccent)
            sb.Append($"      <Color x:Key=\"SystemAccentColor\">{ToXamlColor(tokens.Single(t => t.Key == "YaapicAccentFillBrush").Values[(int)theme])}</Color>\n");
        sb.Append("    </ResourceDictionary>\n");
    }

    private static void AppendMetrics(StringBuilder sb, JsonElement root)
    {
        sb.Append($"  <x:Double x:Key=\"YaapicControlHeight\">{Px(Get(root, "size", "control", "comfortable"))}</x:Double>\n");
        sb.Append($"  <x:Double x:Key=\"YaapicRowHeight\">{Px(Get(root, "size", "row", "comfortable"))}</x:Double>\n");
        foreach (var (key, path) in new (string, string[])[]
        {
            ("YaapicControlHeightCompact", ["size", "control", "compact"]),
            ("YaapicControlHeightSpacious", ["size", "control", "spacious"]),
            ("YaapicRowHeightCompact", ["size", "row", "compact"]),
            ("YaapicRowHeightSpacious", ["size", "row", "spacious"]),
            ("YaapicRailWidth", ["size", "rail"]),
            ("YaapicSidebarWidth", ["size", "sidebar", "default"]),
            ("YaapicSidebarMinWidth", ["size", "sidebar", "min"]),
            ("YaapicSidebarMaxWidth", ["size", "sidebar", "max"]),
            ("YaapicStatusBarHeight", ["size", "statusbar"]),
            ("YaapicTabBarHeight", ["size", "tabbar"]),
        })
            sb.Append($"  <x:Double x:Key=\"{key}\">{Px(Get(root, path))}</x:Double>\n");
        foreach (var space in root.GetProperty("space").EnumerateObject().Where(p => !p.Name.StartsWith('$')))
            sb.Append($"  <x:Double x:Key=\"YaapicSpace{space.Name}\">{Px(space.Value)}</x:Double>\n");
        foreach (var name in new[] { "xs", "sm", "md", "lg", "xl", "full" })
            sb.Append($"  <CornerRadius x:Key=\"YaapicRadius{char.ToUpperInvariant(name[0])}{name[1..]}\">{Px(Get(root, "radius", name))}</CornerRadius>\n");
        // Type tokens are keyed "title.lg", "body.sm"... in the file: PascalCase the dotted name.
        foreach (var type in root.GetProperty("type").EnumerateObject().Where(p => !p.Name.StartsWith('$')))
        {
            var name = string.Concat(type.Name.Split('.').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            var value = type.Value.GetProperty("$value");
            sb.Append($"  <x:Double x:Key=\"YaapicFontSize{name}\">{Px(value.GetProperty("fontSize"))}</x:Double>\n");
            sb.Append($"  <x:Double x:Key=\"YaapicLineHeight{name}\">{Px(value.GetProperty("lineHeight"))}</x:Double>\n");
        }
        sb.Append($"  <Thickness x:Key=\"YaapicBorderThickness\">{Px(Get(root, "border", "width", "default"))}</Thickness>\n");
        sb.Append($"  <Thickness x:Key=\"YaapicFocusBorderThickness\">{Px(Get(root, "border", "width", "focus"))}</Thickness>\n");
    }

    // Colour tokens are the "color" subtree in file order; a token may carry nested tokens (e.g. info.background).
    private static List<ColorToken> ReadColorTokens(JsonElement root, string platform)
    {
        var result = new List<ColorToken>();
        Collect(root.GetProperty("color"));
        return result;

        void Collect(JsonElement node)
        {
            if (node.TryGetProperty("$value", out var value))
            {
                var ext = node.GetProperty("$extensions");
                var themes = ext.GetProperty(ThemesKey);
                var xaml = ext.GetProperty(XamlKey);
                var dark = value.GetString()!;
                string[] aliases = platform == "winui" && xaml.TryGetProperty("winuiAliases", out var list)
                    ? [.. list.EnumerateArray().Select(a => a.GetString()!)]
                    : [];
                result.Add(new ColorToken(xaml.GetProperty(platform).GetString()!,
                    [dark, themes.GetProperty("light").GetString()!, themes.GetProperty("highContrast").GetString()!], aliases));
            }
            foreach (var child in node.EnumerateObject().Where(p => !p.Name.StartsWith('$')))
                Collect(child.Value);
        }
    }

    private static JsonElement? Get(JsonElement root, params string[] path)
    {
        var node = root;
        foreach (var segment in path)
            node = node.GetProperty(segment);
        return node;
    }

    // "6px" -> "6"; a token with a per-platform override (windows, macos, linux) uses it when asked.
    private static string Px(JsonElement? token, string? platform = null)
    {
        var t = token!.Value;
        var raw = t.ValueKind == JsonValueKind.String ? t : t.GetProperty("$value");
        if (platform is not null && t.TryGetProperty("$extensions", out var ext)
            && ext.TryGetProperty("dev.yaapic.platform", out var p) && p.TryGetProperty(platform, out var v))
            raw = v;
        return raw.GetString()!.Replace("px", "", StringComparison.Ordinal);
    }

    // DTCG "#RRGGBB" / "#RRGGBBAA" -> XAML "#AARRGGBB".
    private static string ToXamlColor(string hex) => hex.Length switch
    {
        7 => "#FF" + hex[1..].ToUpperInvariant(),
        9 => "#" + hex[7..9].ToUpperInvariant() + hex[1..7].ToUpperInvariant(),
        _ => throw new FormatException(string.Create(CultureInfo.InvariantCulture, $"Unsupported colour '{hex}'.")),
    };
}
