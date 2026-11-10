using System.Runtime.InteropServices;

namespace Yaapic.Scripting.Lua;

/// <summary>
/// The <c>yl_*</c> shim exported by <c>native/lua/yaapic_lua.c</c>. Every function is non-raising: the Lua API is never
/// called directly, so a Lua <c>longjmp</c> cannot cross a managed frame.
/// </summary>
internal static unsafe partial class LuaNative
{
    private const string Lib = "lua54";

    static LuaNative() => NativeLibraryResolver.EnsureRegistered();

    [LibraryImport(Lib, EntryPoint = "yl_new")] internal static partial nint New(long memoryLimit);
    [LibraryImport(Lib, EntryPoint = "yl_close")] internal static partial void Close(nint state);
    [LibraryImport(Lib, EntryPoint = "yl_open_libs")] internal static partial int OpenLibs(nint state);
    [LibraryImport(Lib, EntryPoint = "yl_set_host")] internal static partial int SetHost(nint state, nint callback);
    [LibraryImport(Lib, EntryPoint = "yl_set_limits")] internal static partial int SetLimits(nint state, long* deadline, int* cancel);
    [LibraryImport(Lib, EntryPoint = "yl_last_abort")] internal static partial int LastAbort(nint state);
    [LibraryImport(Lib, EntryPoint = "yl_run")] internal static partial int Run(nint state, byte* chunk, nuint length, byte* chunkName, byte** errorMessage, int* errorLine);
    [LibraryImport(Lib, EntryPoint = "yl_free")] internal static partial void Free(nint memory);
    [LibraryImport(Lib, EntryPoint = "yl_alloc")] internal static partial nint Alloc(nuint size);
    [LibraryImport(Lib, EntryPoint = "yl_now_ms")] internal static partial long NowMilliseconds();
}
