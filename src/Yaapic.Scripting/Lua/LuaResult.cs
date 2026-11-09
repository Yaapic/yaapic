namespace Yaapic.Scripting.Lua;

/// <summary>Why a Lua run failed.</summary>
internal enum LuaErrorKind
{
    /// <summary>A runtime error (<c>error(...)</c>, a nil call, a failing host call).</summary>
    Runtime = 1,

    /// <summary>The chunk does not compile.</summary>
    Syntax = 2,

    /// <summary>The state memory limit was hit.</summary>
    OutOfMemory = 3,

    /// <summary>The deadline passed.</summary>
    Timeout = 4,

    /// <summary>The cancel flag was raised.</summary>
    Canceled = 5,
}

/// <summary>A failed run.</summary>
/// <param name="Kind">The failure class.</param>
/// <param name="Message">The message without the traceback.</param>
/// <param name="Line">The 1-based line of the failure, if the message carries one.</param>
/// <param name="Traceback">The Lua traceback, if any.</param>
internal sealed record LuaError(LuaErrorKind Kind, string Message, int? Line, string? Traceback);

/// <summary>The outcome of a run: success, or an error.</summary>
/// <param name="Error">The failure, or <see langword="null"/> on success.</param>
internal sealed record LuaResult(LuaError? Error)
{
    /// <summary>A successful run.</summary>
    internal static LuaResult Ok { get; } = new((LuaError?)null);

    /// <summary>Whether the run succeeded.</summary>
    internal bool IsSuccess => Error is null;
}
