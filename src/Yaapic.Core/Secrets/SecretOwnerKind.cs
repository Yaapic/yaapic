namespace Yaapic.Core.Secrets;

/// <summary>The kind of object that owns a secret.</summary>
public enum SecretOwnerKind
{
    /// <summary>An environment.</summary>
    Environment,

    /// <summary>A collection.</summary>
    Collection,

    /// <summary>The workspace globals.</summary>
    Globals,
}
