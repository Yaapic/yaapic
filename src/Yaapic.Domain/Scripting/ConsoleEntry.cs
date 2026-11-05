namespace Yaapic.Domain.Scripting;

/// <summary>One line of the console.</summary>
/// <param name="At">When the line was written.</param>
/// <param name="Level">The severity.</param>
/// <param name="Source">Where the line comes from.</param>
/// <param name="Message">The text; no length rule here (the engine truncates).</param>
/// <param name="Origin">Free text such as "pre-request · Users API", or null.</param>
public sealed record ConsoleEntry(
    DateTimeOffset At,
    ConsoleLevel Level,
    ConsoleSource Source,
    string Message,
    string? Origin = null);
