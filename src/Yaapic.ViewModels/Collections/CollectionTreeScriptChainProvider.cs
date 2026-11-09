using Yaapic.Core.Scripting;

namespace Yaapic.ViewModels.Collections;

/// <summary>An <see cref="IScriptChainProvider"/> reading the folders of the loaded collection tree.</summary>
/// <param name="tree">The collection tree.</param>
public sealed class CollectionTreeScriptChainProvider(CollectionTreeViewModel tree) : IScriptChainProvider
{
    /// <inheritdoc/>
    public ScriptChain GetAncestors(Guid requestId) =>
        new([.. tree.FindFolderPath(requestId).Select(static f => new ScriptStep(f.Name, f.Scripts))]);
}
