namespace Yaapic.Core.Scripting;

/// <summary>The variable layer a script reads or writes.</summary>
public enum ScriptScope
{
    /// <summary>The <c>yaapic.variables</c> view: every layer merged, the highest layer wins; writes go to the local layer.</summary>
    Layered,

    /// <summary>Variables local to one send.</summary>
    Local,

    /// <summary>The active environment.</summary>
    Environment,

    /// <summary>The collection variables.</summary>
    Collection,

    /// <summary>The globals.</summary>
    Globals,
}
