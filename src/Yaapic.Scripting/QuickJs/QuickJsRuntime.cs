using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Yaapic.Scripting.QuickJs;

/// <summary>An owned QuickJS-NG runtime: memory limit, stack limit and the interrupt handler. Not thread-safe.</summary>
internal sealed unsafe class QuickJsRuntime : IDisposable
{
    private readonly List<QuickJsContext> contexts = [];
    private GCHandle interruptHandle;

    /// <summary>Creates a runtime.</summary>
    /// <param name="memoryLimit">The heap limit in bytes, 0 for none.</param>
    /// <param name="maxStackSize">The JavaScript stack limit in bytes (kept below the thread's real stack).</param>
    /// <exception cref="PlatformNotSupportedException">The process is not 64-bit (the <c>JSValue</c> layout is the 64-bit one).</exception>
    /// <exception cref="DllNotFoundException">The native library is missing.</exception>
    internal QuickJsRuntime(nuint memoryLimit = 0, nuint maxStackSize = 256 * 1024)
    {
        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException("The QuickJS bindings need a 64-bit process.");
        }

        Handle = QuickJsNative.JS_NewRuntime();
        if (Handle == 0)
        {
            throw new InvalidOperationException("JS_NewRuntime failed.");
        }

        if (memoryLimit > 0)
        {
            QuickJsNative.JS_SetMemoryLimit(Handle, memoryLimit);
        }

        QuickJsNative.JS_SetMaxStackSize(Handle, maxStackSize);
        interruptHandle = GCHandle.Alloc(Interrupt);
        QuickJsNative.JS_SetInterruptHandler(
            Handle,
            (nint)(delegate* unmanaged[Cdecl]<nint, nint, int>)&OnInterrupt,
            GCHandle.ToIntPtr(interruptHandle));
    }

    /// <summary>The deadline and cancellation flag that abort a running script.</summary>
    internal InterruptState Interrupt { get; } = new();

    /// <summary>The native <c>JSRuntime*</c>, 0 once disposed.</summary>
    internal nint Handle { get; private set; }

    /// <summary>Creates a context with the allowed intrinsics only (no <c>std</c>, no <c>os</c>, no modules).</summary>
    /// <returns>The context, disposed with the runtime.</returns>
    internal QuickJsContext CreateContext()
    {
        ObjectDisposedException.ThrowIf(Handle == 0, this);
        var context = new QuickJsContext(this);
        contexts.Add(context);
        return context;
    }

    /// <summary>Runs the promise job queue until it is empty.</summary>
    /// <returns>The error of the first failing job, or <see langword="null"/>.</returns>
    internal JsError? RunPendingJobs()
    {
        ObjectDisposedException.ThrowIf(Handle == 0, this);
        while (true)
        {
            var status = QuickJsNative.JS_ExecutePendingJob(Handle, out var failedContext);
            if (status == 0)
            {
                return null;
            }

            if (status < 0)
            {
                return contexts.Find(c => c.Handle == failedContext)?.ReadError()
                    ?? new JsError(JsErrorKind.Exception, "A pending job failed.", null, null);
            }
        }
    }

    /// <summary>Forgets a disposed context.</summary>
    internal void Remove(QuickJsContext context) => contexts.Remove(context);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Handle == 0)
        {
            return;
        }

        foreach (var context in contexts.ToArray())
        {
            context.Dispose();
        }

        QuickJsNative.JS_RunGC(Handle);
        QuickJsNative.JS_FreeRuntime(Handle);
        Handle = 0;
        interruptHandle.Free();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnInterrupt(nint context, nint opaque)
    {
        try
        {
            return ((InterruptState)GCHandle.FromIntPtr(opaque).Target!).ShouldAbort() ? 1 : 0;
        }
        catch (Exception)
        {
            return 1; // never let an exception cross into native code
        }
    }
}
