namespace Yaapic.Domain.Scripting;

/// <summary>The result of running one script.</summary>
/// <param name="Phase">The phase the script ran in.</param>
/// <param name="Origin">Free text naming the owner of the script.</param>
/// <param name="Language">The script language.</param>
/// <param name="Status">How the run ended.</param>
/// <param name="Error">The error, when the run did not succeed.</param>
/// <param name="Console">The console entries written during the run.</param>
/// <param name="Tests">The test results produced during the run.</param>
/// <param name="Duration">How long the run took.</param>
public sealed record ScriptRunResult(
    ScriptPhase Phase,
    string Origin,
    ScriptLanguage Language,
    ScriptRunStatus Status,
    ScriptError? Error,
    IReadOnlyList<ConsoleEntry> Console,
    IReadOnlyList<TestResult> Tests,
    TimeSpan Duration)
{
    /// <summary>Gets a value indicating whether the run succeeded.</summary>
    public bool Succeeded => Status == ScriptRunStatus.Succeeded;

    /// <summary>Creates the result of a script that was not run: succeeded, no output, zero duration.</summary>
    /// <param name="phase">The phase.</param>
    /// <param name="origin">The origin text.</param>
    /// <param name="language">The language.</param>
    /// <returns>A succeeded result with no output.</returns>
    public static ScriptRunResult Skipped(ScriptPhase phase, string origin, ScriptLanguage language) =>
        new(phase, origin, language, ScriptRunStatus.Succeeded, null, [], [], TimeSpan.Zero);

    /// <summary>Creates the result of a run whose engine could not be loaded.</summary>
    /// <param name="phase">The phase.</param>
    /// <param name="origin">The origin text.</param>
    /// <param name="language">The language.</param>
    /// <param name="reason">Why the engine is unavailable; becomes the error message.</param>
    /// <returns>An unavailable result.</returns>
    public static ScriptRunResult Unavailable(ScriptPhase phase, string origin, ScriptLanguage language, string reason) =>
        new(phase, origin, language, ScriptRunStatus.Unavailable, new ScriptError(reason), [], [], TimeSpan.Zero);
}
