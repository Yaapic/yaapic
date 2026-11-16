# M4-41: ViewModel: OAuth 2.0 token actions and status

Status: todo
Platform: any
Depends on: M4-21, M4-23, M4-40

## Goal

The OAuth 2.0 editor can get a new token, clear it, and show its state; the authorization-code flow is cancelable.

## Design decisions

- Read first: ADR 0013 (When tokens are fetched), M4-21, M4-23, M4-40, `RequestEditorViewModel.cs` (how a send resolves variables:
  `IVariableScopeProvider`, `VariableScopes.CreateSource()`, `RequestResolver`) and the cancel-command style of the send command.
- `OAuth2EditorViewModel` gets a second constructor (the parameterless one of M4-40 stays and disables the token features) taking
  `IOAuth2TokenService tokens`, `Func<OAuth2Settings, OAuth2Settings> resolveSettings` (substitutes the variables of the editor's request; the
  request editor supplies it, M4-43) and `TimeProvider time`.
- Properties: `TokenStatusText` (`Strings.OAuth2NoToken`, `Strings.OAuth2TokenValidUntil(time)` using the local short date-time,
  `Strings.OAuth2TokenExpired`), `bool HasToken`, `bool IsGettingToken`, `string? TokenError`.
- Commands: `GetNewTokenCommand` (async, cancelable through the standard `CancelGetTokenCommand`; calls `tokens.GetNewTokenAsync(resolveSettings(ToSettings()),
  ct)`; sets `TokenError` on failure, clears it on success; always refreshes the status; cancellation shows no error),
  `ClearTokenCommand` (`tokens.Clear`, refresh status, clears the error). The token value is **never** exposed by this view model.
- The status refreshes on `Load`, on every settings change, after the commands, and when the token service's cache raises `Changed` (the
  service exposes the event: add `event EventHandler? TokensChanged` to `IOAuth2TokenService` and raise it from `OAuth2TokenService` when
  the cache changes; update the fake).
- `GetNewTokenCommand` can execute only when `TokenUrl` is non-empty and, for `AuthorizationCode`, `AuthorizationUrl` is non-empty.
- Strings `OAuth2NoToken`, `OAuth2TokenValidUntil`, `OAuth2TokenExpired`, `OAuth2GetNewToken`, `OAuth2ClearToken`, `OAuth2WaitingForBrowser`, EN
  and FR.

## Out of scope

Showing the token text, copying it, views, persistence.

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests` with `FakeOAuth2TokenService` and a fake `TimeProvider`: status text for the three states and its refresh
  on events; get-token success and failure; cancel during `GetNewTokenAsync` (a gated fake) leaves no error and resets `IsGettingToken`; clear;
  can-execute rules; the fake receives the **resolved** settings; no property contains the token value.
- [ ] Core test for `TokensChanged` raised on set, remove, clear.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the OAuth 2.0 token actions and status`
