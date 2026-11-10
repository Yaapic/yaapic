using System.Runtime.InteropServices;

namespace Yaapic.Scripting.QuickJs;

/// <summary>Raw QuickJS-NG C API (blittable types only). Inline-only helpers are re-implemented on <see cref="JSValue"/>.</summary>
internal static unsafe partial class QuickJsNative
{
    private const string Lib = "quickjs-ng";

    static QuickJsNative() => NativeLibraryResolver.EnsureRegistered();

    [LibraryImport(Lib)] internal static partial nint JS_NewRuntime();
    [LibraryImport(Lib)] internal static partial void JS_FreeRuntime(nint rt);
    [LibraryImport(Lib)] internal static partial void JS_SetMemoryLimit(nint rt, nuint limit);
    [LibraryImport(Lib)] internal static partial void JS_SetMaxStackSize(nint rt, nuint size);
    [LibraryImport(Lib)] internal static partial void JS_SetInterruptHandler(nint rt, nint handler, nint opaque);
    [LibraryImport(Lib)] internal static partial void JS_RunGC(nint rt);
    [LibraryImport(Lib)] internal static partial int JS_ExecutePendingJob(nint rt, out nint ctx);

    [LibraryImport(Lib)] internal static partial nint JS_NewContextRaw(nint rt);
    [LibraryImport(Lib)] internal static partial void JS_FreeContext(nint ctx);
    [LibraryImport(Lib)] internal static partial nint JS_GetContextOpaque(nint ctx);
    [LibraryImport(Lib)] internal static partial void JS_SetContextOpaque(nint ctx, nint opaque);
    [LibraryImport(Lib)] internal static partial int JS_AddIntrinsicBaseObjects(nint ctx);
    [LibraryImport(Lib)] internal static partial int JS_AddIntrinsicDate(nint ctx);
    [LibraryImport(Lib)] internal static partial int JS_AddIntrinsicEval(nint ctx);
    [LibraryImport(Lib)] internal static partial void JS_AddIntrinsicRegExpCompiler(nint ctx);
    [LibraryImport(Lib)] internal static partial int JS_AddIntrinsicRegExp(nint ctx);
    [LibraryImport(Lib)] internal static partial int JS_AddIntrinsicJSON(nint ctx);
    [LibraryImport(Lib)] internal static partial int JS_AddIntrinsicProxy(nint ctx);
    [LibraryImport(Lib)] internal static partial int JS_AddIntrinsicMapSet(nint ctx);
    [LibraryImport(Lib)] internal static partial int JS_AddIntrinsicTypedArrays(nint ctx);
    [LibraryImport(Lib)] internal static partial int JS_AddIntrinsicPromise(nint ctx);

    [LibraryImport(Lib)] internal static partial JSValue JS_Eval(nint ctx, byte* input, nuint length, byte* fileName, int flags);
    [LibraryImport(Lib)] internal static partial int JS_PromiseState(nint ctx, JSValue promise);
    [LibraryImport(Lib)] internal static partial JSValue JS_PromiseResult(nint ctx, JSValue promise);
    [LibraryImport(Lib)] internal static partial JSValue JS_GetException(nint ctx);
    [LibraryImport(Lib)] internal static partial void JS_FreeValue(nint ctx, JSValue value);
    [LibraryImport(Lib)] internal static partial JSValue JS_JSONStringify(nint ctx, JSValue obj, JSValue replacer, JSValue space);
    [LibraryImport(Lib)] internal static partial nint JS_ToCStringLen2(nint ctx, out nuint length, JSValue value, byte cesu8);
    [LibraryImport(Lib)] internal static partial void JS_FreeCString(nint ctx, nint text);
    [LibraryImport(Lib)] internal static partial JSValue JS_NewStringLen(nint ctx, byte* text, nuint length);
    [LibraryImport(Lib)] internal static partial JSValue JS_GetPropertyStr(nint ctx, JSValue obj, byte* name);
    [LibraryImport(Lib)] internal static partial int JS_SetPropertyStr(nint ctx, JSValue obj, byte* name, JSValue value);
    [LibraryImport(Lib)] internal static partial JSValue JS_GetGlobalObject(nint ctx);
    [LibraryImport(Lib)] internal static partial JSValue JS_NewError(nint ctx);
    [LibraryImport(Lib)] internal static partial JSValue JS_Throw(nint ctx, JSValue error);
    [LibraryImport(Lib)] internal static partial JSValue JS_NewCFunctionData(nint ctx, nint function, int length, int magic, int dataLength, JSValue* data);
}
