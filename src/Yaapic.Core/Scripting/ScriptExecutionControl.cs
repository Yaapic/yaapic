using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Scripting;

/// <summary>The execution control requested by the scripts of one send; shared by every host of that send.</summary>
public sealed class ScriptExecutionControl
{
    private readonly List<string> _warnings = [];

    /// <summary>Gets a value indicating whether a pre-request script asked to skip the request.</summary>
    public bool SkipRequested { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="SetNextRequest"/> took effect.</summary>
    public bool NextRequestSet { get; private set; }

    /// <summary>Gets the name or id given to the last effective <see cref="SetNextRequest"/>; <see langword="null"/> stops the run.</summary>
    public string? NextRequest { get; private set; }

    /// <summary>Gets the warnings raised by the control calls.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Chooses the next request of a Runner run; the last call wins.</summary>
    /// <param name="nameOrId">The name or id of the next request, or <see langword="null"/> to end the run.</param>
    /// <param name="inRunner">Whether the send belongs to a Runner run; outside it the call only warns, once.</param>
    public void SetNextRequest(string? nameOrId, bool inRunner)
    {
        if (!inRunner)
        {
            if (!_warnings.Contains(Strings.ScriptSetNextRequestOutsideRunner))
            {
                _warnings.Add(Strings.ScriptSetNextRequestOutsideRunner);
            }

            return;
        }

        NextRequestSet = true;
        NextRequest = nameOrId;
    }

    /// <summary>Skips the request: nothing is sent and no post-response script runs.</summary>
    /// <param name="phase">The phase of the calling script.</param>
    /// <exception cref="InvalidOperationException">The call comes from a post-response script.</exception>
    public void SkipRequest(ScriptPhase phase)
    {
        if (phase == ScriptPhase.PostResponse)
        {
            throw new InvalidOperationException(Strings.ScriptSkipRequestPostResponse);
        }

        SkipRequested = true;
    }
}
