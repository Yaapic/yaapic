namespace Yaapic.Domain.Scripting;

/// <summary>An error raised by a script.</summary>
/// <param name="Message">The error message.</param>
/// <param name="Line">The 1-based line, when known.</param>
/// <param name="Column">The 1-based column, when known.</param>
/// <param name="Stack">The stack trace text, when known.</param>
public sealed record ScriptError(string Message, int? Line = null, int? Column = null, string? Stack = null);
