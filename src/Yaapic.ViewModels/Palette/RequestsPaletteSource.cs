using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Collections;

namespace Yaapic.ViewModels.Palette;

/// <summary>Offers every saved request of the loaded collection tree.</summary>
public sealed class RequestsPaletteSource : IPaletteSource
{
    private readonly CollectionTreeViewModel _collections;

    /// <summary>Creates the source.</summary>
    /// <param name="collections">The collection tree whose loaded requests are offered.</param>
    public RequestsPaletteSource(CollectionTreeViewModel collections)
    {
        ArgumentNullException.ThrowIfNull(collections);
        _collections = collections;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PaletteItem> GetItems()
    {
        List<PaletteItem> items = [];
        Collect(_collections.Tree, "", items);
        return items;
    }

    private void Collect(IEnumerable<CollectionNode> nodes, string path, List<PaletteItem> items)
    {
        foreach (var node in nodes)
        {
            if (node.Kind == CollectionNodeKind.Folder)
            {
                Collect(node.Children, path.Length == 0 ? node.Name : $"{path} / {node.Name}", items);
            }
            else if (node.Request is { } request)
            {
                var subtitle = $"{request.Method.ToText()}  {request.Url}" + (path.Length == 0 ? "" : $"  ·  {path}");
                var id = request.Id;
                items.Add(new PaletteItem($"request:{id}", request.Name, subtitle, "Request", null, () =>
                {
                    _collections.OpenRequest(id);
                    return Task.CompletedTask;
                }));
            }
        }
    }
}
