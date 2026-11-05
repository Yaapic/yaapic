# M2-36: WinUI collections tree

Status: done
Platform: windows
Depends on: M2-35, M2-16

## Goal

The WinUI sidebar shows the collection tree instead of the flat request list.

## Design decisions

- Read `MainWindow.xaml` (sidebar of M1-38) and `design/mockups/MainWindow.dc.html` (sidebar list, filter) first.
- Replace the request `ListView` with a `TreeView` whose `ItemsSource` is `ViewModel.Collections.Roots`; each item is a
  `TreeViewItem` in a `DataTemplate` (`x:DataType` = `CollectionNodeViewModel`) with `ItemsSource` = `Children` and
  `IsExpanded` two-way. Folder row: icon (`collection` for `IsCollection`, `folder` otherwise, from
  `design/xaml/Yaapic.Icons.WinUI.xaml`) + name; request row: method text (width 44, `YaapicCodeFontSize`, brush from
  `BrushMap.Method` through `x:Bind`) + name. A tooltip shows the URL on request rows. Choose the template with a
  `TreeViewItem` style trigger by `IsFolder` through `Visibility` functions (`ListMap`/`VisibilityMap`); no
  `DataTemplateSelector` that needs reflection.
- Invoking a node (`ItemInvoked`) calls `Collections.OpenCommand` with the node. The filter `TextBox` binds to
  `Collections.FilterText` (placeholder "Filter requests"). The empty state text "No collections yet" with the "New
  request" button shows when `Collections.IsEmpty` (the buttons still use `Tabs.NewTab` for now; M2-37 adds the rest).
- `Collections.LastError` shows in the existing error `InfoBar`.
- If CsWinRT reports AOT warnings for `ObservableCollection<CollectionNodeViewModel>`, fix them in the App project only,
  as in M1-24, and record the fix in Notes.

## Out of scope

Context menus and prompts (M2-37), drag and drop, keyboard navigation beyond the `TreeView` defaults.

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke on the published exe, with a workspace holding `requests/demo/folder.yaml`,
      `requests/demo/sub/folder.yaml` and requests at each level (create the files by hand): the tree shows them,
      expanding/collapsing works, clicking a request opens its tab, the filter narrows the tree.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): show collections as a tree in the sidebar`

## Notes

- No CsWinRT/AOT warnings for `ObservableCollection<CollectionNodeViewModel>`; no fix needed. Tree rows use `YiCollection`/`YiFolder` path data (no new resource keys). Smoke done with UI Automation on the AOT exe: expand/collapse, request opens its tab, filter narrows the tree.
