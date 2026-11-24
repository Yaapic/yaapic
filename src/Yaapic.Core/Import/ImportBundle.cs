using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Import;

/// <summary>Everything one import produced, before it is written to the stores.</summary>
/// <param name="Collection">The collection folder with its tree; <see langword="null"/> when the source holds none.</param>
/// <param name="UnfiledRequests">Requests that belong to no collection.</param>
/// <param name="Environments">The imported environments.</param>
/// <param name="Globals">The imported global variables.</param>
/// <param name="Report">What was converted with a loss or skipped.</param>
public sealed record ImportBundle(
    ImportFolder? Collection,
    IReadOnlyList<HttpRequestDefinition> UnfiledRequests,
    IReadOnlyList<EnvironmentDefinition> Environments,
    IReadOnlyList<KeyValueEntry> Globals,
    ImportReport Report)
{
    /// <summary>Gets the number of requests in the tree plus the unfiled ones.</summary>
    public int RequestCount => UnfiledRequests.Count + (Collection is null ? 0 : CountRequests(Collection));

    /// <summary>Gets the number of folders in the tree, the collection folder included.</summary>
    public int FolderCount => Collection is null ? 0 : CountFolders(Collection);

    /// <summary>Creates a bundle with nothing in it.</summary>
    /// <param name="report">The report to carry.</param>
    /// <returns>The empty bundle.</returns>
    public static ImportBundle Empty(ImportReport report) => new(null, [], [], [], report);

    private static int CountRequests(ImportFolder folder) =>
        folder.Children.Sum(c => c is ImportFolder f ? CountRequests(f) : 1);

    private static int CountFolders(ImportFolder folder) =>
        1 + folder.Children.OfType<ImportFolder>().Sum(CountFolders);
}
