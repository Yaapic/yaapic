# M2-58: WinUI sidebar mode switch and history list

Status: done
Platform: windows
Depends on: M2-57, M2-36

## Goal

The WinUI sidebar switches between the collection tree and the request history.

## Design decisions

- Read the rail and sidebar of `MainWindow.xaml` (M1-38) and `design/mockups/MainWindow.dc.html` (rail) first. The rail
  entries "Collections" (`collection` icon) and "History" (`history` icon, from `design/xaml/Yaapic.Icons.WinUI.xaml`)
  call `ShowCollectionsCommand` / `ShowHistoryCommand`; the selected one shows the active indicator of the rail style
  from `ViewModel.Mode`.
- Sidebar content swaps by `Visibility` (functions on `SidebarMode`): the collection tree (M2-36) or the history view
  `Views/HistoryView.xaml` (`UserControl`, `ViewModel` of type `HistoryViewModel`): filter `TextBox` bound to
  `FilterText` (placeholder "Filter history"), a "Clear" button on `ClearCommand`, and the groups as an `ItemsRepeater` of
  groups (header text `Title`, then an `ItemsRepeater` of items; no `CollectionViewSource` grouping, which is
  reflection-based). Item row: method text with `BrushMap.Method`-style colour, URL (trimmed with ellipsis), second line
  with status chip text (`StatusText`, colour by `StatusCategory` through the existing mapping, the error style when `IsFailed`) and `TimeText` +
  `DurationText`; click opens (`OpenCommand`), context menu "Open" / "Delete". Empty state "No requests sent yet"
  when `IsEmpty`.
- `History.LastError` shows in the existing error `InfoBar`.

## Out of scope

Search inside request bodies, pinning entries, keyboard shortcuts for the mode switch.

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke on the published exe: send two requests, switch to History: both appear under "Today", newest first;
      click one: a new unsaved tab opens with the same request; delete one; clear (confirmation); restart: the
      remaining history is still there.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add sidebar mode switch and history list`
