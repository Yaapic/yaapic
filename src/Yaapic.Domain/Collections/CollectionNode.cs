using Yaapic.Domain.Http;

namespace Yaapic.Domain.Collections;

/// <summary>A node of the collection tree: either a folder with children or a request.</summary>
public sealed record CollectionNode
{
    CollectionNode(Guid id, CollectionNodeKind kind, string name, FolderDefinition? folder, HttpRequestDefinition? request,
        IReadOnlyList<CollectionNode> children)
    {
        Id = id;
        Kind = kind;
        Name = name;
        Folder = folder;
        Request = request;
        Children = children;
    }

    /// <summary>Gets the identifier of the folder or request.</summary>
    public Guid Id { get; }

    /// <summary>Gets the node kind.</summary>
    public CollectionNodeKind Kind { get; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets the folder; set for folders only.</summary>
    public FolderDefinition? Folder { get; }

    /// <summary>Gets the request; set for requests only.</summary>
    public HttpRequestDefinition? Request { get; }

    /// <summary>Gets the children, in the order of <see cref="Sort"/>; always empty for requests.</summary>
    public IReadOnlyList<CollectionNode> Children { get; }

    /// <summary>Creates a folder node.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="children">The children; they are sorted with <see cref="Sort"/>.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static CollectionNode ForFolder(FolderDefinition folder, IReadOnlyList<CollectionNode> children)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(children);
        return new(folder.Id, CollectionNodeKind.Folder, folder.Name, folder, null, Sort(children));
    }

    /// <summary>Creates a request node.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static CollectionNode ForRequest(HttpRequestDefinition request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(request.Id, CollectionNodeKind.Request, request.Name, null, request, []);
    }

    /// <summary>Orders nodes: folders first, then requests, each by name (ordinal, ignoring case) then by id.</summary>
    /// <param name="nodes">The nodes to order.</param>
    /// <returns>The ordered nodes.</returns>
    public static IReadOnlyList<CollectionNode> Sort(IEnumerable<CollectionNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        return [.. nodes
            .OrderBy(static n => n.Kind)
            .ThenBy(static n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static n => n.Id)];
    }

    /// <summary>Enumerates all descendants, depth first, not including this node.</summary>
    /// <returns>The descendants.</returns>
    public IEnumerable<CollectionNode> Descendants() => Flatten(Children);

    /// <summary>Enumerates the roots and all their descendants, depth first.</summary>
    /// <param name="roots">The root nodes.</param>
    /// <returns>Every node, each parent before its children.</returns>
    public static IEnumerable<CollectionNode> Flatten(IEnumerable<CollectionNode> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        foreach (var root in roots)
        {
            yield return root;
            foreach (var d in root.Descendants()) yield return d;
        }
    }
}
