using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Yaapic.Infrastructure.Auth;

namespace Yaapic.Infrastructure.Tests.Auth;

public sealed class SystemBrowserLauncherTests
{
    private static async Task<ProcessStartInfo> CaptureAsync(OSPlatform platform, string url)
    {
        ProcessStartInfo? seen = null;
        await new SystemBrowserLauncher(platform, p => seen = p).OpenAsync(new Uri(url), CancellationToken.None);
        return seen!;
    }

    [Fact]
    public async Task Windows_UsesShellExecuteWithTheUrl()
    {
        var info = await CaptureAsync(OSPlatform.Windows, "https://example.com/a?b=1");
        Assert.True(info.UseShellExecute);
        Assert.Equal("https://example.com/a?b=1", info.FileName);
    }

    [Theory]
    [InlineData("OSX", "open")]
    [InlineData("LINUX", "xdg-open")]
    public async Task Unix_UsesTheOpenerWithOneArgument(string os, string exe)
    {
        var info = await CaptureAsync(OSPlatform.Create(os), "http://example.com/x");
        Assert.False(info.UseShellExecute);
        Assert.Equal(exe, info.FileName);
        Assert.Equal(["http://example.com/x"], info.ArgumentList);
    }

    [Fact]
    public async Task SpacesAndQuotes_StayOneArgument()
    {
        var info = await CaptureAsync(OSPlatform.Linux, "https://example.com/a b?q=\"x\" --evil");
        Assert.Single(info.ArgumentList);
        Assert.DoesNotContain(' ', info.ArgumentList[0]);
        Assert.DoesNotContain('"', info.ArgumentList[0]);
    }

    [Theory]
    [InlineData("file:///c:/x")]
    [InlineData("ftp://example.com/x")]
    [InlineData("javascript:alert(1)")]
    public async Task OtherSchemes_AreRejected(string url)
    {
        var launcher = new SystemBrowserLauncher(OSPlatform.Windows, _ => Assert.Fail("must not start"));
        await Assert.ThrowsAsync<ArgumentException>(() => launcher.OpenAsync(new Uri(url), CancellationToken.None));
    }

    [Fact]
    public async Task RelativeUri_IsRejected()
    {
        var launcher = new SystemBrowserLauncher(OSPlatform.Windows, _ => Assert.Fail("must not start"));
        await Assert.ThrowsAsync<ArgumentException>(() => launcher.OpenAsync(new Uri("/x", UriKind.Relative), CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartFailure_BecomesGenericInvalidOperation(bool win32)
    {
        var launcher = new SystemBrowserLauncher(OSPlatform.Linux, _ => throw (win32 ? new Win32Exception("x") : new InvalidOperationException("x")));
        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => launcher.OpenAsync(new Uri("https://example.com/?state=secret"), CancellationToken.None));
        Assert.DoesNotContain("secret", e.Message);
    }
}
