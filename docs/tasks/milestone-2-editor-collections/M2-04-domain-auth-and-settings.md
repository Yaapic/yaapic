# M2-04: Domain: request authentication and settings

Status: todo
Platform: any
Depends on: M2-03

## Goal

A request carries its authentication and per-request settings.

## Design decisions

- New in `Yaapic.Domain.Http`: `AuthKind { None, Basic, Bearer, ApiKey }`, `ApiKeyLocation { Header, Query }`,
  `public sealed record RequestAuth(AuthKind Kind, string Username, string Password, string Token, string Key,
  string Value, ApiKeyLocation Location)` with `static RequestAuth None` (all strings empty, `Header`), and
  `public sealed record RequestSettings(bool FollowRedirects, int TimeoutSeconds)` with `static RequestSettings
  Default` = `(true, 0)`; `TimeoutSeconds` 0 means the application default. The `RequestSettings` constructor throws
  `ArgumentOutOfRangeException` for a negative timeout.
- `HttpRequestDefinition` gains two trailing parameters `RequestAuth Auth, RequestSettings Settings`; `Create` uses
  `RequestAuth.None` and `RequestSettings.Default`; `IsEquivalentTo` also compares them (plain record equality).
- Fix every compile error caused by the new parameters by passing `RequestAuth.None` / `RequestSettings.Default`
  (`RequestEditorViewModel.ToDefinition`, `TabsViewModel.IsUntouched`, `RequestYamlSerializer.Deserialize`, tests).
  Behaviour is unchanged: the editor and the serializer do not use them yet (M2-08, M2-11).

## Out of scope

Applying auth when sending (M2-06), persistence (M2-08), editor state (M2-11).

## Acceptance criteria

- [ ] Tests in `Yaapic.Domain.Tests`: `Create` has the defaults; `IsEquivalentTo` is false when only the auth or only
      the settings differ; a negative timeout throws.
- [ ] Whole solution builds with 0 warnings; every existing test still green.

## Suggested commit message

`feat(domain): add request auth and settings`
