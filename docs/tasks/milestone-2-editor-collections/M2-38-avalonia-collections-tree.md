# M2-38: Avalonia collections tree

Status: todo
Platform: unix
Depends on: M2-35, M2-21, M1-57

## Goal

Same as M2-36, in `Yaapic.App.Avalonia`; read M2-36 first for behaviour and texts.

## Design decisions

- Replace the sidebar request list in `MainWindow.axaml` with a `TreeView`, `ItemsSource` = `Collections.Roots`, item
  template `TreeDataTemplate` (`DataType` = `CollectionNodeViewModel`, `ItemsSource` = `Children`), compiled bindings.
  `TreeViewItem.IsExpanded` is bound two-way to the node's `IsExpanded` through the item container theme/style.
- Folder row: icon + name; request row: method text coloured with `BrushMap.MethodBrush` + name, URL in the tooltip;
  row kind chosen by `IsVisible` bound to `IsFolder` / its negation (two `StackPanel`s in the template), no template
  selector.
- Selecting or double-tapping a request node calls `Collections.OpenCommand`; tapping a folder toggles it. The filter
  `TextBox` binds to `Collections.FilterText`; empty state when `Collections.IsEmpty`; `Collections.LastError` in the
  existing error banner.

## Out of scope

Menus and prompts (M2-39), drag and drop.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke: same checks as M2-36 on the host OS.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): show collections as a tree in the sidebar`
