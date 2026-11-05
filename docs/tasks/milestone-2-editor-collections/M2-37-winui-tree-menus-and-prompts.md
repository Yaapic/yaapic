# M2-37: WinUI tree context menus and prompts

Status: done
Platform: windows
Depends on: M2-36, M2-15

## Goal

Create, rename, duplicate, move and delete collections, folders and requests from the WinUI sidebar.

## Design decisions

- Sidebar header: the existing "New" button becomes a split/`DropDownButton` with `MenuFlyout` items "New request"
  (`Collections.NewRequestCommand` with a null parameter) and "New collection" (`NewCollectionCommand`).
- Context menu (`MenuFlyout` on the tree item template, right-click and the Menu key):
  - Folder or collection: "New request", "New folder", "Rename", "Delete".
  - Request: "Rename", "Duplicate", "Delete".
  - Both: "Move to" submenu (`MenuFlyoutSubItem`), filled when the menu opens from
    `Collections.GetMoveTargets(node)` (one item per `FolderOption`, text = `Path`, click runs `MoveCommand`).
  Items call the matching `CollectionTreeViewModel` commands with the node as `CommandParameter` (set the parameter from
  the item's `DataContext` in code-behind; the `MenuFlyout` is outside the data template's visual tree).
- Prompts and delete confirmation come from the `IDialogService` already implemented (M2-15); nothing extra here.
- Keyboard: F2 renames the selected node, Delete deletes it.

## Out of scope

Drag and drop, folder settings (M2-48), multi-select.

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke on the published exe: create a collection "Demo", a folder "v1" inside it, a request in the folder;
      rename the request; duplicate it; move the duplicate to the top level; delete the folder (confirmation names its
      request count); restart: the structure on disk (`%USERPROFILE%\Yaapic\Default\requests`) matches the UI.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add tree context menus and prompts for collections`
