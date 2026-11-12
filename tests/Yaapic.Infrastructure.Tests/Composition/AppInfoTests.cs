using Yaapic.Composition;

namespace Yaapic.Infrastructure.Tests.Composition;

public sealed class AppInfoTests
{
    [Fact]
    public void The_version_comes_from_the_assembly_informational_version()
    {
        var assembly = typeof(AppComposition).Assembly;
        var info = AppComposition.CreateAppInfo(assembly);
        var numeric = assembly.GetName().Version!;

        // Local builds are 0.0.0-dev; the release workflow passes -p:YaapicVersion=<tag>. Either way the numbers agree.
        Assert.Equal((numeric.Major, numeric.Minor, numeric.Build), (info.Version.Major, info.Version.Minor, info.Version.Patch));
    }
}
