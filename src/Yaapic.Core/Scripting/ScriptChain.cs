using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Scripting;

/// <summary>One script set of a <see cref="ScriptChain"/> and where it comes from.</summary>
/// <param name="Origin">The display text of the owner: a collection or folder name, or the request name.</param>
/// <param name="Scripts">The scripts of that owner.</param>
public sealed record ScriptStep(string Origin, ScriptSet Scripts);

/// <summary>The ordered script sets that apply to a request: collection, folders, then the request.</summary>
/// <param name="Steps">The steps, outermost first.</param>
public sealed record ScriptChain(IReadOnlyList<ScriptStep> Steps)
{
    /// <summary>Gets a chain with no step.</summary>
    public static ScriptChain Empty { get; } = new([]);

    /// <summary>Returns a copy with the request step appended last.</summary>
    /// <param name="name">The request name.</param>
    /// <param name="scripts">The current scripts of the request, saved or not.</param>
    /// <returns>The new chain.</returns>
    public ScriptChain WithRequest(string name, ScriptSet scripts) => new([.. Steps, new ScriptStep(name, scripts)]);
}
