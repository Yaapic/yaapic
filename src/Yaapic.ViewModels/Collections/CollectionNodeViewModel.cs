using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;

namespace Yaapic.ViewModels.Collections;

/// <summary>A folder or request row of the collection tree.</summary>
public sealed partial class CollectionNodeViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Creates a row.</summary>
    /// <param name="node">The domain node.</param>
    /// <param name="parent">The parent row; null for a root.</param>
    public CollectionNodeViewModel(CollectionNode node, CollectionNodeViewModel? parent)
    {
        ArgumentNullException.ThrowIfNull(node);
        Node = node;
        Parent = parent;
    }

    /// <summary>Gets the domain node.</summary>
    public CollectionNode Node { get; }

    /// <summary>Gets the parent row; null for a root.</summary>
    public CollectionNodeViewModel? Parent { get; }

    /// <summary>Gets the visible children, in store order.</summary>
    public ObservableCollection<CollectionNodeViewModel> Children { get; } = [];

    /// <summary>Gets the identifier of the folder or request.</summary>
    public Guid Id => Node.Id;

    /// <summary>Gets the node kind.</summary>
    public CollectionNodeKind Kind => Node.Kind;

    /// <summary>Gets a value indicating whether this row is a folder.</summary>
    public bool IsFolder => Kind == CollectionNodeKind.Folder;

    /// <summary>Gets a value indicating whether this row is a folder with no parent, that is a collection.</summary>
    public bool IsCollection => IsFolder && Parent is null;

    /// <summary>Gets the display name.</summary>
    public string Name => Node.Name;

    /// <summary>Gets the HTTP method of a request; null for a folder.</summary>
    public HttpMethodKind? Method => Node.Request?.Method;

    /// <summary>Gets the upper-case method name of a request, such as <c>GET</c>; empty for a folder.</summary>
    public string MethodText => Method?.ToText() ?? "";

    /// <summary>Gets the URL template of a request; empty for a folder.</summary>
    public string Url => Node.Request?.Url ?? "";

    internal Action<CollectionNodeViewModel>? ExpansionChanged { get; set; }

    partial void OnIsExpandedChanged(bool value) => ExpansionChanged?.Invoke(this);
}
