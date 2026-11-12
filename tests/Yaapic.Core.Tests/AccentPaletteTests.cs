using Yaapic.Core.Preferences;

namespace Yaapic.Core.Tests;

public sealed class AccentPaletteTests
{
    public static TheoryData<string> Presets => [.. AccentChoice.Presets];

    [Fact]
    public void Teal_is_the_shipped_accent_of_the_token_file()
    {
        var dark = AccentPalette.ForChoice("teal", dark: true)!.Value;
        var light = AccentPalette.ForChoice("teal", dark: false)!.Value;

        Assert.Equal(new AccentTokens(0x1FC7D4, 0x5FE0EA, 0x18B3BF, 0x1FC7D4, 0x5FE0EA, 0x04252A), dark);
        Assert.Equal(new AccentTokens(0x1FC7D4, 0x5FE0EA, 0x18B3BF, 0x0A7C86, 0x0A6B74, 0x04252A), light);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void Every_preset_keeps_text_readable_in_both_themes(string id)
    {
        foreach (var dark in new[] { true, false })
        {
            var t = AccentPalette.ForChoice(id, dark)!.Value;
            var background = dark ? AccentPalette.DarkBackground : AccentPalette.LightBackground;

            Assert.True(AccentPalette.Contrast(t.On, t.Fill) >= 4.5, $"{id} dark={dark}: text on the fill");
            Assert.True(AccentPalette.Contrast(t.Text, background) >= 4.5, $"{id} dark={dark}: accent text on the background");
            Assert.True(AccentPalette.Contrast(t.Border, background) >= 3.0 || dark, $"{id} dark={dark}: border");
            Assert.True(AccentPalette.Contrast(t.On, t.Hover) >= 3.0, $"{id} dark={dark}: text on the hover fill");
            Assert.True(AccentPalette.Contrast(t.On, t.Pressed) >= 3.0, $"{id} dark={dark}: text on the pressed fill");
        }
    }

    [Theory]
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    [InlineData("#777777")]
    [InlineData("#FFFF00")]
    [InlineData("#0000FF")]
    [InlineData("#808000")]
    public void Any_custom_colour_gives_readable_tokens(string colour)
    {
        foreach (var dark in new[] { true, false })
        {
            var t = AccentPalette.ForChoice(colour, dark)!.Value;
            var background = dark ? AccentPalette.DarkBackground : AccentPalette.LightBackground;

            Assert.True(AccentPalette.Contrast(t.On, t.Fill) >= 4.5, $"{colour} dark={dark}: text on the fill");
            Assert.True(AccentPalette.Contrast(t.Text, background) >= 4.5, $"{colour} dark={dark}: accent text");
        }
    }

    [Fact]
    public void The_custom_colour_is_the_fill()
    {
        Assert.Equal(0x336699, AccentPalette.ForChoice("#336699", dark: true)!.Value.Fill);
        Assert.Equal(0x336699, AccentPalette.ForChoice("#336699", dark: false)!.Value.Fill);
    }

    [Theory]
    [InlineData("system")]
    [InlineData("chartreuse")]
    [InlineData(null)]
    public void System_and_invalid_choices_have_no_tokens(string? choice) =>
        Assert.Null(AccentPalette.ForChoice(choice, dark: true));

    [Fact]
    public void Every_preset_id_has_a_colour()
    {
        foreach (var id in AccentChoice.Presets)
        {
            Assert.InRange(AccentPalette.PresetColor(id), 0, 0xFFFFFF);
        }

        Assert.Throws<ArgumentException>(() => AccentPalette.PresetColor("nope"));
    }
}
