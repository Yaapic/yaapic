using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IScriptEngine"/> that records invocations and answers through a delegate.</summary>
/// <param name="reply">Produces the result for each invocation.</param>
public sealed class FakeScriptEngine(Func<ScriptInvocation, ScriptRunResult> reply) : IScriptEngine
{
    private readonly List<ScriptInvocation> _invocations = [];

    /// <summary>Gets or sets the language this engine claims.</summary>
    public ScriptLanguage Language { get; set; } = ScriptLanguage.JavaScript;

    /// <summary>Gets or sets the reason reported by <see cref="IsAvailable"/>; <see langword="null"/> means available.</summary>
    public string? UnavailableReason { get; set; }

    /// <summary>Gets or sets a delay before answering; the run ends as canceled when the token is canceled meanwhile.</summary>
    public TimeSpan Delay { get; set; }

    /// <summary>Gets the invocations received so far, in order.</summary>
    public IReadOnlyList<ScriptInvocation> Invocations => _invocations;

    /// <summary>Creates an engine that answers from a queue, then with skipped results once it is empty.</summary>
    /// <param name="results">The results to return, in order.</param>
    /// <returns>The engine.</returns>
    public static FakeScriptEngine FromQueue(params ScriptRunResult[] results)
    {
        var queue = new Queue<ScriptRunResult>(results);
        return new FakeScriptEngine(i => queue.Count > 0 ? queue.Dequeue() : ScriptRunResult.Skipped(i.Phase, i.Origin, i.Block.Language));
    }

    /// <inheritdoc/>
    public bool IsAvailable(out string? reason)
    {
        reason = UnavailableReason;
        return reason is null;
    }

    /// <inheritdoc/>
    public async Task<ScriptRunResult> RunAsync(ScriptInvocation invocation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        _invocations.Add(invocation);
        if (Delay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(Delay, ct);
            }
            catch (OperationCanceledException)
            {
                return new ScriptRunResult(invocation.Phase, invocation.Origin, Language, ScriptRunStatus.Canceled, null, [], [], TimeSpan.Zero);
            }
        }

        return reply(invocation);
    }
}
