# M4-65: WinUI: JWT decoder dialog

Status: todo
Platform: windows
Depends on: M4-49, M4-61

## Goal

A dialog where the user pastes a JWT and sees its header, payload, claims and expiry.

## Design decisions

- Read first: `Views/ConvertScriptDialog.cs` (a small dialog built in code, U-05) and `VaultDialog.xaml`, `Views/CodeEditorView.xaml(.cs)` (read-only mode, `EditorLanguage`),
  M4-49, M4-60.
- `Views/JwtDecoderDialog.xaml` (`JwtDecoderViewModel`): a multi-line `TextBox` for the token (placeholder "Paste a JWT"; a **Paste** button reads the clipboard text into `Input`);
  a status chip (`StatusText`, colour by `StatusKind` using the existing semantic tokens: success, error, warning, neutral); two read-only `CodeEditorView`s with
  `EditorLanguage="json"` for header and payload; a claims list (`Claims`: name, local time, relative time); the signature line; the permanent note `JwtNotVerifiedNote`.
  Close button only.
- The error text under the input in the error colour. `x:Bind` only; tokens only.
- Opened from the shell event `JwtDecoderRequested` (M4-60) next to the other dialogs.
- Also a "Decode JWT" entry in the context of the Auth tab is **not** added (palette only).

## Out of scope

Verifying signatures, editing and re-signing, decoding from the response panel.

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards).
- [ ] Manual smoke on the published exe: paste the RFC 7515 A.1 token: header and payload shown, status "No expiry"; paste a token with a past `exp`: "Expired"; paste garbage: the error
  text; the palette entry opens the dialog. Record anything not run.
- [ ] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the JWT decoder dialog`
