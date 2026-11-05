# M2-35: Shell and composition: collections

Status: done
Platform: any
Depends on: M2-31, M2-34

## Goal

`ShellViewModel` exposes the collection tree and `AppComposition` builds it on the file stores.

## Design decisions

- `ShellViewModel` gains the constructor parameter `CollectionTreeViewModel collections` (inserted after `requests`) and the
  property `Collections`. `InitializeAsync` also awaits `Collections.RefreshAsync()` right after `Requests.RefreshAsync()`.
  `Requests` (the flat list, M1-21) stays: session restore opens tabs through it and the command palette (M2-61) reads it.
  Do not remove it.
- `AppComposition.CreateShell` creates `FileCollectionStore(workspaceRoot)` and
  `CollectionTreeViewModel(collectionStore, requestStore, tabs, dialogs)`.
- Update every test that builds a `ShellViewModel` and `AppCompositionTests`.
- No view changes: the sidebar keeps showing the flat list until M2-36 and M2-38.

## Out of scope

Views, history, globals.

## Acceptance criteria

- [x] Tests: `ShellViewModelTests` assert `Collections` is refreshed during initialization (a nested request in the
      store appears in `Collections.Roots`); `AppCompositionTests` assert the tree of a temp workspace that contains a
      collection with one request after `InitializeAsync`; all M1 shell tests green.
- [x] 0 warnings; architecture tests green.

## Suggested commit message

`feat: expose the collection tree from the shell`
