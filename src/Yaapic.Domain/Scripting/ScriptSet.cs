namespace Yaapic.Domain.Scripting;

/// <summary>The pre-request and post-response scripts of a request or folder.</summary>
/// <param name="PreRequest">The script run before the request is sent.</param>
/// <param name="PostResponse">The script run after the response is received.</param>
public sealed record ScriptSet(ScriptBlock PreRequest, ScriptBlock PostResponse)
{
    /// <summary>A set with two empty blocks.</summary>
    public static ScriptSet Empty { get; } = new(ScriptBlock.Empty, ScriptBlock.Empty);

    /// <summary>Gets a value indicating whether both blocks are empty.</summary>
    public bool IsEmpty => PreRequest.IsEmpty && PostResponse.IsEmpty;

    /// <summary>Gets the block of a phase.</summary>
    /// <param name="phase">The phase.</param>
    /// <returns>The block.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="phase"/> is not a defined value.</exception>
    public ScriptBlock Get(ScriptPhase phase) => phase switch
    {
        ScriptPhase.PreRequest => PreRequest,
        ScriptPhase.PostResponse => PostResponse,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    /// <summary>Returns a copy with the block of a phase replaced.</summary>
    /// <param name="phase">The phase.</param>
    /// <param name="block">The new block.</param>
    /// <returns>The new set.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="phase"/> is not a defined value.</exception>
    public ScriptSet With(ScriptPhase phase, ScriptBlock block) => phase switch
    {
        ScriptPhase.PreRequest => this with { PreRequest = block },
        ScriptPhase.PostResponse => this with { PostResponse = block },
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };
}
