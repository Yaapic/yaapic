# M4-63: WinUI: JWT, Digest and AWS Signature v4 in the Auth tab

Status: todo
Platform: windows
Depends on: M4-62

## Goal

The Auth tab offers the JWT, Digest and AWS Signature v4 kinds with their fields.

## Design decisions

- Read first: M4-62 (same structure), `AuthEditorView.xaml`, M4-42, M4-43.
- `Views/JwtEditorView.xaml` (`JwtEditorViewModel`): algorithm `ComboBox`; Secret (`PasswordBox`) plus a "Secret is Base64 encoded" `CheckBox` when `UsesSecret`; Private key
  (multi-line `TextBox`, monospace, 6 lines, `AcceptsReturn`, the placeholder `Paste a PEM private key or use {{variable}}`) when `UsesPrivateKey`; Payload (multi-line monospace
  `TextBox`, 6 lines); Extra header fields (multi-line, 3 lines); "Add issued-at (iat)" `CheckBox`; "Expires in (seconds, 0 = never)" `NumberBox`; placement `ComboBox` with the
  prefix or the parameter name field (`ShowHeaderPrefix`, `ShowQueryParameter`); the `Hints` list as in M4-62.
- Digest: reuses the Basic group (username and password `PasswordBox`) with an explanatory line `Strings.DigestHint` ("The first request is a probe without a body; the server's challenge is
  then answered.") shown under it: add the string, EN and FR.
- `Views/AwsV4EditorView.xaml` (`AwsV4EditorViewModel`): Access key ID, Secret access key (`PasswordBox`), Session token (`PasswordBox`, optional), Region, Service; the `Hints` list.
- Visibility by the kind index as the existing groups do; `x:Bind` only; tokens only.

## Out of scope

OAuth 2.0 (M4-62), the JWT decoder dialog (M4-65).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards).
- [ ] Manual smoke on the published exe against a local stub: a JWT HS256 request sends `Authorization: Bearer <three-part token>` whose payload matches (check by pasting it
  into the decoder dialog once M4-65 is done, otherwise by eye in the stub log); changing the algorithm to RS256 swaps the secret field for the key field; a Digest request against a stub that
  demands Digest returns 200 (a wrong password returns 401); an AWS request shows the `Authorization: AWS4-HMAC-SHA256 ...` header at the stub. Record anything not run.
- [ ] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add JWT, Digest and AWS Signature v4 to the Auth tab`
