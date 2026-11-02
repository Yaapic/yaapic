# M2-39: Avalonia tree context menus and prompts

Status: todo
Platform: unix
Depends on: M2-38, M2-20

## Goal

Same as M2-37, in `Yaapic.App.Avalonia`; read M2-37 first for behaviour and texts.

## Design decisions

- Sidebar header "New" `Button` with a `Flyout`/`MenuFlyout`: "New request" and "New collection".
- `ContextMenu` on the tree item (right-click, Menu key) with the same entries per node kind as M2-37, including the
  "Move to" `MenuItem` whose sub-items are rebuilt in the `Opening` handler from `Collections.GetMoveTargets(node)`;
  each item invokes the `CollectionTreeViewModel` command with the node (or `MoveRequest`) as parameter, set from the
  item's `DataContext` in code-behind.
- Keyboard: F2 rename, Delete delete (Cmd+Backspace on macOS).

## Out of scope

Same as M2-37.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke: same checks as M2-37 on the host OS, workspace under `~/Yaapic/Default`.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add tree context menus and prompts for collections`
