# M2-17: WinUI body pane: all body kinds

Status: todo
Platform: windows
Depends on: M2-12, M2-15, M2-16

## Goal

The Body tab of the WinUI editor edits none, raw, url-encoded, form-data and binary bodies.

## Design decisions

- Read the current Body pane in `RequestEditorView.xaml` first (body mode bar of M1-41, `CodeEditorView` of M1-44).
- Body mode bar (existing segmented/radio control) now has five modes, labels "None", "Raw", "x-www-form-urlencoded",
  "form-data", "Binary", bound by index to `BodyKind` through code-behind (no enum `ItemsSource`).
- Raw: unchanged, with the content-type picker listing JSON, Text, XML, HTML, JavaScript (code-behind items, index map,
  `CodeEditorView.Language` follows it).
- URL-encoded: the reusable `KeyValueEditorView` bound to `UrlEncodedFields`.
- Form-data: new `Views/FormDataEditorView.xaml` (`UserControl`, `ViewModel` of type `FormDataEditorViewModel`): rows of
  `CheckBox`, Key `TextBox`, a small Text/File toggle (`ComboBox` by index) bound to the row `Kind`, Value `TextBox`
  (when `IsFile` it is read-only and a "Browse" button runs the row's `PickFileCommand`), and a delete button hidden on
  the placeholder row. Same styles as `KeyValueEditorView` (M1-40).
- Binary: a read-only `TextBox` with `BinaryFilePath` and a "Select file" button (`PickBinaryFileCommand`); empty state
  text "No file selected".
- When `AllowsBody` is false the whole pane is replaced by the hint of M2-16. `x:Bind` only, `x:DataType` on every
  template, visibility through `Presentation/VisibilityMap.cs` functions.

## Out of scope

Drag and drop of files, body size warnings, JSON validation.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke on the published exe: POST `https://httpbin.org/post` with url-encoded `a=1`, then with form-data
      `a=1` plus a file field, then with a binary file: the response echoes each (httpbin shows `form`, `files`, `data`).
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): edit url-encoded, form-data and binary bodies`
