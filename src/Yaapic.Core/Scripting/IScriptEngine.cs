using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Scripting;

/// <summary>
/// Runs scripts of one language. Implementations never throw for script problems (they become
/// <see cref="ScriptRunResult"/> statuses) and throw only <see cref="ArgumentNullException"/> for a null argument.
/// They honour <see cref="ScriptInvocation.Limits"/> and the cancellation token, and the console and test lists they
/// return already respect the limits.
/// </summary>
public interface IScriptEngine
{
    /// <summary>Gets the language this engine runs.</summary>
    ScriptLanguage Language { get; }

    /// <summary>Tells whether the engine can run scripts (for example, its native library loaded).</summary>
    /// <param name="reason">When unavailable, why; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when scripts can run.</returns>
    bool IsAvailable(out string? reason);

    /// <summary>Runs one script block.</summary>
    /// <param name="invocation">What to run, and the host and limits to run it with.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The outcome of the run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="invocation"/> is <see langword="null"/>.</exception>
    Task<ScriptRunResult> RunAsync(ScriptInvocation invocation, CancellationToken ct);
}
