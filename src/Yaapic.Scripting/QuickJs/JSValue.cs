using System.Runtime.InteropServices;

namespace Yaapic.Scripting.QuickJs;

/// <summary>
/// Mirror of QuickJS-NG's <c>JSValue</c> on 64-bit targets without NaN boxing: an 8-byte union followed by an
/// <c>int64</c> tag (16 bytes, passed by value).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct JSValue(ulong bits, long tag)
{
    private const long TagObject = -1;
    private const long TagUndefined = 3;
    private const long TagException = 6;

    /// <summary>The union payload.</summary>
    internal readonly ulong Bits = bits;

    /// <summary>The type tag.</summary>
    internal readonly long Tag = tag;

    /// <summary><c>JS_UNDEFINED</c>.</summary>
    internal static JSValue Undefined => new(0, TagUndefined);

    /// <summary><c>JS_EXCEPTION</c>: an exception is pending on the context.</summary>
    internal static JSValue Exception => new(0, TagException);

    /// <summary>Whether this is <c>JS_EXCEPTION</c>.</summary>
    internal bool IsException => Tag == TagException;

    /// <summary>Whether this is <c>undefined</c>.</summary>
    internal bool IsUndefined => Tag == TagUndefined;

    /// <summary>Whether this is an object.</summary>
    internal bool IsObject => Tag == TagObject;
}
