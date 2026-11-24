# M6-06: Core: data variable layer

Status: done
Platform: any
Depends on: M6-01

## Goal

Add the iteration **data** layer to variable resolution (ADR 0018): local, data, environment, collection, globals, dynamic.
Outside a run the layer is empty, so nothing changes for single sends.

## Design decisions

- Read first: ADR 0018 ("Data variable layer"), `src/Yaapic.Core/Variables/VariableScopes.cs`, `VariableScopeKind.cs`,
  `CompositeVariableSource.cs`, `src/Yaapic.Core/Scripting/ScriptVariableOverlay.cs`, `src/Yaapic.ViewModels/Requests/VariableContext.cs`
  and `VariablePopupViewModel.cs` (they switch on `VariableScopeKind`).
- `VariableScopeKind` gains `Data` **appended last** (after `Local`; never reorder existing members, their numeric values may be
  stored). Its XML doc: "The iteration data of a Runner run (CSV or JSON row); read-only; consulted after the local variables."
- `VariableScopes` gains a fifth optional positional parameter `IReadOnlyList<KeyValueEntry>? Data = null` (after `Local`).
  `CreateSource()` order: `Local`, `Data`, environment, collection, globals, dynamic. `TryResolve(..., out isSecret)` adds
  `(VariableScopeKind.Data, ListSource(Data ?? []))` right after `Local`. Data rows are never secret.
- `ScriptVariableOverlay`: keeps the initial `Data` list read-only. `TryGet(Layered, ...)` order becomes local, data, environment,
  collection, globals, dynamic. `List(Layered)` merges data at the same place. There is **no** `ScriptScope.Data`: scripts read data
  through the layered view and `yaapic.iterationData` (M6-09). `ToScopes()` carries `Data` through unchanged, so the resolver of
  the main request still sees it after pre-request scripts.
- `VariableContext` (popup and hints): a `Data` row is listed with the label `Strings.VariablesScopeData` ("Iteration data"),
  read-only (not editable from the popup), between local and environment. `VariablePopupViewModel` maps `Data` to the same label;
  `Data` is never offered as a writable scope. Search every `switch` on `VariableScopeKind` in `src/` (`rtk grep -rn "VariableScopeKind\." src --include=*.cs`)
  and handle `Data` where the compiler or a default branch would otherwise mis-handle it.
- String (EN and FR): `variables.scopeData` = "Iteration data".

## Out of scope

Parsing data files (M6-07, M6-08), the script API (M6-09 to M6-12), the runner.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: precedence (local beats data, data beats environment, collection and globals), `TryResolve`
  reports `Data`, `CreateSource` resolves a request URL `{{user}}` from the data list, `VariableScopes.None` and existing call sites
  unchanged (data null); `ScriptVariableOverlay` layered get/list include data in the right place, `ToScopes()` keeps it, writes
  through `Layered` still go to local and never touch data.
- [x] Tests in `Yaapic.ViewModels.Tests`: a hint for a data variable shows the "Iteration data" label and is not writable.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the iteration data variable layer`
