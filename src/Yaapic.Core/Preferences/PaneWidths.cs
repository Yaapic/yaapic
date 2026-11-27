namespace Yaapic.Core.Preferences;

/// <summary>The resizable panes whose width is remembered.</summary>
public enum PaneId
{
    /// <summary>The main window sidebar (requests, collections and history).</summary>
    Sidebar,

    /// <summary>The request list of the Runner setup pane.</summary>
    RunnerList,

    /// <summary>The request list of the Load test setup pane.</summary>
    LoadTestList,
}

/// <summary>The width limits of the resizable panes, in device-independent units.</summary>
public static class PaneLimits
{
    /// <summary>Gets the narrowest width that keeps the pane readable.</summary>
    /// <param name="pane">The pane.</param>
    /// <returns>The minimum width.</returns>
    public static double Min(PaneId pane) => pane == PaneId.Sidebar ? 200 : 280;

    /// <summary>Gets the widest width that still leaves the editor or results a usable width at the 900 wide minimum window.</summary>
    /// <param name="pane">The pane.</param>
    /// <returns>The maximum width.</returns>
    public static double Max(PaneId pane) => pane == PaneId.Sidebar ? 440 : 520;

    /// <summary>Gets the width used when nothing is stored.</summary>
    /// <param name="pane">The pane.</param>
    /// <returns>The default width.</returns>
    public static double Default(PaneId pane) => pane == PaneId.Sidebar ? 272 : 360;

    /// <summary>Brings a width into the limits of a pane.</summary>
    /// <param name="pane">The pane.</param>
    /// <param name="width">The width; a missing, NaN or infinite value gives the default.</param>
    /// <returns>The width, clamped between <see cref="Min"/> and <see cref="Max"/>.</returns>
    public static double Clamp(PaneId pane, double? width) =>
        width is { } w && double.IsFinite(w) ? Math.Clamp(w, Min(pane), Max(pane)) : Default(pane);
}

/// <summary>The remembered pane widths; every value is within <see cref="PaneLimits"/> when built with <see cref="From"/> or <see cref="With"/>.</summary>
public sealed record PaneWidths
{
    /// <summary>Gets the widths of a user who never resized a pane.</summary>
    public static PaneWidths Default { get; } = new();

    /// <summary>Gets the main window sidebar width.</summary>
    public double Sidebar { get; init; } = PaneLimits.Default(PaneId.Sidebar);

    /// <summary>Gets the Runner request list width.</summary>
    public double RunnerList { get; init; } = PaneLimits.Default(PaneId.RunnerList);

    /// <summary>Gets the Load test request list width.</summary>
    public double LoadTestList { get; init; } = PaneLimits.Default(PaneId.LoadTestList);

    /// <summary>Creates widths from raw stored values, clamping each one.</summary>
    /// <param name="sidebar">The stored sidebar width, if any.</param>
    /// <param name="runnerList">The stored Runner list width, if any.</param>
    /// <param name="loadTestList">The stored Load test list width, if any.</param>
    /// <returns>The widths, with a default for every missing or invalid value.</returns>
    public static PaneWidths From(double? sidebar, double? runnerList, double? loadTestList) => new()
    {
        Sidebar = PaneLimits.Clamp(PaneId.Sidebar, sidebar),
        RunnerList = PaneLimits.Clamp(PaneId.RunnerList, runnerList),
        LoadTestList = PaneLimits.Clamp(PaneId.LoadTestList, loadTestList),
    };

    /// <summary>Gets the width of a pane.</summary>
    /// <param name="pane">The pane.</param>
    /// <returns>The width.</returns>
    public double Get(PaneId pane) => pane switch
    {
        PaneId.Sidebar => Sidebar,
        PaneId.RunnerList => RunnerList,
        _ => LoadTestList,
    };

    /// <summary>Returns a copy with one width changed.</summary>
    /// <param name="pane">The pane.</param>
    /// <param name="width">The new width, clamped to the limits of the pane.</param>
    /// <returns>The new widths.</returns>
    public PaneWidths With(PaneId pane, double width)
    {
        var clamped = PaneLimits.Clamp(pane, width);
        return pane switch
        {
            PaneId.Sidebar => this with { Sidebar = clamped },
            PaneId.RunnerList => this with { RunnerList = clamped },
            _ => this with { LoadTestList = clamped },
        };
    }
}

/// <summary>Remembers pane widths: serves the loaded values and saves a change into the preferences, keeping every other preference.</summary>
/// <param name="store">The preferences store.</param>
/// <param name="initial">The widths loaded at start-up.</param>
public sealed class PaneLayoutSettings(IPreferencesStore store, PaneWidths initial)
{
    private PaneWidths _widths = initial;

    /// <summary>Gets the current width of a pane.</summary>
    /// <param name="pane">The pane.</param>
    /// <returns>The width, within the limits of the pane.</returns>
    public double Get(PaneId pane) => _widths.Get(pane);

    /// <summary>Remembers a width and saves it; a storage failure is ignored, the layout then only lasts for this run.</summary>
    /// <param name="pane">The pane.</param>
    /// <param name="width">The width, clamped to the limits of the pane.</param>
    /// <returns>A task completing when the width is saved.</returns>
    public async Task SetAsync(PaneId pane, double width)
    {
        _widths = _widths.With(pane, width);
        try
        {
            var current = await store.LoadAsync(CancellationToken.None);
            await store.SaveAsync(current with { Panes = current.Panes.With(pane, _widths.Get(pane)) }, CancellationToken.None);
        }
        catch (Storage.StorageException)
        {
            // Not remembered after a restart; the pane keeps its width meanwhile.
        }
    }
}
