using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import;

/// <summary>The counts of what <see cref="ImportApplier"/> saved, with the report of the whole import.</summary>
/// <param name="Folders">The folders saved, the collection folder included.</param>
/// <param name="Requests">The requests saved, unfiled ones included.</param>
/// <param name="Environments">The environments saved.</param>
/// <param name="GlobalVariables">The global variables added.</param>
/// <param name="Report">A new report: the bundle's entries first, then the applier's.</param>
public sealed record ImportOutcome(int Folders, int Requests, int Environments, int GlobalVariables, ImportReport Report);

/// <summary>Writes an <see cref="ImportBundle"/> into the workspace, best effort and without rollback (ADR 0016).</summary>
/// <param name="requests">The request store.</param>
/// <param name="collections">The collection store.</param>
/// <param name="environments">The environment store.</param>
/// <param name="globals">The global variables store.</param>
/// <param name="isVaultUnlocked">Tells whether secret values can be stored.</param>
public sealed class ImportApplier(
    IRequestStore requests,
    ICollectionStore collections,
    IEnvironmentStore environments,
    IGlobalsStore globals,
    Func<bool> isVaultUnlocked)
{
    private readonly IRequestStore _requests = requests;
    private readonly ICollectionStore _collections = collections;
    private readonly IEnvironmentStore _environments = environments;
    private readonly IGlobalsStore _globals = globals;

    /// <summary>Applies a bundle.</summary>
    /// <param name="bundle">The bundle to write.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>What was saved and the combined report.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled; what was already saved stays.</exception>
    /// <exception cref="StorageException">A store cannot be read (listing, loading); failed saves are reported instead.</exception>
    public async Task<ImportOutcome> ApplyAsync(ImportBundle bundle, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var run = new Run(this, ct, isVaultUnlocked());
        return await run.ExecuteAsync(bundle);
    }

    private sealed class Run(ImportApplier owner, CancellationToken ct, bool vaultOpen)
    {
        private readonly ImportReport _report = new();
        private int _dropped, _folders, _requests;

        public async Task<ImportOutcome> ExecuteAsync(ImportBundle bundle)
        {
            _report.AddRange(bundle.Report.Entries);
            var environments = await SaveEnvironmentsAfterTreeAsync(bundle);
            var addedGlobals = environments.Globals;
            if (_dropped > 0)
            {
                _report.Warning("", Strings.ImportSecretsDropped(_dropped));
            }

            return new(_folders, _requests, environments.Saved, addedGlobals, _report);
        }

        private async Task<(int Saved, int Globals)> SaveEnvironmentsAfterTreeAsync(ImportBundle bundle)
        {
            if (bundle.Collection is { } root)
            {
                await SaveCollectionAsync(root);
            }

            foreach (var request in bundle.UnfiledRequests)
            {
                await SaveRequestAsync(request, null, []);
            }

            return (await SaveEnvironmentsAsync(bundle.Environments), await MergeGlobalsAsync(bundle.Globals));
        }

        private IReadOnlyList<KeyValueEntry> Sanitize(IReadOnlyList<KeyValueEntry> rows, bool allowSecrets)
        {
            if (allowSecrets || !rows.Any(static r => r.IsSecret))
            {
                return rows;
            }

            return [.. rows.Select(r =>
            {
                if (!r.IsSecret)
                {
                    return r;
                }

                _dropped++;
                return r with { IsSecret = false, Value = "" };
            })];
        }

        private async Task SaveCollectionAsync(ImportFolder root)
        {
            var tree = await owner._collections.LoadTreeAsync(ct);
            var taken = tree.Where(static n => n.Kind == CollectionNodeKind.Folder).Select(static n => n.Name).ToList();
            var name = Unique(root.Folder.Name, taken);
            var folder = root.Folder with { Name = name, Variables = Sanitize(root.Folder.Variables, vaultOpen) };
            ct.ThrowIfCancellationRequested();
            try
            {
                await owner._collections.SaveFolderAsync(folder, null, ct);
            }
            catch (StorageException)
            {
                _report.Skipped(name, Strings.ImportSubtreeSkipped(name, CountRequests(root)));
                return;
            }

            _folders++;
            await SaveChildrenAsync(root.Children, folder.Id, [name]);
        }

        private async Task SaveChildrenAsync(IReadOnlyList<ImportItem> children, Guid parentId, string[] folderPath)
        {
            foreach (var child in children)
            {
                if (child is ImportRequest r)
                {
                    await SaveRequestAsync(r.Request, parentId, folderPath);
                    continue;
                }

                var sub = (ImportFolder)child;
                ct.ThrowIfCancellationRequested();
                var path = ImportPath.Join([.. folderPath, sub.Folder.Name]);
                var folder = sub.Folder with { Variables = Sanitize(sub.Folder.Variables, false) };
                try
                {
                    await owner._collections.SaveFolderAsync(folder, parentId, ct);
                }
                catch (StorageException)
                {
                    _report.Skipped(path, Strings.ImportSubtreeSkipped(path, CountRequests(sub)));
                    continue;
                }

                _folders++;
                await SaveChildrenAsync(sub.Children, folder.Id, [.. folderPath, sub.Folder.Name]);
            }
        }

        private async Task SaveRequestAsync(HttpRequestDefinition request, Guid? parentId, string[] folderPath)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await owner._requests.SaveAsync(request, ct);
                if (parentId is not null)
                {
                    await owner._collections.MoveAsync(request.Id, parentId, ct);
                }

                _requests++;
            }
            catch (StorageException ex)
            {
                var path = ImportPath.Join([.. folderPath, request.Name]);
                _report.Skipped(path, Strings.ImportSaveFailed(path, ex.Message));
            }
        }

        private async Task<int> SaveEnvironmentsAsync(IReadOnlyList<Domain.Environments.EnvironmentDefinition> imported)
        {
            if (imported.Count == 0)
            {
                return 0;
            }

            var taken = (await owner._environments.ListAsync(ct)).Select(static e => e.Name).ToList();
            var saved = 0;
            foreach (var environment in imported)
            {
                ct.ThrowIfCancellationRequested();
                var name = Unique(environment.Name, taken);
                try
                {
                    await owner._environments.SaveAsync(
                        environment with { Name = name, Variables = Sanitize(environment.Variables, vaultOpen) }, ct);
                    saved++;
                }
                catch (StorageException ex)
                {
                    _report.Skipped(name, Strings.ImportSaveFailed(name, ex.Message));
                }
            }

            return saved;
        }

        private async Task<int> MergeGlobalsAsync(IReadOnlyList<KeyValueEntry> imported)
        {
            if (imported.Count == 0)
            {
                return 0;
            }

            var current = await owner._globals.LoadAsync(ct);
            var keys = current.Select(static g => g.Key).ToHashSet(StringComparer.Ordinal);
            var added = new List<KeyValueEntry>();
            var skipped = 0;
            foreach (var row in Sanitize(imported, vaultOpen))
            {
                if (keys.Add(row.Key))
                {
                    added.Add(row);
                }
                else
                {
                    skipped++;
                }
            }

            if (skipped > 0)
            {
                _report.Info("", Strings.ImportGlobalsSkipped(skipped));
            }

            if (added.Count == 0)
            {
                return 0;
            }

            ct.ThrowIfCancellationRequested();
            try
            {
                await owner._globals.SaveAsync([.. current, .. added], ct);
                return added.Count;
            }
            catch (StorageException ex)
            {
                _report.Skipped("", Strings.ImportSaveFailed("", ex.Message));
                return 0;
            }
        }

        // Name, else "Name (2)", "Name (3)"...; compared ignoring case; the chosen name joins taken.
        private string Unique(string name, List<string> taken)
        {
            var unique = name;
            for (var n = 2; taken.Exists(t => string.Equals(t, unique, StringComparison.OrdinalIgnoreCase)); n++)
            {
                unique = $"{name} ({n})";
            }

            taken.Add(unique);
            if (unique != name)
            {
                _report.Info(name, Strings.ImportRenamed(name, unique));
            }

            return unique;
        }

        private static int CountRequests(ImportFolder folder) =>
            folder.Children.Sum(static c => c is ImportFolder f ? CountRequests(f) : 1);
    }
}
