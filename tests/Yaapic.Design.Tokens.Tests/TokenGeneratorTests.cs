using System.Text;
using Yaapic.Design.Tokens;

namespace Yaapic.Design.Tokens.Tests;

public class TokenGeneratorTests
{
    private static readonly string DesignDir = FindDesignDir();

    private static string FindDesignDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Yaapic.sln")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Yaapic.sln not found"), "design");
    }

    // The reference files are checked out with the platform's line endings; the generator emits LF.
    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([DesignDir, .. path])).Replace("\r\n", "\n");

    private static string Tokens() => Read("tokens", "yaapic.tokens.json");

    // YAAPIC_UPDATE_SNAPSHOTS=1 rewrites the reference file (LF endings) instead of asserting.
    private static void AssertMatchesReference(string actual, params string[] path)
    {
        if (Environment.GetEnvironmentVariable("YAAPIC_UPDATE_SNAPSHOTS") == "1")
            File.WriteAllText(Path.Combine([DesignDir, .. path]), actual, new UTF8Encoding(false));
        else
            Assert.Equal(Read(path), actual);
    }

    [Fact]
    public void WinUI_output_matches_reference() =>
        AssertMatchesReference(TokenGenerator.GenerateWinUI(Tokens()), "xaml", "Yaapic.Colors.WinUI.xaml");

    [Fact]
    public void Avalonia_output_matches_reference() =>
        AssertMatchesReference(TokenGenerator.GenerateAvalonia(Tokens()), "xaml", "Yaapic.Colors.Avalonia.axaml");

    [Fact]
    public void Changing_a_token_changes_the_output_and_breaks_the_snapshot()
    {
        var changed = Tokens().Replace("#1FC7D4", "#FF00FF", StringComparison.Ordinal);

        var winUi = TokenGenerator.GenerateWinUI(changed);
        var avalonia = TokenGenerator.GenerateAvalonia(changed);

        Assert.Contains("#FFFF00FF", winUi);
        Assert.NotEqual(Read("xaml", "Yaapic.Colors.WinUI.xaml"), winUi);
        Assert.NotEqual(Read("xaml", "Yaapic.Colors.Avalonia.axaml"), avalonia);
    }

    [Theory]
    [InlineData("#1FC7D4", "#FF1FC7D4")]
    [InlineData("#1FC7D41A", "#1A1FC7D4")]
    public void Colours_are_converted_to_argb(string dtcg, string argb)
    {
        var json = Tokens().Replace("#131518", dtcg, StringComparison.Ordinal);
        Assert.Contains($"YaapicSurfaceAppBrush\" Color=\"{argb}\"", TokenGenerator.GenerateWinUI(json));
    }

    // The text of one theme dictionary ("Default", "Light", "HighContrast") of the WinUI output.
    private static string WinUiTheme(string key)
    {
        var xaml = TokenGenerator.GenerateWinUI(Tokens());
        var start = xaml.IndexOf($"<ResourceDictionary x:Key=\"{key}\">", StringComparison.Ordinal);
        return xaml[start..xaml.IndexOf("</ResourceDictionary>", start, StringComparison.Ordinal)];
    }

    [Fact]
    public void Fluent_aliases_are_emitted_in_default_and_light_but_not_high_contrast()
    {
        const string alias = "TextFillColorPrimaryBrush";

        Assert.Contains($"x:Key=\"{alias}\" Color=\"#FFECEEF1\"", WinUiTheme("Default"));
        Assert.Contains($"x:Key=\"{alias}\" Color=\"#FF16191D\"", WinUiTheme("Light"));
        Assert.DoesNotContain(alias, WinUiTheme("HighContrast"));
        Assert.DoesNotContain(alias, TokenGenerator.GenerateAvalonia(Tokens()));
        Assert.DoesNotContain("winuiAliases", TokenGenerator.GenerateAvalonia(Tokens()));
    }

    [Theory]
    [InlineData("Default", "#FFFF6B6B")]
    [InlineData("Light", "#FFC42B2B")]
    [InlineData("HighContrast", "#FFFF8080")]
    public void Each_colour_token_has_a_colour_resource(string theme, string argb) =>
        Assert.Contains($"<Color x:Key=\"YaapicDangerColor\">{argb}</Color>", WinUiTheme(theme));

    [Fact]
    public void Avalonia_has_colour_resources_in_both_themes()
    {
        var axaml = TokenGenerator.GenerateAvalonia(Tokens());

        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(axaml, "x:Key=\"YaapicDangerColor\""));
    }

    [Theory]
    [InlineData("<x:Double x:Key=\"YaapicSpace300\">12</x:Double>")]
    [InlineData("<CornerRadius x:Key=\"YaapicRadiusControl\">4</CornerRadius>")]
    [InlineData("<CornerRadius x:Key=\"YaapicRadiusFull\">999</CornerRadius>")]
    [InlineData("<x:Double x:Key=\"YaapicFontSizeTitle\">20</x:Double>")]
    [InlineData("<x:Double x:Key=\"YaapicLineHeightTitle\">28</x:Double>")]
    [InlineData("<x:Double x:Key=\"YaapicFontSizeCodeSm\">11</x:Double>")]
    [InlineData("<x:Double x:Key=\"YaapicSidebarWidth\">272</x:Double>")]
    [InlineData("<x:Double x:Key=\"YaapicTitleBarHeight\">40</x:Double>")]
    [InlineData("<Thickness x:Key=\"YaapicFocusBorderThickness\">2</Thickness>")]
    [InlineData("<x:Double x:Key=\"YaapicBodyFontSize\">13</x:Double>")]
    public void WinUI_metrics_are_generated(string expected) =>
        Assert.Contains(expected, TokenGenerator.GenerateWinUI(Tokens()));

    [Fact]
    public void Avalonia_metrics_are_generated()
    {
        var axaml = TokenGenerator.GenerateAvalonia(Tokens());

        Assert.Contains("<x:Double x:Key=\"YaapicSpace300\">12</x:Double>", axaml);
        Assert.Contains("<CornerRadius x:Key=\"YaapicRadiusControl\">6</CornerRadius>", axaml);
        Assert.DoesNotContain("YaapicTitleBarHeight", axaml);
    }
}
