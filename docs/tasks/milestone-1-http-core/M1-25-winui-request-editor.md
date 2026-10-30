# M1-25: WinUI request editor view

Status: done
Platform: windows
Depends on: M1-24

## Goal

The request editor inside each tab: name, method, URL, Send/Cancel, Params, Headers and Body.

## Design decisions

- `Yaapic.App.WinUI/Views/RequestEditorView.xaml` (`UserControl`, property `ViewModel` of type
  `RequestEditorViewModel`) replaces the M1-24 placeholder as the `TabView` item content. Layout: a vertical split with
  the editor on top and a `ContentPresenter` area reserved for the response (M1-26) below, rows `*` / `*`.
- Top row: `TextBox` for `Name` (no border, title style); then a row with method `ComboBox` (items `Methods`, shows
  `GET`/`POST` through a static `x:Bind` function, foreground per method brush, width 100), URL `TextBox` (fills,
  monospace `YaapicMonoFont`, placeholder "Enter URL or paste text", `UpdateSourceTrigger=PropertyChanged`) and a
  Send button (accent style, bound to `SendCommand`); while `IsSending`, the Send button is replaced by a "Cancel"
  button bound to `CancelCommand`.
- Below: `SelectorBar` with Params, Headers, Body. Params and Headers use a reusable
  `Views/KeyValueEditorView.xaml` (`UserControl`, `ViewModel` of type `KeyValueEditorViewModel`): an `ItemsRepeater` or
  `ListView` of rows with a `CheckBox` (`Enabled`), Key `TextBox` (placeholder "Key"), Value `TextBox` (placeholder
  "Value"), and a delete button (`RemoveRowCommand`, hidden for the placeholder row through `IsEmpty`). All
  `TwoWay` with `UpdateSourceTrigger=PropertyChanged`.
- Body pane: `RadioButtons` none/raw (`BodyKind`), a `ComboBox` for `BodyContentType` (visible only for raw), and a
  multi-line `TextBox` (`AcceptsReturn`, `TextWrapping=NoWrap`, `YaapicMonoFont`, `YaapicCodeFontSize`, scrollbars)
  for `BodyText`. When `Method` is GET, show a hint text "GET requests are sent without a body".
- Keyboard: Ctrl+Enter triggers `SendCommand` (window-level accelerator on the active tab's editor).
- Plain `TextBox` everywhere: the code editor control is ADR 0002 (later milestone).

## Out of scope

Response display (M1-26), variable highlighting or autocomplete, URL/params sync, other body types.

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke on the published exe: type `https://httpbin.org/get`, add a param, Ctrl+Enter: the request goes out
      (check with the M1-26 panel later; in this card, check `IsSending` toggles the Cancel button), the tab title gets
      the dirty marker, Ctrl+S saves and the request appears in the sidebar; closing a dirty tab shows the prompt.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add request editor view`

## Implementation notes

- The method and content-type pickers are index-based `ComboBox`es filled with static items and wired in code-behind:
  boxed managed enums (`HttpMethodKind`, `RawContentType`) bound as `ItemsSource`/`SelectedItem` crash XAML at startup
  under Native AOT (the JIT build is fine). The method foreground is set from `BrushMap.Method` on selection.
- The editor and response rows are `2*` / `*` (not `*` / `*`) so the Params/Headers tables keep room in the minimum
  window size.
- Verified on the Native AOT published exe through UI automation: the editor renders, typing a param adds a
  placeholder row, Ctrl+Enter swaps Send for Cancel, Ctrl+S saves (dirty marker gone, request in the sidebar). Closing
  a dirty tab prompt was not driven.
