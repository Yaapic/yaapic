using Yaapic.Core.Import;
using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Export;

/// <summary>Reads the data to export from the stores, blanking every secret value.</summary>
/// <param name="collections">The collection store.</param>
/// <param name="environments">The environment store.</param>
/// <param name="globals">The globals store.</param>
public sealed class ExportSourceReader(ICollectionStore collections, IEnvironmentStore environments, IGlobalsStore globals)
{
    /// <summary>Reads the source.</summary>
    /// <param name="collectionId">The top-level folder to export, or <see langword="null"/> for none.</param>
    /// <param name="environmentIds">The environments to export; empty means none, unknown ids are ignored.</param>
    /// <param name="includeGlobals">Whether to export the global variables.</param>
    /// <param name="report">Receives an <see cref="ImportSeverity.Info"/> when secret values were blanked.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ExportException"><paramref name="collectionId"/> is not a top-level folder.</exception>
    /// <exception cref="StorageException">A store cannot be read.</exception>
    public async Task<ExportSource> ReadAsync(Guid? collectionId, IReadOnlyList<Guid> environmentIds, bool includeGlobals,
        ImportReport report, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(environmentIds);
        ArgumentNullException.ThrowIfNull(report);
        var blanked = 0;

        CollectionNode? collection = null;
        if (collectionId is { } id)
        {
            var roots = await collections.LoadTreeAsync(ct);
            var root = roots.FirstOrDefault(n => n.Id == id && n.Kind == CollectionNodeKind.Folder)
                ?? throw new ExportException(Strings.ExportCollectionNotFound);
            collection = Blank(root);
        }

        var wanted = environmentIds.ToHashSet();
        var envs = (await environments.ListAsync(ct)).Where(e => wanted.Contains(e.Id))
            .Select(e => e with { Variables = BlankAll(e.Variables) }).ToList();
        var globalRows = includeGlobals ? BlankAll(await globals.LoadAsync(ct)) : [];

        if (blanked > 0) report.Info("", Strings.ExportSecretsOmitted(blanked));
        return new ExportSource(collection, envs, globalRows, collection?.Name ?? "Environments");

        IReadOnlyList<KeyValueEntry> BlankAll(IReadOnlyList<KeyValueEntry> rows) =>
            [.. rows.Select(r =>
            {
                if (!r.IsSecret) return r;
                blanked++;
                return r with { Value = "" };
            })];

        CollectionNode Blank(CollectionNode node) =>
            node.Kind == CollectionNodeKind.Request
                ? node
                : CollectionNode.ForFolder(node.Folder! with { Variables = BlankAll(node.Folder!.Variables) },
                    [.. node.Children.Select(Blank)]);
    }
}
