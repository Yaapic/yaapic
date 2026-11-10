using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Yaapic.Scripting.Lua;

/// <summary>
/// An owned Lua 5.4 state with a memory limit, a restricted standard library, a deadline, a cancel flag and one host
/// function (<c>__host(op, argsJson)</c>). Not thread-safe: use one state per thread.
/// </summary>
internal sealed unsafe class LuaState : IDisposable
{
    private const string TracebackMarker = "\nstack traceback:";

    // The shim host callback has no user data, so the state running on this thread is found here (Run saves and restores it).
    [ThreadStatic]
    private static LuaState? current;

    private readonly long* limits; // [0] deadline in NowMilliseconds units (0 = none), [1] cancel flag
    private nint handle;
    private Func<string, string, string>? host;

    /// <summary>Creates a state with the allowed libraries only.</summary>
    /// <param name="memoryLimit">The heap limit in bytes, 0 for none.</param>
    /// <exception cref="DllNotFoundException">The native library is missing.</exception>
    /// <exception cref="InvalidOperationException">The state could not be created.</exception>
    internal LuaState(long memoryLimit = 0)
    {
        handle = LuaNative.New(memoryLimit);
        if (handle == 0 || LuaNative.OpenLibs(handle) != 0)
        {
            if (handle != 0)
            {
                LuaNative.Close(handle);
                handle = 0;
            }

            throw new InvalidOperationException("Creating the Lua state failed.");
        }

        limits = (long*)NativeMemory.AllocZeroed(2 * sizeof(long));
        LuaNative.SetLimits(handle, limits, (int*)(limits + 1));
    }

    /// <summary>Aborts runs that are still going after <paramref name="timeout"/>.</summary>
    internal void SetTimeout(TimeSpan timeout) =>
        Volatile.Write(ref limits[0], LuaNative.NowMilliseconds() + (long)timeout.TotalMilliseconds);

    /// <summary>Aborts the running script at its next instruction batch. Safe to call from another thread.</summary>
    internal void Cancel() => Volatile.Write(ref *(int*)(limits + 1), 1);

    /// <summary>Clears the deadline and the cancel flag.</summary>
    internal void ResetLimits()
    {
        Volatile.Write(ref limits[0], 0);
        Volatile.Write(ref *(int*)(limits + 1), 0);
    }

    /// <summary>Defines the global <c>__host(op, argsJson)</c>; an exception thrown by <paramref name="handler"/> raises a Lua error.</summary>
    /// <param name="handler">Receives the operation and the arguments and returns the result text.</param>
    internal void DefineHost(Func<string, string, string> handler)
    {
        ObjectDisposedException.ThrowIf(handle == 0, this);
        host = handler;
        LuaNative.SetHost(handle, (nint)(delegate* unmanaged[Cdecl]<byte*, byte*, byte**, int*, int>)&OnHost);
    }

    /// <summary>Compiles and runs a text chunk.</summary>
    /// <param name="source">The Lua source.</param>
    /// <param name="name">The chunk name used in messages.</param>
    /// <returns>Success, or the error with its kind, message and line.</returns>
    internal LuaResult Run(string source, string name)
    {
        ObjectDisposedException.ThrowIf(handle == 0, this);
        var chunk = Encoding.UTF8.GetBytes(source);
        var chunkName = Encoding.UTF8.GetBytes(name + "\0");
        byte* message;
        int line;
        int status;
        var previous = current;
        current = this;
        try
        {
            fixed (byte* chunkPtr = chunk)
            fixed (byte* namePtr = chunkName)
            {
                status = LuaNative.Run(handle, chunkPtr, (nuint)chunk.Length, namePtr, &message, &line);
            }
        }
        finally
        {
            current = previous;
        }

        if (status == 0)
        {
            return LuaResult.Ok;
        }

        var text = message == null ? "" : Marshal.PtrToStringUTF8((nint)message) ?? "";
        if (message != null)
        {
            LuaNative.Free((nint)message);
        }

        var cut = text.IndexOf(TracebackMarker, StringComparison.Ordinal);
        var kind = status is >= 1 and <= 5 ? (LuaErrorKind)status : LuaErrorKind.Runtime;
        return new(new LuaError(
            kind,
            cut < 0 ? text : text[..cut],
            line > 0 ? line : null,
            cut < 0 ? null : text[(cut + 1)..]));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (handle == 0)
        {
            return;
        }

        LuaNative.Close(handle);
        handle = 0;
        NativeMemory.Free(limits);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnHost(byte* op, byte* args, byte** result, int* resultLength)
    {
        string text;
        var status = 0;
        try
        {
            text = current!.host!(Marshal.PtrToStringUTF8((nint)op) ?? "", Marshal.PtrToStringUTF8((nint)args) ?? "");
        }
        catch (Exception ex)
        {
            text = ex.Message; // never let an exception cross into native code
            status = 1;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var buffer = (byte*)LuaNative.Alloc((nuint)bytes.Length);
            if (buffer == null)
            {
                return 1;
            }

            bytes.CopyTo(new Span<byte>(buffer, bytes.Length));
            *result = buffer;
            *resultLength = bytes.Length;
            return status;
        }
        catch (Exception)
        {
            return 1;
        }
    }
}
