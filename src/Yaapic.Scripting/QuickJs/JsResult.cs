namespace Yaapic.Scripting.QuickJs;

/// <summary>Why an evaluation failed.</summary>
internal enum JsErrorKind
{
    /// <summary>A JavaScript exception (syntax error, <c>throw</c>, runtime error).</summary>
    Exception,

    /// <summary>The interrupt handler aborted the run (deadline or cancellation).</summary>
    Interrupted,

    /// <summary>The runtime memory limit was hit.</summary>
    OutOfMemory,
}

/// <summary>A failed evaluation.</summary>
/// <param name="Kind">The failure class.</param>
/// <param name="Message">The error message.</param>
/// <param name="Stack">The JavaScript stack, if any.</param>
/// <param name="Line">The 1-based line of the failure, if it could be read from the stack.</param>
internal sealed record JsError(JsErrorKind Kind, string Message, string? Stack, int? Line);

/// <summary>The outcome of an evaluation: a value, or an error.</summary>
/// <param name="Value">Strings as is, objects as JSON, other values as <c>ToString</c>; <see langword="null"/> for <c>undefined</c> or on failure.</param>
/// <param name="Error">The failure, or <see langword="null"/> on success.</param>
internal sealed record JsResult(string? Value, JsError? Error)
{
    /// <summary>Whether the evaluation succeeded.</summary>
    internal bool IsSuccess => Error is null;
}
