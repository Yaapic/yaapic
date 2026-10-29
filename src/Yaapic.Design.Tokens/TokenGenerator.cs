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
        sb.Append("  <FontFamily x:Key=\"YaapicMonoFont\">ms-appx:///Assets/Fonts/JetBrainsMono.ttf#JetBrains Mono</FontFamily>\n");
        sb.Append($"  <x:Double x:Key=\"YaapicBodyFontSize\">{Px(Get(root, "type", "body")!.Value.GetProperty("$value").GetProperty("fontSize"))}</x:Double>\n");
        sb.Append($"  <x:Double x:Key=\"YaapicCodeFontSize\">{Px(Get(root, "type", "code")!.Value.GetProperty("$value").GetProperty("fontSize"))}</x:Double>\n");
        sb.Append("</ResourceDictionary>\n");
        return sb.ToString();
    }

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

    private sealed record ColorToken(string Key, string[] Values);

    private static void AppendTheme(StringBuilder sb, string key, Theme theme, List<ColorToken> tokens, bool systemAccent = false)
    {
        sb.Append($"    <ResourceDictionary x:Key=\"{key}\">\n");
        foreach (var t in tokens)
            sb.Append($"      <SolidColorBrush x:Key=\"{t.Key}\" Color=\"{ToXamlColor(t.Values[(int)theme])}\"/>\n");
        if (systemAccent)
            sb.Append($"      <Color x:Key=\"SystemAccentColor\">{ToXamlColor(tokens.Single(t => t.Key == "YaapicAccentFillBrush").Values[(int)theme])}</Color>\n");
        sb.Append("    </ResourceDictionary>\n");
    }

    private static void AppendMetrics(StringBuilder sb, JsonElement root)
    {
        sb.Append($"  <x:Double x:Key=\"YaapicControlHeight\">{Px(Get(root, "size", "control", "comfortable"))}</x:Double>\n");
        sb.Append($"  <x:Double x:Key=\"YaapicRowHeight\">{Px(Get(root, "size", "row", "comfortable"))}</x:Double>\n");
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
                var dark = value.GetString()!;
                result.Add(new ColorToken(ext.GetProperty(XamlKey).GetProperty(platform).GetString()!,
                    [dark, themes.GetProperty("light").GetString()!, themes.GetProperty("highContrast").GetString()!]));
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
