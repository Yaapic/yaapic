# M4-40: ViewModel: OAuth 2.0 editor

Status: todo
Platform: any
Depends on: M4-05, M4-22

## Goal

A view model edits the fields of an `OAuth2Settings`, knows which fields apply to the chosen grant, and hints at literal secrets.

## Design decisions

- Read first: ADR 0013, `src/Yaapic.ViewModels/Requests/AuthEditorViewModel.cs` (the `_loading` / `Changed` pattern, `ObservableProperty`),
  `KeyValueEditorViewModel` or any sub-editor view model for the style, the secret hint wording in the existing variable editors.
- `src/Yaapic.ViewModels/Requests/OAuth2EditorViewModel.cs`, `public sealed partial class`, `ObservableObject`:
  - observable properties for every field of `OAuth2Settings` (same names), `Load(OAuth2Settings)` that raises no `Changed`, `ToSettings()`,
    event `Changed` raised on every edit (not on `Load`), and static lists `Grants`, `ClientAuthentications`, `Placements`.
  - Visibility flags (raise `PropertyChanged` when `Grant` changes): `ShowAuthorizationUrl` and `ShowRedirectUri` (AuthorizationCode),
    `ShowUserCredentials` (Password), `ShowClientSecret` (all grants), `ShowPlacementName` (`HeaderPrefix` when `Header`, `QueryParameterName`
    when `Query`).
  - `IReadOnlyList<string> Hints` recomputed on every change: a `Strings.SecretLiteralHint("Client secret")` entry per secret-like field
    (`ClientSecret`, `Password`) whose value is non-empty and does not consist of a single `{{variable}}` token (use `VariableScanner` to detect
    that); `Strings.OAuth2RedirectUriInvalid` when `AuthorizationCode` is selected and `OAuth2AuthorizationRequest.TryGetListenAddress` fails.
- The view model does not call the network; token actions are M4-41.
- Strings: `SecretLiteralHint` (shared by the other editors; check M3 for an existing equivalent and reuse it), field labels and the grant names
  (`OAuth2GrantClientCredentials`, `...AuthorizationCode`, `...Password`, `OAuth2ClientAuthBasic`, `OAuth2ClientAuthBody`, placement names, field
  labels), EN and FR, regenerate `Strings.g.cs`.

## Out of scope

Token buttons and status (M4-41), the Auth editor integration (M4-43), views.

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests`: load/to-settings round trip; `Changed` count; no `Changed` on load; visibility flags per grant;
  hints for a literal secret, for `{{secret}}` (no hint), for empty (no hint); redirect URI hint only for the code grant.
- [ ] Architecture tests green (ViewModels never reference an App project), 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the OAuth 2.0 editor view model`
