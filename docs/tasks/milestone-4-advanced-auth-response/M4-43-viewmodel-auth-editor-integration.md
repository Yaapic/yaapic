# M4-43: ViewModel: the auth editor offers the new kinds

Status: todo
Platform: any
Depends on: M4-08, M4-41, M4-42

## Goal

`AuthEditorViewModel` lists OAuth 2.0, JWT, Digest and AWS Signature v4, exposes their editors and builds the right `RequestAuth`.

## Design decisions

- Read first: `AuthEditorViewModel.cs`, `RequestEditorViewModel.cs` (where `Auth` is created, `ToAuth`, the `Changed` handler that marks the tab
  dirty, `FromDefinition`/`CreateNew` factories and their optional parameters), M4-40 to M4-42, the existing tests of both.
- `AuthEditorViewModel.Kinds` becomes `[None, Basic, Bearer, ApiKey, OAuth2, Jwt, Digest, AwsV4]`.
- New read-only properties `OAuth2`, `Jwt`, `AwsV4` (the three editors, created in the constructor). `Digest` uses the existing `Username` and
  `Password`.
- `Load(RequestAuth)` loads the sub-editors from `auth.OAuth2 ?? default` etc. (still no `Changed`); `ToAuth()` returns the existing positional
  record `with` the sub-settings **only for the active kind** (`OAuth2`, `Jwt`, `AwsV4` stay null for other kinds so files stay clean), and for
  `Digest` the username and password. Switching the kind keeps the edited values of the other kinds in memory until the tab closes (so a
  user switching back does not lose them) but `ToAuth` writes only the active one.
- The sub-editors' `Changed` events are forwarded to `AuthEditorViewModel.Changed`.
- `IsActive` stays `Kind != None`.
- `RequestEditorViewModel`: pass the token-service wiring through its factories as one optional parameter `IOAuth2TokenService? oauth2Tokens = null`
  (like `ScriptSendServices` in M3-43: null keeps today's behaviour, the OAuth 2.0 token buttons stay disabled); give the editor the settings
  resolver `settings => resolved settings` using the same `IVariableScopeProvider` source as a send (the resolver of M4-09 exposes a
  `ResolveOAuth2(OAuth2Settings, IVariableSource)` helper: add it, a thin wrapper over the substitution code used there).
- Strings for the kind names (`AuthKindOAuth2`, `AuthKindJwt`, `AuthKindDigest`, `AuthKindAwsV4`), EN and FR.

## Out of scope

Views, composition.

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests`: the kind list; for each new kind a definition loads and `ToAuth` returns an equal `RequestAuth`;
  non-active kinds are null in the output; switching kinds keeps in-memory edits; `Changed` forwarded from each sub-editor and not raised by
  `Load`; the request editor's dirty state changes when an OAuth 2.0 field is edited and not on load; with a fake token service the get-token command
  receives settings with variables substituted; existing auth tests unchanged.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): offer OAuth 2.0, JWT, Digest and AWS auth in the editor`
