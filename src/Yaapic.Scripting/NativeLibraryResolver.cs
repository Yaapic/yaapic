using System.Reflection;
using System.Runtime.InteropServices;

namespace Yaapic.Scripting;

/// <summary>Finds the native script engine libraries next to the app or under <c>runtimes/&lt;rid&gt;/native</c>.</summary>
internal static class NativeLibraryResolver
{
    private static int registered;

    /// <summary>Registers the resolver for this assembly once; later calls do nothing.</summary>
    internal static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref registered, 1) == 0)
        {
            NativeLibrary.SetDllImportResolver(typeof(NativeLibraryResolver).Assembly, Resolve);
        }
    }

    /// <summary>Maps a logical library name (<c>quickjs-ng</c>, <c>lua54</c>) to the platform file name.</summary>
    internal static string FileName(string name) =>
        OperatingSystem.IsWindows() ? $"{name}.dll"
        : OperatingSystem.IsMacOS() ? $"lib{name}.dylib"
        : $"lib{name}.so";

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        var file = FileName(name);
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, file),
            Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", file),
        ];
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        // Not found here: default probing runs and ends in a DllNotFoundException that the engine reports as Unavailable.
        return 0;
    }
}
