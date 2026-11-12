namespace Yaapic.Core.Preferences;

/// <summary>The accent tokens of one theme, each as <c>0xRRGGBB</c>.</summary>
/// <param name="Fill">The accent fill (primary buttons, checked boxes, selection).</param>
/// <param name="Hover">The fill under the pointer.</param>
/// <param name="Pressed">The fill while pressed.</param>
/// <param name="Border">The accent border.</param>
/// <param name="Text">Accent coloured text on the theme background.</param>
/// <param name="On">The colour of text and glyphs drawn on the fill.</param>
public readonly record struct AccentTokens(int Fill, int Hover, int Pressed, int Border, int Text, int On);

/// <summary>Turns one accent colour into the accent tokens of the dark and the light theme, keeping text readable.</summary>
public static class AccentPalette
{
    /// <summary>The background text is checked against in the dark theme (the app surface).</summary>
    public const int DarkBackground = 0x131518;

    /// <summary>The background text is checked against in the light theme.</summary>
    public const int LightBackground = 0xFFFFFF;

    /// <summary>The minimum contrast of text.</summary>
    public const double TextContrast = 4.5;

    private static readonly Dictionary<string, int> PresetColors = new()
    {
        ["teal"] = 0x1FC7D4,
        ["blue"] = 0x3B82F6,
        ["purple"] = 0x8B5CF6,
        ["pink"] = 0xEC4899,
        ["red"] = 0xEF4444,
        ["orange"] = 0xF97316,
        ["green"] = 0x22C55E,
        ["grey"] = 0x8A93A3,
    };

    // Teal is the shipped accent: its values are the ones of the design token file, not derived.
    private static readonly AccentTokens TealDark = new(0x1FC7D4, 0x5FE0EA, 0x18B3BF, 0x1FC7D4, 0x5FE0EA, 0x04252A);
    private static readonly AccentTokens TealLight = new(0x1FC7D4, 0x5FE0EA, 0x18B3BF, 0x0A7C86, 0x0A6B74, 0x04252A);

    /// <summary>Gets the base colour of a preset.</summary>
    /// <param name="id">A preset id from <see cref="AccentChoice.Presets"/>.</param>
    /// <returns>The colour as <c>0xRRGGBB</c>.</returns>
    /// <exception cref="ArgumentException">The id is not a preset.</exception>
    public static int PresetColor(string id) =>
        PresetColors.TryGetValue(id, out var rgb) ? rgb : throw new ArgumentException($"Unknown accent preset '{id}'.", nameof(id));

    /// <summary>Gets the tokens of an accent choice.</summary>
    /// <param name="choice">A preset id or a custom <c>#RRGGBB</c>.</param>
    /// <param name="dark"><see langword="true"/> for the dark theme.</param>
    /// <returns>The tokens, or <see langword="null"/> for <see cref="AccentChoice.System"/> (the platform supplies its own) and invalid values.</returns>
    public static AccentTokens? ForChoice(string? choice, bool dark)
    {
        if (choice == "teal")
        {
            return dark ? TealDark : TealLight;
        }

        if (choice is not null && PresetColors.TryGetValue(choice, out var preset))
        {
            return Derive(preset, dark);
        }

        return AccentChoice.TryParseCustom(choice, out var custom) ? Derive(custom, dark) : null;
    }

    /// <summary>Derives the tokens of one colour.</summary>
    /// <param name="rgb">The accent as <c>0xRRGGBB</c>.</param>
    /// <param name="dark"><see langword="true"/> for the dark theme.</param>
    /// <returns>The tokens.</returns>
    public static AccentTokens Derive(int rgb, bool dark)
    {
        var (h, s, l) = ToHsl(rgb);
        var on = PickOn(rgb, h, s);
        var onIsDark = Luminance(on) < 0.5;

        // Under a dark label the states lighten then darken (like the teal); under a white label both darken, so the label stays readable.
        var hover = onIsDark ? FromHsl(h, Math.Min(s + 0.04, 1), Math.Min(l + 0.15, 0.88)) : FromHsl(h, s, Math.Max(l - 0.06, 0.05));
        var pressed = onIsDark ? FromHsl(h, Math.Min(s + 0.03, 1), Math.Max(l - 0.07, 0.05)) : FromHsl(h, s, Math.Max(l - 0.12, 0.05));

        int text;
        int border;
        if (dark)
        {
            text = Reach(hover, DarkBackground, TextContrast, lighter: true);
            border = rgb;
        }
        else
        {
            text = Reach(rgb, LightBackground, TextContrast, lighter: false);
            border = Reach(rgb, LightBackground, 3.0, lighter: false);
        }

        return new AccentTokens(rgb, hover, pressed, border, text, on);
    }

    /// <summary>Gets the WCAG contrast ratio of two colours.</summary>
    /// <param name="a">The first colour as <c>0xRRGGBB</c>.</param>
    /// <param name="b">The second colour as <c>0xRRGGBB</c>.</param>
    /// <returns>A ratio from 1 to 21.</returns>
    public static double Contrast(int a, int b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    // A tinted near-black when it reads on the fill, else white, else whichever of pure black and white contrasts more.
    private static int PickOn(int fill, double h, double s)
    {
        var tinted = FromHsl(h, Math.Min(s, 0.85), 0.09);
        if (Contrast(tinted, fill) >= TextContrast)
        {
            return tinted;
        }

        return Contrast(0xFFFFFF, fill) >= Contrast(0x000000, fill) ? 0xFFFFFF : 0x000000;
    }

    // Moves the lightness of a colour, one percent at a time, until it reaches the contrast against a background.
    private static int Reach(int start, int background, double ratio, bool lighter)
    {
        var (h, s, l) = ToHsl(start);
        var rgb = start;
        for (var i = 0; i < 100 && Contrast(rgb, background) < ratio; i++)
        {
            l = lighter ? Math.Min(l + 0.01, 1) : Math.Max(l - 0.01, 0);
            rgb = FromHsl(h, s, l);
        }

        return rgb;
    }

    private static double Luminance(int rgb)
    {
        static double Channel(int v)
        {
            var c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel((rgb >> 16) & 0xFF)) + (0.7152 * Channel((rgb >> 8) & 0xFF)) + (0.0722 * Channel(rgb & 0xFF));
    }

    private static (double H, double S, double L) ToHsl(int rgb)
    {
        double r = ((rgb >> 16) & 0xFF) / 255.0, g = ((rgb >> 8) & 0xFF) / 255.0, b = (rgb & 0xFF) / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        if (max == min)
        {
            return (0, 0, l);
        }

        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h;
        if (max == r)
        {
            h = ((g - b) / d) + (g < b ? 6 : 0);
        }
        else if (max == g)
        {
            h = ((b - r) / d) + 2;
        }
        else
        {
            h = ((r - g) / d) + 4;
        }

        return (h / 6, s, l);
    }

    private static int FromHsl(double h, double s, double l)
    {
        static double Hue(double p, double q, double t)
        {
            if (t < 0)
            {
                t += 1;
            }

            if (t > 1)
            {
                t -= 1;
            }

            return t < 1.0 / 6 ? p + ((q - p) * 6 * t) : t < 0.5 ? q : t < 2.0 / 3 ? p + ((q - p) * ((2.0 / 3) - t) * 6) : p;
        }

        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - (l * s);
            var p = (2 * l) - q;
            r = Hue(p, q, h + (1.0 / 3));
            g = Hue(p, q, h);
            b = Hue(p, q, h - (1.0 / 3));
        }

        static int To(double v) => (int)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return (To(r) << 16) | (To(g) << 8) | To(b);
    }
}
