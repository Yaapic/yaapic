# M4-21: Core: OAuth 2.0 token client (client credentials, password, refresh)

Status: todo
Platform: any
Depends on: M4-20

## Goal

A service that returns a valid token for the non-interactive grants, using the cache and refreshing when possible.

## Design decisions

- Read first: ADR 0013 (When tokens are fetched), M4-19, M4-20, `IHttpTransport`, `FakeHttpTransport`.
- `public interface IOAuth2TokenService` in `Yaapic.Core.Auth.OAuth2`:
  - `Task<OAuth2TokenResult> GetTokenAsync(OAuth2Settings resolved, CancellationToken ct)`: never interactive.
  - `Task<OAuth2TokenResult> GetNewTokenAsync(OAuth2Settings resolved, CancellationToken ct)`: ignores the cache and fetches (for
    `AuthorizationCode` it runs the interactive flow, added by M4-23; until then it returns `Fail` "not available").
  - `OAuth2TokenStatus GetStatus(OAuth2Settings resolved)` (`NoToken`, `Valid(expiresAt)`, `Expired`) for the UI.
  - `void Clear(OAuth2Settings resolved)`.
  - `public sealed record OAuth2TokenResult(OAuth2Token? Token, string? Error)`.
- `public sealed class OAuth2TokenService(IHttpTransport transport, IOAuth2TokenCache cache, TimeProvider time) : IOAuth2TokenService`:
  1. `GetTokenAsync`: key from `OAuth2TokenKey.For`; cached and not expired (leeway 30 s) returns it; expired with a refresh token tries
     `Refresh` (a failed refresh drops the stale token and falls through); then by grant: `ClientCredentials` and `Password` fetch with
     M4-20; `AuthorizationCode` without a valid token returns `Fail(Strings.OAuth2GetTokenFirst)`.
  2. A fetched or refreshed token is stored; when a refresh answer has no `refresh_token`, the old refresh token is kept.
  3. Required fields: `TokenUrl` (absolute http/https) else `Strings.OAuth2TokenUrlMissing`; `ClientCredentials` needs `ClientId`; `Password`
     needs `Username`.
  4. Concurrent calls for the same key share one in-flight fetch (a `Dictionary<key, Task<...>>` under a lock) so two simultaneous sends do not
     request two tokens.
  5. Transport failures return `Fail(failure.Message)`; cancellation propagates as `OperationCanceledException` (the caller maps it).
- Strings `OAuth2GetTokenFirst`, `OAuth2TokenUrlMissing`, `OAuth2ClientIdMissing`, `OAuth2UsernameMissing`, EN and FR.
- A test double `FakeOAuth2TokenService` in `tests/Yaapic.TestDoubles/` (scripted results, records calls).

## Out of scope

The interactive flow (M4-22 to M4-25), the authenticator (M4-26), UI.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests` with `FakeHttpTransport`, `InMemoryOAuth2TokenCache` and a fake `TimeProvider`: first call fetches and caches;
  second call uses the cache (transport called once); after expiry minus leeway it refreshes; failed refresh falls back to a fresh fetch;
  kept refresh token; each required-field error; authorization-code grant without a token fails with the "get a token" message; two
  concurrent calls make one request; transport failure and error JSON surface their message; `Clear` removes the entry; `GetStatus` for all
  three states.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the OAuth 2.0 token service for non-interactive grants`
