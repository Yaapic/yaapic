using Yaapic.Domain.Collections;

namespace Yaapic.Core.Runner;

/// <summary>Builds the request list of a run.</summary>
public static class RunPlanBuilder
{
    /// <summary>Lists the requests of a folder, depth first, in <see cref="CollectionNode.Children"/> order.</summary>
    /// <param name="folder">The run folder.</param>
    /// <returns>The requests with their folder paths.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="folder"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="folder"/> is a request node.</exception>
    public static IReadOnlyList<RunItem> ListRequests(CollectionNode folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (folder.Kind != CollectionNodeKind.Folder)
        {
            throw new ArgumentException("The run root must be a folder.", nameof(folder));
        }

        List<RunItem> items = [];
        Walk(folder, folder.Name, items);
        return items;
    }

    private static void Walk(CollectionNode folder, string path, List<RunItem> items)
    {
        foreach (CollectionNode child in folder.Children)
        {
            if (child.Kind == CollectionNodeKind.Folder)
            {
                Walk(child, $"{path}/{child.Name}", items);
            }
            else
            {
                items.Add(new RunItem(child.Request!, path));
            }
        }
    }
}
