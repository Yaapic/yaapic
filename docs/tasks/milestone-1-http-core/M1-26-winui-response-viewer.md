# M1-26: WinUI response viewer

Status: done
Platform: windows
Depends on: M1-25

## Goal

Show the response of the active tab: status, time, size, Pretty/Raw body, headers, errors and warnings.

## Design decisions

- `Yaapic.App.WinUI/Views/ResponsePanelView.xaml` (`UserControl`, `ViewModel` of type `ResponsePanelViewModel`), placed
  in the response area of `RequestEditorView` and bound to `ViewModel.Response`.
- States (`State`): `Empty` -> centered text "Send a request to see the response"; `Sending` -> `ProgressRing` and
  "Sending..."; `Failed` -> `InfoBar` severity Error with `ErrorMessage`; `Completed` -> the panel below.
- Header line: status chip (`StatusText`, background/foreground by `StatusCategory`: Success ->
  `YaapicSuccessBackgroundBrush`/`YaapicSuccessBrush`, Redirect -> Info, ClientError -> Warning, ServerError -> Danger,
  Informational -> `YaapicTextSecondaryBrush` text only), then `DurationText` and `SizeText` in secondary text.
- `UnresolvedWarning`, when not null, shows as an `InfoBar` severity Warning above the body (all states except Empty).
- `SelectorBar` Body / Headers. Body: `RadioButtons` (`MaxColumns="2"`) "Pretty" / "Raw" bound to `ViewMode`
  (index mapping through a static `x:Bind` function with `BindBack`); a read-only multi-line `TextBox` (`IsReadOnly`, `YaapicMonoFont`, `YaapicCodeFontSize`,
  `TextWrapping=NoWrap`, both scrollbars, selectable) bound to `BodyText`; when `IsBinary`, show `BinaryText` instead.
  Headers: two-column list (name, value), selectable text.
- All mapping (state to visibility, category to brushes) through static `x:Bind` functions in `Presentation/BrushMap.cs`
  or a sibling `Presentation/VisibilityMap.cs`.

## Out of scope

Syntax highlighting, copy/save buttons, search, image preview, cookies, timeline.

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke on the published exe (only start and clean exit verified; the scenarios below were not driven): GET `https://httpbin.org/json` shows `200 OK`, time, size and indented JSON;
      Raw shows the original text; Headers tab lists headers; `https://httpbin.org/status/404` shows a warning-coloured
      chip; `http://localhost:1` shows the connection error; a URL with `{{missing}}` shows the unresolved warning;
      Cancel during `https://httpbin.org/delay/10` shows "Request canceled".
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add response viewer`
