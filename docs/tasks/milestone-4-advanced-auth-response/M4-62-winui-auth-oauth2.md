# M4-62: WinUI: OAuth 2.0 in the Auth tab

Status: done
Platform: windows
Depends on: M4-43, M4-61

## Goal

The Auth tab of the request editor offers OAuth 2.0 with its fields, the Get new access token button and the token status.

## Design decisions

- Read first: `src/Yaapic.App.WinUI/Views/AuthEditorView.xaml` and `.xaml.cs` (kind `ComboBox`, per-kind field groups, `VisibilityMap`, index maps), M2-18 (how
  the Auth tab was built), M4-40, M4-41, M4-43, the vault dialog for the masked-field style.
- `AuthEditorView` kind `ComboBox` shows the eight kinds (index maps in code-behind as today; names from `Strings.AuthKind*`).
- New `Views/OAuth2EditorView.xaml` (`UserControl`, `ViewModel` of type `OAuth2EditorViewModel`, `x:Bind` only), shown for the OAuth2 kind in `AuthEditorView`:
  - a `ComboBox` for the grant, then the fields stacked: Token URL; Authorization URL and Redirect URI (`ShowAuthorizationUrl` / `ShowRedirectUri`); Client ID; Client
    secret (`PasswordBox`, two-way); Username and Password (`ShowUserCredentials`); Scope; Audience; Client authentication `ComboBox`; Token placement `ComboBox` with the
    prefix or parameter name field depending on `ShowPlacementName`.
  - Every text field has the placeholder `Use {{variable}} to keep secrets out of the file` (existing string) where the field can hold a secret.
  - The `Hints` list below the fields as `InfoBar`-style warning rows (severity Warning, the existing component of M3 if there is one: reuse), not blocking.
  - A token block at the bottom: the status text, a primary "Get new access token" button (`GetNewTokenCommand`; shows a `ProgressRing` and a "Cancel" button while
    `IsGettingToken`, with `Strings.OAuth2WaitingForBrowser`), a "Clear token" button, and the error text in the error colour when `TokenError` is set.
  - Follow the layout, spacing and styles of the Basic and API key groups (M1-36/M1-37 control styles); design tokens only, no literal colours.
- The Auth tab dot badge (`Auth.IsActive`) keeps working for the new kinds.

## Out of scope

JWT, Digest and AWS fields (M4-63), copying the token, a token viewer.

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards).
- [x] Manual smoke on the published exe (stub server from `tools/screenshots` or `httpbin`-like local stub that implements a token endpoint; record what was used): with a client-credentials
  configuration the send succeeds and the API stub sees `Authorization: Bearer <token>`; the status shows the expiry; Clear token then Send fetches again; with the authorization-code grant a
  send without a token shows the "Get a new access token first" error and the button opens the system browser (the stub authorization page redirects to the loopback URL and the status
  becomes valid). Record anything not run.
- [x] Both themes use tokens only; strings come from `Strings` (EN and FR present).
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add OAuth 2.0 authentication to the Auth tab`
