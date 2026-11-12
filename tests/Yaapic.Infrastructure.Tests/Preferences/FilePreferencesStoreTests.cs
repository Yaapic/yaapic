using Yaapic.Core.Preferences;
using Yaapic.Infrastructure.Preferences;

namespace Yaapic.Infrastructure.Tests.Preferences;

public sealed class FilePreferencesStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "yaapic-pref-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string File_ => Path.Combine(_dir, "Yaapic", "preferences.json");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preferences_round_trip()
    {
        var preferences = new UserPreferences(AppLanguage.French, "#A1B2C3");

        await new FilePreferencesStore(_dir).SaveAsync(preferences, Ct);

        Assert.Equal(preferences, await new FilePreferencesStore(_dir).LoadAsync(Ct));
    }

    [Fact]
    public async Task A_missing_file_gives_the_defaults() =>
        Assert.Equal(UserPreferences.Default, await new FilePreferencesStore(_dir).LoadAsync(Ct));

    [Fact]
    public async Task A_corrupt_file_gives_the_defaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        await File.WriteAllTextAsync(File_, "{ not json", Ct);

        Assert.Equal(UserPreferences.Default, await new FilePreferencesStore(_dir).LoadAsync(Ct));
    }

    [Fact]
    public async Task Unknown_values_fall_back_to_the_defaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        await File.WriteAllTextAsync(File_, """{"language":"klingon","accent":"chartreuse"}""", Ct);

        Assert.Equal(UserPreferences.Default, await new FilePreferencesStore(_dir).LoadAsync(Ct));
    }

    [Theory]
    [InlineData("teal", true)]
    [InlineData("system", true)]
    [InlineData("#00ff7f", true)]
    [InlineData("#00ff7", false)]
    [InlineData("00ff7f", false)]
    [InlineData("#00gg7f", false)]
    [InlineData("#+0ff7f", false)]
    [InlineData(null, false)]
    public void Accent_choices_are_validated(string? value, bool valid) => Assert.Equal(valid, AccentChoice.IsValid(value));
}
