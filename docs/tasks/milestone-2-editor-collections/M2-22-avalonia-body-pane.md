# M2-22: Avalonia body pane: all body kinds

Status: done
Platform: unix
Depends on: M2-12, M2-20, M2-21

## Goal

Same as M2-17, in `Yaapic.App.Avalonia`; read M2-17 first for behaviour and texts.

## Design decisions

- `Views/RequestEditorView.axaml` Body tab: body mode `RadioButton`s (None, Raw, x-www-form-urlencoded, form-data,
  Binary) wired by index in code-behind, content-type picker with five items (JSON, Text, XML, HTML, JavaScript).
- URL-encoded reuses `KeyValueEditorView`; new `Views/FormDataEditorView.axaml` (compiled bindings, `x:DataType` =
  `FormDataEditorViewModel`) with the same row layout as M2-17 (`ItemsControl`, Text/File `ComboBox` by index, Browse
  button on `PickFileCommand`, delete button hidden on the placeholder row).
- Binary: read-only `TextBox` for `BinaryFilePath` + "Select file" button; empty text "No file selected".
- The pane is replaced by the no-body hint when `AllowsBody` is false.

## Out of scope

Same as M2-17.

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke: same checks as M2-17 on the host OS.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): edit url-encoded, form-data and binary bodies`
