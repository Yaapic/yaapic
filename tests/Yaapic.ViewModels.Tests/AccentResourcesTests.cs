using Yaapic.Core.Preferences;
using Yaapic.ViewModels.Theming;

namespace Yaapic.ViewModels.Tests;

public sealed class AccentResourcesTests
{
    private static readonly AccentTokens Tokens = new(0x111111, 0x222222, 0x333333, 0x444444, 0x555555, 0x666666);

    [Fact]
    public void Each_role_reaches_its_keys_on_WinUI()
    {
        var entries = AccentResources.Entries(winUi: true, Tokens);

        Assert.Contains(new AccentResource("AccentButtonBackground", 0x111111, true), entries);
        Assert.Contains(new AccentResource("YaapicAccentFillHoverColor", 0x222222, false), entries);
        Assert.Contains(new AccentResource("SystemAccentColor", 0x111111, false), entries);
        Assert.Contains(new AccentResource("TextOnAccentFillColorPrimaryBrush", 0x666666, true), entries);
    }

    [Fact]
    public void Avalonia_also_gets_the_shades_of_the_system_accent()
    {
        var entries = AccentResources.Entries(winUi: false, Tokens);

        Assert.Contains(new AccentResource("SystemAccentColorLight2", 0x222222, false), entries);
        Assert.Contains(new AccentResource("SystemAccentColorDark3", 0x333333, false), entries);
        Assert.DoesNotContain(entries, e => e.Key == "TextOnAccentFillColorPrimaryBrush");
    }

    [Fact]
    public void No_key_is_set_to_two_colours()
    {
        foreach (var winUi in new[] { true, false })
        {
            var entries = AccentResources.Entries(winUi, Tokens);

            Assert.Equal(entries.Count, entries.Select(e => (e.Key, e.IsBrush)).Distinct().Count());
        }
    }
}
