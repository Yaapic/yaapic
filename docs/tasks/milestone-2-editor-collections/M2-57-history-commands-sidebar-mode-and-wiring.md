# M2-57: History commands, sidebar mode and shell wiring

Status: todo
Platform: any
Depends on: M2-56, M2-55, M2-35

## Goal

Open, delete and clear history entries, switch the sidebar between collections and history, and wire it all.

## Design decisions

- `TabsViewModel` gains `void OpenAsNew(HttpRequestDefinition definition)`: opens a **new unsaved tab** (`isNew: true`)
  with a copy of the definition that has a fresh `Guid` id and activates it.
- `HistoryViewModel` gains `HistoryViewModel(IHistoryStore store, TimeProvider time, TabsViewModel tabs, IDialogService
  dialogs)` (extend the M2-56 constructor): `[RelayCommand] void Open(HistoryItemViewModel item)` (`tabs.OpenAsNew`),
  `[RelayCommand] Task DeleteAsync(HistoryItemViewModel item)` (no confirmation, removes from store and list),
  `[RelayCommand] Task ClearAsync()` (`ConfirmDeleteAsync("the whole history")`; on true clears the store and the list).
  Storage errors go to `LastError`.
- `enum SidebarMode { Collections, History }` and `ShellViewModel`: parameter `HistoryViewModel history` (after
  `collections`), property `History`, observable `SidebarMode Mode` (default `Collections`) plus `[RelayCommand]
  ShowCollections()` and `ShowHistory()`; `InitializeAsync` awaits `History.RefreshAsync()`.
- `AppComposition.CreateShell`: `FileHistoryStore(workspaceRoot)`, `TimeProvider.System`, the history view model, and in
  the tab factory `editor.Sent += (_, e) => _ = history.RecordAsync(e.Request, e.Report)` (create the `HistoryViewModel`
  variable before the `TabsViewModel`, it needs `tabs`: declare it first as a local assigned after the tabs exist, the
  lambda captures the variable).
- Update the tests that build `ShellViewModel`, `HistoryViewModel` and `AppCompositionTests`.

## Out of scope

Views (M2-58, M2-59), keyboard shortcuts for the mode switch.

## Acceptance criteria

- [ ] Tests: `OpenAsNew` creates a new tab with a different id, dirty/new, request content equal; open from history
      activates it; delete; clear confirmed and refused; `Mode` switching commands; an `AppComposition` test sends a request
      through the shell (fake transport not possible there: use the loopback server of the end-to-end tests) and finds the
      entry in `Shell.History` and in `.yaapic/history.jsonl` of the temp workspace.
- [ ] 0 warnings; architecture tests green.

## Suggested commit message

`feat: add history commands, sidebar mode and shell wiring`
