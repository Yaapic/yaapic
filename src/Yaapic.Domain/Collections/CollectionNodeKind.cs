namespace Yaapic.Domain.Collections;

/// <summary>The kind of a <see cref="CollectionNode"/>.</summary>
public enum CollectionNodeKind
{
    /// <summary>A folder that can hold children.</summary>
    Folder,

    /// <summary>A request; it never has children.</summary>
    Request,
}
