using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Yaapic.Scripting.QuickJs;

/// <summary>A QuickJS-NG context. Every <c>JSValue</c> it owns is freed; <see cref="LiveValues"/> proves it in tests.</summary>
internal sealed unsafe partial class QuickJsContext : IDisposable
{
    private const string OutOfMemoryText = "out of memory";
    private const int EvalFlagAsync = 1 << 7; // JS_EVAL_FLAG_ASYNC
    private const int PromisePending = 0;
    private const int PromiseRejected = 2;

    private readonly QuickJsRuntime runtime;
    private readonly List<Func<string, string, string>> callbacks = [];
    private GCHandle self;

    internal QuickJsContext(QuickJsRuntime runtime)
    {
        this.runtime = runtime;
        Handle = QuickJsNative.JS_NewContextRaw(runtime.Handle);
        if (Handle == 0)
        {
            throw new InvalidOperationException("JS_NewContextRaw failed.");
        }

        // Allowed intrinsics only: no std, no os, no module loader, so require/std/os stay undefined.
        QuickJsNative.JS_AddIntrinsicBaseObjects(Handle);
        QuickJsNative.JS_AddIntrinsicDate(Handle);
        QuickJsNative.JS_AddIntrinsicEval(Handle);
        QuickJsNative.JS_AddIntrinsicRegExpCompiler(Handle);
        QuickJsNative.JS_AddIntrinsicRegExp(Handle);
        QuickJsNative.JS_AddIntrinsicJSON(Handle);
        QuickJsNative.JS_AddIntrinsicProxy(Handle);
        QuickJsNative.JS_AddIntrinsicMapSet(Handle);
        QuickJsNative.JS_AddIntrinsicTypedArrays(Handle);
        QuickJsNative.JS_AddIntrinsicPromise(Handle);

        self = GCHandle.Alloc(this);
        QuickJsNative.JS_SetContextOpaque(Handle, GCHandle.ToIntPtr(self));
    }

    /// <summary>The native <c>JSContext*</c>, 0 once disposed.</summary>
    internal nint Handle { get; private set; }

    /// <summary>The number of <c>JSValue</c>s obtained and not yet freed (must be 0 between operations).</summary>
    internal int LiveValues { get; private set; }

    /// <summary>Evaluates a global script.</summary>
    /// <param name="source">The JavaScript source.</param>
    /// <param name="fileName">The name shown in stack traces.</param>
    /// <returns>The value or the error.</returns>
    internal JsResult Eval(string source, string fileName = "<eval>")
    {
        ThrowIfDisposed();
        var input = Utf8(source);
        var name = Utf8(fileName);
        JSValue value;
        fixed (byte* inputPtr = input)
        fixed (byte* namePtr = name)
        {
            value = Own(QuickJsNative.JS_Eval(Handle, inputPtr, (nuint)(input.Length - 1), namePtr, 0));
        }

        try
        {
            return value.IsException ? new JsResult(null, ReadError()) : new JsResult(ValueToString(value), null);
        }
        finally
        {
            Free(value);
        }
    }

    /// <summary>
    /// Evaluates a global script that may use top-level <c>await</c>, then drains the promise job queue.
    /// </summary>
    /// <param name="source">The JavaScript source.</param>
    /// <param name="fileName">The name shown in stack traces.</param>
    /// <param name="pending">Set when the script's promise never settled once the job queue was empty.</param>
    /// <returns>The error, or <see langword="null"/> when the script finished.</returns>
    internal JsError? EvalAsync(string source, string fileName, out bool pending)
    {
        ThrowIfDisposed();
        pending = false;
        var input = Utf8(source);
        var name = Utf8(fileName);
        JSValue promise;
        fixed (byte* inputPtr = input)
        fixed (byte* namePtr = name)
        {
            promise = Own(QuickJsNative.JS_Eval(Handle, inputPtr, (nuint)(input.Length - 1), namePtr, EvalFlagAsync));
        }

        try
        {
            if (promise.IsException)
            {
                return ReadError();
            }

            var jobError = runtime.RunPendingJobs();
            switch (QuickJsNative.JS_PromiseState(Handle, promise))
            {
                case PromiseRejected:
                    return DescribeError(Own(QuickJsNative.JS_PromiseResult(Handle, promise)));
                case PromisePending:
                    pending = true;
                    return jobError;
                default:
                    return jobError;
            }
        }
        finally
        {
            Free(promise);
        }
    }

    /// <summary>Defines a global function <c>name(a, b)</c> that calls <paramref name="callback"/> with both arguments as strings.</summary>
    /// <param name="name">The global name.</param>
    /// <param name="callback">Receives the first two arguments (empty when missing); its result is returned to JavaScript. A throw becomes a JavaScript error.</param>
    internal void DefineHostFunction(string name, Func<string, string, string> callback)
    {
        ThrowIfDisposed();
        callbacks.Add(callback);
        var function = Own(QuickJsNative.JS_NewCFunctionData(
            Handle, (nint)(delegate* unmanaged[Cdecl]<nint, JSValue, int, JSValue*, int, JSValue*, JSValue>)&OnHostCall, 2, callbacks.Count - 1, 0, null));
        var global = Own(QuickJsNative.JS_GetGlobalObject(Handle));
        fixed (byte* namePtr = Utf8(name))
        {
            QuickJsNative.JS_SetPropertyStr(Handle, global, namePtr, function); // takes ownership of function
        }

        LiveValues--;
        Free(global);
    }

    /// <summary>Reads and clears the pending exception.</summary>
    /// <returns>The error, classified by the interrupt state and the message.</returns>
    internal JsError ReadError() => DescribeError(Own(QuickJsNative.JS_GetException(Handle)));

    private JsError DescribeError(JSValue exception)
    {
        string? stack = null;
        string? message;
        if (exception.IsObject)
        {
            message = StringProperty(exception, "message") ?? ToStr(exception);
            stack = StringProperty(exception, "stack");
        }
        else
        {
            message = ToStr(exception);
        }

        Free(exception);
        ClearPendingException();
        message ??= "Unknown error";

        var kind = runtime.Interrupt.Fired ? JsErrorKind.Interrupted
            : message.Contains(OutOfMemoryText, StringComparison.Ordinal) ? JsErrorKind.OutOfMemory
            : JsErrorKind.Exception;
        return new JsError(kind, message, stack, stack is null ? null : LineOf(stack));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Handle == 0)
        {
            return;
        }

        QuickJsNative.JS_FreeContext(Handle);
        Handle = 0;
        self.Free();
        runtime.Remove(this);
    }

    [GeneratedRegex(@":(\d+)(?::\d+)?\)?\s*$", RegexOptions.Multiline)]
    private static partial Regex LinePattern();

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static JSValue OnHostCall(nint context, JSValue thisValue, int argc, JSValue* argv, int magic, JSValue* data)
    {
        var owner = (QuickJsContext)GCHandle.FromIntPtr(QuickJsNative.JS_GetContextOpaque(context)).Target!;
        try
        {
            var first = argc > 0 ? owner.ToStr(argv[0]) : string.Empty;
            var second = argc > 1 ? owner.ToStr(argv[1]) : string.Empty;
            if (first is null || second is null)
            {
                return JSValue.Exception; // the conversion left its exception pending
            }

            var result = owner.callbacks[magic](first, second);
            var bytes = Encoding.UTF8.GetBytes(result);
            fixed (byte* ptr = bytes)
            {
                return QuickJsNative.JS_NewStringLen(context, ptr, (nuint)bytes.Length); // ownership goes to the engine
            }
        }
        catch (Exception ex)
        {
            return owner.Throw(ex.Message);
        }
    }

    private JSValue Throw(string message)
    {
        var error = QuickJsNative.JS_NewError(Handle);
        var bytes = Encoding.UTF8.GetBytes(message);
        fixed (byte* text = bytes)
        fixed (byte* property = "message\0"u8)
        {
            var value = QuickJsNative.JS_NewStringLen(Handle, text, (nuint)bytes.Length);
            QuickJsNative.JS_SetPropertyStr(Handle, error, property, value);
        }

        return QuickJsNative.JS_Throw(Handle, error); // takes ownership of the error, returns JS_EXCEPTION
    }

    private string? ValueToString(JSValue value)
    {
        if (value.IsUndefined)
        {
            return null;
        }

        if (value.IsObject)
        {
            var json = Own(QuickJsNative.JS_JSONStringify(Handle, value, JSValue.Undefined, JSValue.Undefined));
            var text = json.IsException || json.IsUndefined ? null : ToStr(json);
            Free(json);
            ClearPendingException(); // a cyclic object falls back to ToString
            if (text is not null)
            {
                return text;
            }
        }

        var result = ToStr(value);
        ClearPendingException();
        return result;
    }

    private string? StringProperty(JSValue obj, string name)
    {
        fixed (byte* namePtr = Utf8(name))
        {
            var property = Own(QuickJsNative.JS_GetPropertyStr(Handle, obj, namePtr));
            var text = property.IsException || property.IsUndefined ? null : ToStr(property);
            Free(property);
            ClearPendingException();
            return text;
        }
    }

    private string? ToStr(JSValue value)
    {
        var ptr = QuickJsNative.JS_ToCStringLen2(Handle, out var length, value, 0);
        if (ptr == 0)
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString((byte*)ptr, checked((int)length));
        }
        finally
        {
            QuickJsNative.JS_FreeCString(Handle, ptr);
        }
    }

    private void ClearPendingException() => Free(Own(QuickJsNative.JS_GetException(Handle)));

    private static int? LineOf(string stack) =>
        LinePattern().Match(stack) is { Success: true } match && int.TryParse(match.Groups[1].Value, out var line) ? line : null;

    private static byte[] Utf8(string text)
    {
        var bytes = new byte[Encoding.UTF8.GetByteCount(text) + 1]; // QuickJS needs a trailing NUL
        Encoding.UTF8.GetBytes(text, bytes);
        return bytes;
    }

    private JSValue Own(JSValue value)
    {
        LiveValues++;
        return value;
    }

    private void Free(JSValue value)
    {
        LiveValues--;
        QuickJsNative.JS_FreeValue(Handle, value);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Handle == 0, this);
}
