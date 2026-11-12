using System.Globalization;
using Yaapic.Core.Preferences;
using Yaapic.Localization;

namespace Yaapic.Core.Tests;

public sealed class LanguageResolverTests
{
    [Theory]
    [InlineData(AppLanguage.System, "fr-FR", UiLanguage.French)]
    [InlineData(AppLanguage.System, "de-DE", UiLanguage.English)]
    [InlineData(AppLanguage.French, "en-US", UiLanguage.French)]
    [InlineData(AppLanguage.English, "fr-FR", UiLanguage.English)]
    public void The_preference_wins_over_the_system(AppLanguage choice, string system, UiLanguage expected) =>
        Assert.Equal(expected, LanguageResolver.Resolve(choice, CultureInfo.GetCultureInfo(system)));
}
