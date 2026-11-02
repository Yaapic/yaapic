# M2-16: WinUI method picker and URL bar for all methods

Status: done
Platform: windows
Depends on: M2-11, M2-13

## Goal

The WinUI request editor offers the seven methods, colours each one, and shows the synced URL and params.

## Design decisions

- Read `Views/RequestEditorView.xaml(.cs)`, `Presentation/BrushMap.cs` and the M1-25 implementation notes first: enum
  `ItemsSource` bindings crash under Native AOT, so keep the existing index/`Tag`-based wiring and extend it.
- The method control lists `RequestEditorViewModel.Methods` (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS), built in
  code from `HttpMethodKindExtensions.ToText`; selecting sets `ViewModel.Method`; the label shows `MethodText`.
- `BrushMap.Method(HttpMethodKind)` returns `YaapicMethodGetBrush`, `YaapicMethodPostBrush`, `YaapicMethodPutBrush`,
  `YaapicMethodPatchBrush`, `YaapicMethodDeleteBrush`, `YaapicMethodHeadBrush` or `YaapicMethodOptionsBrush` (all exist
  in the generated resources). Every other place that colours or labels a method (tab header, sidebar rows) uses
  `MethodText`/`BrushMap.Method`, so check `MainWindow.xaml` and `ListMap.cs` for hard-coded GET/POST logic and replace
  it.
- The "GET requests are sent without a body" hint becomes "{METHOD} requests are sent without a body" shown when
  `AllowsBody` is false.
- The URL `TextBox` stays bound to `Url` (two-way, `PropertyChanged`); the Params table follows through the ViewModel
  (M2-13). The Params tab header badge shows `QueryParams.ActiveCount`.
- Follow the restyled look (M1-41) and the method colours of `design/mockups/MainWindow.dc.html`.

## Out of scope

Body pane (M2-17), Auth and Settings tabs (M2-18).

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke on the published exe: choose each method, the label colour changes; typing `?a=1&b=2` in the URL fills
      two Params rows; unchecking one removes it from the URL; the hint shows for GET, HEAD and OPTIONS only.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): pick any HTTP method and sync the URL with the params table`
