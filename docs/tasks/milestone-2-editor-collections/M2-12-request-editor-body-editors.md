# M2-12: Request editor ViewModel: body editors

Status: todo
Platform: any
Depends on: M2-03, M2-10, M2-11

## Goal

The editor ViewModel edits url-encoded fields, multipart fields (with file fields) and a binary file path.

## Design decisions

- `BodyKinds` = `[None, Raw, UrlEncoded, FormData, Binary]`.
- `UrlEncodedFields` is a `KeyValueEditorViewModel` (M1-16). New `FormDataEditorViewModel` in `Yaapic.ViewModels.Editing`
  with `ObservableCollection<FormDataRowViewModel> Rows` (always one trailing empty placeholder row, like the key/value
  editor), `event Changed`, `Load(IEnumerable<FormField>)`, `ToFields()` (empty rows dropped) and `ActiveCount`.
  `FormDataRowViewModel`: observable `Enabled`, `Key`, `Value`, `Kind` (`FormFieldKind`), `bool IsFile`,
  `bool IsEmpty`, `RemoveCommand`-style removal handled by the editor as in `KeyValueEditorViewModel`, and
  `Task PickFileAsync()` (a relay command) that calls `IDialogService.PickFileAsync("Select a file")` and sets `Value`
  when a path comes back. Changing `Kind` to Text keeps `Value`.
- `RequestEditorViewModel` gets `string BinaryFilePath` (observable) and `PickBinaryFileCommand` (same dialog call).
  `FromDefinition`/`CreateNew` gain a trailing optional parameter `IDialogService? dialogs = null`; with null the pick
  commands do nothing. Update `TabsViewModel` factory callers (`AppComposition`, tests) to pass the dialog service.
- `ToDefinition` writes `Body = new RequestBody(Kind, ContentType, BodyText, fields, FilePath)` where `fields` are the
  url-encoded entries (as text fields) when `Kind` is `UrlEncoded`, the multipart fields when `FormData`, else `[]`;
  `FilePath` is `BinaryFilePath` when `Kind` is `Binary`, else `""`. `FromDefinition` fills the editor matching the
  body kind. Their `Changed` events refresh dirty/title.

## Out of scope

Views (M2-17, M2-22), resolving or sending (M2-05, M2-07), drag and drop of files.

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests`: each body kind round trips through `FromDefinition`/`ToDefinition`; editing
      a form row, switching a row to File, adding/removing rows, or changing the binary path makes the tab dirty and
      reverting cleans it; the placeholder row never appears in `ToFields`; `PickFileAsync` sets the value from
      `FakeDialogService.PickFileAnswer` and keeps it on cancel (null); only the active kind's fields are written.
- [ ] 0 warnings.

## Suggested commit message

`feat(viewmodels): add url-encoded, form-data and binary body editors`
