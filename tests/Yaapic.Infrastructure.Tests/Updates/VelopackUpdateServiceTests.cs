using Velopack;
using Velopack.Locators;
using Velopack.Sources;
using Yaapic.Core.Updates;
using Yaapic.Infrastructure.Updates;

namespace Yaapic.Infrastructure.Tests.Updates;

/// <summary>The adapter against a folder feed and a fake install (Velopack's test locator): no network, no installed app.</summary>
public sealed class VelopackUpdateServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-vpk-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private VelopackUpdateService Service(string installed, string? feedVersion, string? feedDirectory = null)
    {
        var feed = feedDirectory ?? Path.Combine(_root, "feed");
        Directory.CreateDirectory(feed);
        if (feedVersion is not null)
        {
            // Velopack reads releases.<channel>.json, the channel being the OS name by default.
            var json = $$"""{"Assets":[{"PackageId":"Yaapic","Version":"{{feedVersion}}","Type":"Full","FileName":"Yaapic-{{feedVersion}}-full.nupkg","SHA1":"0","SHA256":"0","Size":1,"NotesMarkdown":"New things","NotesHtml":""}]}""";
            foreach (var os in new[] { "win", "osx", "linux" })
            {
                File.WriteAllText(Path.Combine(feed, $"releases.{os}.json"), json);
            }
        }

        var packages = Path.Combine(_root, "packages");
        Directory.CreateDirectory(packages);
        return new VelopackUpdateService(_ => new UpdateManager(new SimpleFileSource(new DirectoryInfo(feed)), null, new TestVelopackLocator("Yaapic", installed, packages)));
    }

    [Fact]
    public void A_fake_install_is_supported() => Assert.True(Service("1.0.0", null).IsSupported);

    [Fact]
    public async Task A_newer_release_is_available_with_its_notes()
    {
        var result = await Service("1.0.0", "2.0.0").CheckAsync(AppChannel.Stable, TestContext.Current.CancellationToken);

        var available = Assert.IsType<UpdateCheckResult.Available>(result);
        Assert.Equal(AppVersion.Parse("2.0.0"), available.Update.Version);
        Assert.Equal("New things", available.Update.Notes);
    }

    [Fact]
    public async Task The_newest_version_is_up_to_date() =>
        Assert.IsType<UpdateCheckResult.UpToDate>(await Service("2.0.0", "2.0.0").CheckAsync(AppChannel.Stable, TestContext.Current.CancellationToken));

    [Fact]
    public async Task A_malformed_feed_is_a_failure_not_an_exception()
    {
        var service = Service("1.0.0", null);
        foreach (var os in new[] { "win", "osx", "linux" })
        {
            await File.WriteAllTextAsync(Path.Combine(_root, "feed", $"releases.{os}.json"), "{ not json", TestContext.Current.CancellationToken);
        }

        var result = await service.CheckAsync(AppChannel.Stable, TestContext.Current.CancellationToken);

        Assert.IsType<UpdateCheckResult.Failed>(result);
    }

    [Fact]
    public async Task A_build_that_is_not_installed_is_unsupported()
    {
        var service = new VelopackUpdateService();

        Assert.False(service.IsSupported);
        Assert.IsType<UpdateCheckResult.Failed>(await service.CheckAsync(AppChannel.Preview, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Download_and_apply_need_a_check_first()
    {
        var service = Service("1.0.0", "2.0.0");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadAsync(new Core.Updates.UpdateInfo(AppVersion.Parse("2.0.0")), new Progress<int>(), TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(service.ApplyOnRestart);
        Assert.Throws<InvalidOperationException>(service.RestartAndApply);
    }
}
