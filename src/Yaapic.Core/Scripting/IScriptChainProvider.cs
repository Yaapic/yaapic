namespace Yaapic.Core.Scripting;

/// <summary>Resolves the scripts inherited by a request from its collection and folders.</summary>
public interface IScriptChainProvider
{
    /// <summary>Gets the scripts of the folders that contain a request.</summary>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>One step per folder, outermost (the collection) first; empty for an unfiled or unknown request.</returns>
    ScriptChain GetAncestors(Guid requestId);
}
