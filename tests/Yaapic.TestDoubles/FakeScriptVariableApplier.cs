using Yaapic.Core.Scripting;
using Yaapic.ViewModels.Requests;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IScriptVariableApplier"/> that records the changes it receives.</summary>
public sealed class FakeScriptVariableApplier : IScriptVariableApplier
{
    /// <summary>Gets the calls received, in order.</summary>
    public List<(Guid RequestId, IReadOnlyList<VariableChange> Changes)> Calls { get; } = [];

    /// <summary>Gets or sets the errors returned by every call.</summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ApplyAsync(Guid requestId, IReadOnlyList<VariableChange> changes)
    {
        Calls.Add((requestId, changes));
        return Task.FromResult(Errors);
    }
}
