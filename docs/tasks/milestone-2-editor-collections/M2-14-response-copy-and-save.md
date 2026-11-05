# M2-14: Response panel ViewModel: copy and save body

Status: done
Platform: any
Depends on: M2-10, M2-12

## Goal

The user can copy the response body to the clipboard or save it to a file.

## Design decisions

- `ResponsePanelViewModel` gets a trailing optional constructor parameter `IDialogService? dialogs = null`;
  `RequestEditorViewModel` passes the `dialogs` it received in M2-12.
- It keeps the last response body bytes and content type from `ShowReport` (cleared by `Clear` and `ShowSending`).
- `[RelayCommand] CopyBodyAsync()`: `SetClipboardTextAsync(BodyText)`; `CanExecute` false when the state is not a
  response, or `IsBinary`.
- `[RelayCommand] SaveBodyAsync()`: `SaveFileAsync(suggestedName, bytes)`; `CanExecute` false without a response.
  Suggested name = `response.` + extension from the content type (`json`, `xml`, `html`, `js`, `txt` for any other
  `text/*`, otherwise `bin`). Saves the **raw** bytes, not the pretty-printed text.
- `CanExecute` changes are raised when the state changes.

## Out of scope

Views (M2-19, M2-24), copying headers, response search, saving a request together with its response.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests`: copy sends the displayed text (Pretty or Raw as shown); copy disabled for
      binary and for no response; save passes the raw bytes and the expected suggested name for json, xml, html,
      plain text and unknown types; commands disabled after `Clear`; with a null dialog service nothing throws.
- [x] 0 warnings.

## Suggested commit message

`feat(viewmodels): copy and save the response body`
