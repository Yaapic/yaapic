# M5-09: Core: import applier

Status: done
Platform: any
Depends on: M5-04

## Goal

`ImportApplier` writes an `ImportBundle` into the workspace through the existing stores, following the apply policy of ADR 0016.

## Design decisions

- Read first: ADR 0016 ("Apply policy"), `src/Yaapic.Core/Storage/ICollectionStore.cs`, `IRequestStore.cs`, `IEnvironmentStore.cs`, `IGlobalsStore.cs`, `src/Yaapic.Core/Secrets/SecretRows.cs` and `SecretAwareCollectionStore.cs`,
  `tests/Yaapic.TestDoubles/InMemoryCollectionStore.cs` and the other `InMemory*Store` doubles.
- `src/Yaapic.Core/Import/ImportApplier.cs`: `public sealed class ImportApplier(IRequestStore requests, ICollectionStore collections, IEnvironmentStore environments, IGlobalsStore globals, Func<bool> isVaultUnlocked)` and
  `Task<ImportOutcome> ApplyAsync(ImportBundle bundle, CancellationToken ct)`; `public sealed record ImportOutcome(int Folders, int Requests, int Environments, int GlobalVariables, ImportReport Report)` (the report is a **new** report: the bundle's entries first, then the applier's).
- Algorithm (in this order, `ct.ThrowIfCancellationRequested()` before each save):
  1. `vaultOpen = isVaultUnlocked()`. Clean every `KeyValueEntry` list you are about to save (collection variables, environments, globals, nested folder variables) with `Sanitize(rows, allowSecrets)`: when `!vaultOpen`, rows with
     `IsSecret` become `IsSecret = false, Value = ""` and are counted; nested folders (not the collection folder) always sanitize their secret rows the same way. After all saves, if the count is greater than zero add one
     `Warning` `Strings.ImportSecretsDropped(count)`.
  2. Collection: `LoadTreeAsync`, collect the names of the top-level folders; unique name = `Name`, else `Name (2)`, `Name (3)`... compared ignoring case (ordinal); when renamed add `Info` `Strings.ImportRenamed(original, unique)`.
     `SaveFolderAsync(folder with Name = unique, null)`, then walk the children depth first in order: a folder `SaveFolderAsync(child.Folder, parentId)`; a request `SaveAsync(request)` then `MoveAsync(request.Id, parentId)`
     (a new request lands unfiled, see `IRequestStore.SaveAsync`).
  3. `UnfiledRequests`: `SaveAsync` each.
  4. Environments: names compared ignoring case with `ListAsync`; clashes renamed `Name (2)` as above; `SaveAsync`.
  5. Globals: `LoadAsync`; add the rows whose key (ordinal, case-sensitive) is not present; when any was skipped add one `Info` `Strings.ImportGlobalsSkipped(count)`; `SaveAsync` only when at least one row was added.
  - A `StorageException` on a save becomes `Skipped` `Strings.ImportSaveFailed(path, message)` (path = `ImportPath.Join` of the folder names and the item name) and the walk continues; when a **folder** fails, its whole subtree is skipped and one entry per skipped
    request is **not** added (one entry for the folder says `Strings.ImportSubtreeSkipped(path, requestCount)`). Counts in the outcome only count what was saved.
- Strings (EN and FR): `import.secretsDropped` = "{0} secret values were not imported because the vault is locked. Unlock the vault and enter them again."; `import.renamed` = "\"{0}\" already exists, the import was named \"{1}\".";
  `import.globalsSkipped` = "{0} global variables already exist and were left unchanged."; `import.saveFailed` = "Could not save \"{0}\": {1}"; `import.subtreeSkipped` = "\"{0}\" could not be saved, its {1} requests were skipped.".

## Out of scope

Reading files, picking the importer (M5-10), UI.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Import/ImportApplierTests` with the in-memory stores: a nested bundle lands with the right parents (check through `LoadTreeAsync`); unfiled requests; name clash `Name (2)` and `Name (3)` ignoring case, with the `Info` entry; environment clash;
  globals merge (existing key kept, new key added, `Info` count); locked vault drops secret values and adds one `Warning` with the count, unlocked vault passes the rows through unchanged (collection folder, environment, globals); nested folder secret rows are always blanked;
  a store that throws `StorageException` on one request gives `Skipped` and the other requests still land; a failing folder skips its subtree with a single entry; cancellation throws `OperationCanceledException`; the outcome counts; the bundle's own entries come first in the outcome report.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the import applier`
