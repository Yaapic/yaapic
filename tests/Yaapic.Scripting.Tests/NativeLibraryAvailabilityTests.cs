using System.Runtime.InteropServices;

namespace Yaapic.Scripting.Tests;

/// <summary>Proves that the native engine libraries built by <c>native/Build-Natives.ps1</c> land next to the test host and load.</summary>
public sealed class NativeLibraryAvailabilityTests
{
    private static string FileName(string name) =>
        OperatingSystem.IsWindows() ? $"{name}.dll"
        : OperatingSystem.IsMacOS() ? $"lib{name}.dylib"
        : $"lib{name}.so";

    [Theory]
    [InlineData("quickjs-ng", "JS_NewRuntime")]
    [InlineData("lua54", "luaL_newstate")]
    public void Library_Loads_AndExportsItsEntryPoint(string library, string symbol)
    {
        var path = Path.Combine(AppContext.BaseDirectory, FileName(library));

        Assert.True(NativeLibrary.TryLoad(path, out var handle), $"Could not load {path}.");
        try
        {
            Assert.True(NativeLibrary.TryGetExport(handle, symbol, out var address));
            Assert.NotEqual(IntPtr.Zero, address);
        }
        finally
        {
            NativeLibrary.Free(handle);
        }
    }

    [Fact]
    public void ThirdPartyNotices_AreCopiedNextToTheHost() =>
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md")));
}
