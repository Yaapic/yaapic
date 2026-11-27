using Yaapic.Core.Preferences;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public sealed class PaneWidthsTests
{
    public static TheoryData<PaneId> Panes => [PaneId.Sidebar, PaneId.RunnerList, PaneId.LoadTestList];

    [Theory]
    [MemberData(nameof(Panes))]
    public void The_default_is_inside_the_limits(PaneId pane)
    {
        Assert.InRange(PaneLimits.Default(pane), PaneLimits.Min(pane), PaneLimits.Max(pane));
        Assert.Equal(PaneLimits.Default(pane), PaneWidths.Default.Get(pane));
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void A_width_is_clamped_and_a_bad_value_gives_the_default(PaneId pane)
    {
        Assert.Equal(PaneLimits.Min(pane), PaneLimits.Clamp(pane, -50));
        Assert.Equal(PaneLimits.Max(pane), PaneLimits.Clamp(pane, 1e9));
        Assert.Equal(PaneLimits.Max(pane), PaneLimits.Clamp(pane, double.MaxValue));
        Assert.Equal(PaneLimits.Default(pane), PaneLimits.Clamp(pane, double.NaN));
        Assert.Equal(PaneLimits.Default(pane), PaneLimits.Clamp(pane, double.PositiveInfinity));
        Assert.Equal(PaneLimits.Default(pane), PaneLimits.Clamp(pane, null));
        Assert.Equal(PaneLimits.Min(pane) + 10, PaneLimits.Clamp(pane, PaneLimits.Min(pane) + 10));
    }

    [Fact]
    public void The_sidebar_limits_keep_the_tree_readable_and_the_editor_usable_at_the_minimum_window()
    {
        const double window = 900, rail = 48, minEditor = 400;

        Assert.True(PaneLimits.Min(PaneId.Sidebar) >= 200);
        Assert.True(window - rail - PaneLimits.Max(PaneId.Sidebar) >= minEditor);
    }

    [Fact]
    public void From_clamps_every_value_independently()
    {
        var widths = PaneWidths.From(5000, null, 300);

        Assert.Equal(PaneLimits.Max(PaneId.Sidebar), widths.Sidebar);
        Assert.Equal(PaneLimits.Default(PaneId.RunnerList), widths.RunnerList);
        Assert.Equal(300, widths.LoadTestList);
    }

    [Fact]
    public void With_changes_one_pane_only()
    {
        var widths = PaneWidths.Default.With(PaneId.RunnerList, 400);

        Assert.Equal(400, widths.RunnerList);
        Assert.Equal(PaneWidths.Default.Sidebar, widths.Sidebar);
        Assert.Equal(PaneWidths.Default.LoadTestList, widths.LoadTestList);
    }

    [Fact]
    public async Task Setting_a_width_saves_it_clamped_and_keeps_the_other_preferences()
    {
        var store = new InMemoryPreferencesStore { Preferences = new UserPreferences(AppLanguage.French, "blue", "python") };
        var settings = new PaneLayoutSettings(store, store.Preferences.Panes);

        await settings.SetAsync(PaneId.Sidebar, 350);
        await settings.SetAsync(PaneId.RunnerList, 99999);

        Assert.Equal(350, settings.Get(PaneId.Sidebar));
        Assert.Equal(PaneLimits.Max(PaneId.RunnerList), settings.Get(PaneId.RunnerList));
        Assert.Equal(350, store.Preferences.Panes.Sidebar);
        Assert.Equal(PaneLimits.Max(PaneId.RunnerList), store.Preferences.Panes.RunnerList);
        Assert.Equal(AppLanguage.French, store.Preferences.Language);
        Assert.Equal("python", store.Preferences.CodegenTarget);
    }

    [Fact]
    public async Task A_save_does_not_overwrite_preferences_changed_elsewhere_meanwhile()
    {
        var store = new InMemoryPreferencesStore();
        var settings = new PaneLayoutSettings(store, PaneWidths.Default);
        store.Preferences = store.Preferences with { Accent = "pink" };

        await settings.SetAsync(PaneId.LoadTestList, 400);

        Assert.Equal("pink", store.Preferences.Accent);
        Assert.Equal(400, store.Preferences.Panes.LoadTestList);
    }
}
