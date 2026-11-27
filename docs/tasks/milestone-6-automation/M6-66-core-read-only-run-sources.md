# M6-66: Core: read-only run sources and folder lookup

Status: todo
Platform: any
Depends on: M6-65

## Goal

Give the runner a variable source and a script chain built from a loaded collection tree, without any view model, so a headless run
(ADR 0022) can use `RunStepExecutor` and `CollectionRunner` as they are.

## Design decisions

- Read first: ADR 0022, ADR 0018 ("Variables"), `src/Yaapic.Core/Runner/IRunScopeSource.cs`, `RunStepExecutor.cs`, `RunPlanBuilder.cs`,
  `src/Yaapic.Core/Scripting/IScriptChainProvider.cs`, `src/Yaapic.ViewModels/Collections/CollectionTreeScriptChainProvider.cs` and
  `CollectionTreeViewModel.FindCollection`/`FindCollectionVariables`/`FindFolderPath` (the app's behaviour to copy),
  `src/Yaapic.ViewModels/Requests/VariableScopeProvider.GetWritableScopes`, `MonitorScheduler.MonitorScopeSource` (a similar read-only source).
- `src/Yaapic.Core/Scripting/TreeScriptChainProvider.cs`:
  `public sealed class TreeScriptChainProvider(IReadOnlyList<CollectionNode> roots) : IScriptChainProvider`.
  `GetAncestors(requestId)` returns one `ScriptStep(folder.Name, folder.Scripts)` per folder from the top-level folder (the collection) down
  to the request's parent, the same output as `CollectionTreeScriptChainProvider` for the same tree; empty for an unknown request. Walk the
  tree once per call (no cache: trees are small and the class must stay correct if the caller passes a fresh tree).
- `src/Yaapic.Core/Runner/StaticRunScopeSource.cs`:
  `public sealed class StaticRunScopeSource(IReadOnlyList<CollectionNode> roots, EnvironmentDefinition? environment, IReadOnlyList<KeyValueEntry> globals) : IRunScopeSource`.
  - `GetScopes(id)` = `new VariableScopes(environment, <variables of the top-level folder that contains the request, or []>, globals)`.
  - `GetWritableScopes(id)`: `Environment` when `environment` is not null, `Collection` when the request is inside a top-level folder,
    `Globals` always, in that order (same as `VariableScopeProvider`). Scripts can then set variables; the runner keeps them in its
    `RunVariableState` for the rest of the run.
  - `PersistAsync` does nothing and returns an empty list (headless runs never write, ADR 0022). XML doc says so.
- `RunPlanBuilder.FindFolder`: `public static CollectionNode? FindFolder(IReadOnlyList<CollectionNode> roots, string path)`.
  Splits `path` on `/`, trims nothing, ignores one leading or trailing `/`; the first segment matches a top-level folder name, each next
  segment a child folder name, ordinal and case-sensitive; returns `null` when a segment is missing or names a request. Throws
  `ArgumentException` for a null, empty or whitespace path or an empty segment (`A//B`).
- No user-visible text (the localization rule test scans Core): exception messages are developer-facing English.

## Out of scope

Variable overrides and masking (M6-67), composition (M6-68).

## Acceptance criteria

- [ ] `tests/Yaapic.Core.Tests/Scripting/TreeScriptChainProviderTests.cs`: a request at the root of a collection (one step), a request two
  folders deep (three steps, outermost first, names and scripts kept), an unknown id (empty chain).
- [ ] `tests/Yaapic.Core.Tests/Runner/StaticRunScopeSourceTests.cs`: scopes carry the environment, the request's collection variables (not
  another collection's) and the globals; writable scopes with and without an environment; `PersistAsync` returns empty and changes nothing.
- [ ] `tests/Yaapic.Core.Tests/Runner/RunPlanBuilderFindFolderTests.cs`: a collection, a nested folder, a leading and a trailing `/`, a wrong
  case (null), a request name as last segment (null), a missing segment (null), `""`, `"  "` and `"A//B"` (throw).
- [ ] Architecture tests green, 0 warnings, XML docs on every public member, all tests green.
- [ ] Docs: none in this card (M6-74 documents the CLI).

## Suggested commit message

`feat(core): add read-only run sources for headless runs`
