using System.Globalization;
using Yaapic.Core.Updates;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Updates;

namespace Yaapic.ViewModels.Tests;

/// <summary>Runs alone: the language is process-wide, so no other test may run while French is selected.</summary>
[CollectionDefinition("Localizer", DisableParallelization = true)]
public sealed class LocalizerCollection;

[Collection("Localizer")]
public sealed class FrenchTextTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public FrenchTextTests() => Localizer.Use(UiLanguage.French);

    public void Dispose()
    {
        Localizer.Use(UiLanguage.English);
        CultureInfo.CurrentCulture = _culture;
    }

    [Fact]
    public async Task The_update_banner_is_in_french()
    {
        var service = new FakeUpdateService { Result = new UpdateCheckResult.Available(new(AppVersion.Parse("2.0.0"), "notes")) };
        var vm = new UpdateViewModel(service, new InMemoryUpdateSettingsStore(), new AppInfo(AppVersion.Parse("1.0.0")), new UpdateScheduler(TimeProvider.System), new ConsoleViewModel());

        await vm.InitializeAsync();

        Assert.Equal("La version 2.0.0 est disponible.", vm.BannerText);
    }

    [Fact]
    public void Test_status_and_script_statuses_are_in_french()
    {
        Assert.Equal("Moteur indisponible", Strings.ScriptStatusUnavailable);
        Assert.Equal("Nouvelle requête", Strings.RequestNewName);
        Assert.Equal("Envoyer la requête", Strings.PaletteSendRequest);
    }
}
