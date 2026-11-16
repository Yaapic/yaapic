# M4-71: Avalonia: JWT, Digest and AWS Signature v4 in the Auth tab

Status: todo
Platform: unix
Depends on: M4-70

## Goal

The Avalonia Auth tab offers the JWT, Digest and AWS Signature v4 kinds, matching M4-63.

## Design decisions

- Read first: M4-63 (field list, hints, the Digest explanatory line, the multi-line monospace fields), M4-70, `AuthEditorView.axaml`.
- New `Views/JwtEditorView.axaml` and `Views/AwsV4EditorView.axaml` (compiled bindings, `x:DataType` set); Digest reuses the Basic group plus the `DigestHint` text.
- Multi-line fields: `TextBox` with `AcceptsReturn="True"`, monospace font resource, 6 or 3 lines; the expiry field is a `NumericUpDown` (minimum 0, integer format) like the timeout field of the
  Settings tab.
- Visibility by kind and flags as in the existing groups.

## Out of scope

OAuth 2.0 (M4-70), the decoder (M4-72).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly (kill it afterwards).
- [ ] Runtime smoke on macOS or Linux as in M4-63 (JWT, Digest, AWS against a local stub); if not possible, say so in the Notes and tick only the build and AOT items.
- [ ] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add JWT, Digest and AWS Signature v4 to the Auth tab`
