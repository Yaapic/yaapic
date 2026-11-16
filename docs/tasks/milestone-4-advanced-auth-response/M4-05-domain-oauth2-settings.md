# M4-05: Domain: OAuth 2.0 settings and the new auth kinds

Status: todo
Platform: any
Depends on: M4-01

## Goal

The domain can describe an OAuth 2.0 authentication, and `AuthKind` knows the four new kinds.

## Design decisions

- Read first: ADR 0013, `src/Yaapic.Domain/Http/AuthKind.cs`, `RequestAuth.cs`, and every `switch` over `AuthKind` (grep the solution: the
  resolver, the YAML serializer, `AuthEditorViewModel`, the two auth views). New values must not break them: unknown kinds fall through
  to "no auth" until their cards land.
- `AuthKind` gains `OAuth2`, `Jwt`, `Digest`, `AwsV4` (appended, XML docs).
- New files in `src/Yaapic.Domain/Http/`: `OAuth2Grant` (`ClientCredentials`, `AuthorizationCode`, `Password`),
  `OAuth2ClientAuthentication` (`BasicHeader`, `RequestBody`), `OAuth2TokenPlacement` (`Header`, `Query`).
- `public sealed record OAuth2Settings(OAuth2Grant Grant, string TokenUrl, string AuthorizationUrl, string ClientId, string ClientSecret,
  string Scope, string Audience, string Username, string Password, string RedirectUri, OAuth2ClientAuthentication ClientAuthentication,
  OAuth2TokenPlacement Placement, string HeaderPrefix, string QueryParameterName)` with `static OAuth2Settings Default { get; }`: grant
  `ClientCredentials`, every string empty except `RedirectUri` = `http://127.0.0.1:8742/callback`, `HeaderPrefix` = `Bearer`,
  `QueryParameterName` = `access_token`, `BasicHeader`, `Header`.
- `RequestAuth` gets `public OAuth2Settings? OAuth2 { get; init; }` (no constructor change). `RequestAuth.None` unchanged.
- `HttpRequestDefinition.IsEquivalentTo` (and anything else comparing auth) must see the new property: check how auth is compared today and
  extend it; add a test that two definitions differing only in `OAuth2.ClientId` are not equivalent.

## Out of scope

JWT, AWS, Digest settings (M4-06), YAML (M4-07), resolution (M4-09), UI.

## Acceptance criteria

- [ ] Tests in `Yaapic.Domain.Tests`: `OAuth2Settings.Default` values, record equality, `with` on `RequestAuth.OAuth2`, equivalence test above.
- [ ] Every existing `AuthKind` switch still compiles and behaves as before for the four old kinds.
- [ ] Architecture tests green (Domain depends on nothing), 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(domain): add OAuth 2.0 settings and the new auth kinds`
