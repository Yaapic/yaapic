# M4-70: Avalonia: OAuth 2.0 in the Auth tab

Status: done
Platform: unix
Depends on: M4-43, M4-61

## Goal

The Auth tab of the Avalonia request editor offers OAuth 2.0 with its fields, the Get new access token button and the token status, matching M4-62.

## Design decisions

- Read first: M4-62 (the contents of the view are identical, only the controls differ), `src/Yaapic.App.Avalonia/Views/AuthEditorView.axaml(.cs)`, `VaultWindow.axaml` for the masked field
  style, the Avalonia Basic and API key groups, `Presentation/*Map.cs` for the converter style.
- `AuthEditorView` kind selector shows the eight kinds; new `Views/OAuth2EditorView.axaml` (`UserControl`, `x:DataType="vm:OAuth2EditorViewModel"`, compiled bindings only) with the same
  fields, visibility flags, hints, token block, progress indicator and cancel button as M4-62. Masked fields use `PasswordChar` or the existing masked `TextBox` class.
- Strings through the existing `Strings` access pattern of the Avalonia views; tokens through the existing brush resources (no literal colours); fonts and spacing as the sibling groups.

## Out of scope

JWT, Digest, AWS (M4-71), a token viewer.

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly (kill it afterwards).
- [ ] Runtime smoke on macOS or Linux, as in M4-62 (client-credentials send, status, clear, authorization-code button opening the browser and returning through the loopback URL). If the host is
  Windows or no display is available, say so in the Notes and tick only the build and AOT items (as the other Avalonia cards did).
- [x] Both themes use tokens only; strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add OAuth 2.0 authentication to the Auth tab`

## Notes

- Host is Windows: the AOT-published exe was started and stayed up for 6 s, then killed (not driven; no OAuth round trip, no theme check). The runtime smoke item stays unticked for a macOS or Linux run.
