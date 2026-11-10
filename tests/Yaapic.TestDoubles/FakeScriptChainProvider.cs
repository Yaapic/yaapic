using Yaapic.Core.Scripting;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IScriptChainProvider"/> returning a fixed chain.</summary>
public sealed class FakeScriptChainProvider : IScriptChainProvider
{
    /// <summary>Gets or sets the chain returned for every request.</summary>
    public ScriptChain Chain { get; set; } = ScriptChain.Empty;

    /// <inheritdoc/>
    public ScriptChain GetAncestors(Guid requestId) => Chain;
}
