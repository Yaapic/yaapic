# M1-21: Saved requests list ViewModel

Status: done
Platform: any
Depends on: M1-20

## Goal

The sidebar list of saved requests: filter, open, delete, new.

## Design decisions

- `partial class RequestListViewModel : ObservableObject` in `Yaapic.ViewModels.Requests`, constructor
  `(IRequestStore store, TabsViewModel tabs, IDialogService dialogs)`.
- `sealed record RequestListItem(Guid Id, string Name, HttpMethodKind Method, string Url)` with `string MethodText`
  (`GET`/`POST`).
- `ObservableCollection<RequestListItem> Items` (filtered view), observable `FilterText`.
- `Task RefreshAsync()`: loads `store.ListAsync`, keeps the full list in a private field, applies the filter. Filter:
  trimmed, ordinal-ignore-case `Contains` on `Name` or `Url`; empty filter shows all. Store order is kept.
- Subscribes to `tabs.RequestSaved` and calls `RefreshAsync`.
- `[RelayCommand] Task Open(RequestListItem item)`: loads the full definition from the cached list and calls
  `tabs.Open`.
- `[RelayCommand] Task Delete(RequestListItem item)`: `ConfirmDeleteAsync(item.Name)`; on true,
  `tabs.CloseWithoutPrompt(item.Id)`, `store.DeleteAsync`, `RefreshAsync`.
- `[RelayCommand] void NewRequest()` -> `tabs.NewTab()`.

## Out of scope

Folders/collections (M2), drag and drop, multi-select, duplicate, rename in place (rename = edit the name in the tab
and save).

## Acceptance criteria

- [x] Tests: refresh lists store content; filter by name and by URL, case-insensitive; clearing the filter; saving a tab
      refreshes the list; open activates a tab; delete confirmed removes from store and closes the tab; delete refused
      changes nothing.
- [x] 0 warnings.

## Suggested commit message

`feat(viewmodels): add saved requests list view model`
