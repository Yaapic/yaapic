using Yaapic.Localization;
using Yaapic.ViewModels.Environments;

namespace Yaapic.ViewModels.Palette;

/// <summary>Offers switching the active environment.</summary>
public sealed class EnvironmentsPaletteSource : IPaletteSource
{
    private static string Category => Strings.PaletteCategoryEnvironment;

    private readonly EnvironmentsViewModel _environments;

    /// <summary>Creates the source.</summary>
    /// <param name="environments">The environments to switch between.</param>
    public EnvironmentsPaletteSource(EnvironmentsViewModel environments)
    {
        ArgumentNullException.ThrowIfNull(environments);
        _environments = environments;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PaletteItem> GetItems()
    {
        var active = _environments.ActiveEnvironment;
        List<PaletteItem> items = [Item("environment:none", Strings.PaletteNoEnvironment, active is null, null)];
        foreach (var environment in _environments.GetAvailable())
        {
            items.Add(Item($"environment:{environment.Id}", Strings.PaletteUseEnvironment(environment.Name), active == environment, environment));
        }

        return items;
    }

    private PaletteItem Item(string id, string title, bool isActive, EnvironmentItemViewModel? target) =>
        new(id, title, isActive ? Strings.PaletteActive : null, Category, null, () =>
        {
            _environments.ActiveEnvironment = target;
            return Task.CompletedTask;
        });
}
