# M4-19: Core: OAuth 2.0 token model and in-memory cache

Status: done
Platform: any
Depends on: M4-05

## Goal

The OAuth 2.0 token types and a thread-safe in-memory cache with expiry and a stable key.

## Design decisions

- Read first: ADR 0013 (Token lifetime). Namespace `Yaapic.Core.Auth.OAuth2`, folder `src/Yaapic.Core/Auth/OAuth2/`.
- `public sealed record OAuth2Token(string AccessToken, string TokenType, string? RefreshToken, string? Scope, DateTimeOffset? ExpiresAt,
  DateTimeOffset ObtainedAt)` with `bool IsExpired(DateTimeOffset now, TimeSpan leeway)` (`ExpiresAt` null never expires; expired when
  `now + leeway >= ExpiresAt`).
- `public readonly record struct OAuth2TokenKey(string Value)` with `static OAuth2TokenKey For(OAuth2Settings settings)`: lowercase hex
  SHA-256 of the UTF-8 text `grant|tokenUrl|clientId|scope|audience|username` (all taken from the **resolved** settings). Two settings that differ in
  any of those produce different keys; the client secret and password are not part of the key.
- `public interface IOAuth2TokenCache { bool TryGet(OAuth2TokenKey key, out OAuth2Token token); void Set(OAuth2TokenKey key, OAuth2Token token);
  void Remove(OAuth2TokenKey key); void Clear(); event EventHandler? Changed; }` (`TryGet` returns the token even when expired: the caller
  decides about refresh; `Changed` fires on `Set`, `Remove` and `Clear` when something changed).
- `public sealed class InMemoryOAuth2TokenCache : IOAuth2TokenCache` (a `Dictionary` under a lock). Lives in Core (it is pure state), with
  a `Leeway` constant `TimeSpan.FromSeconds(30)` exposed as `OAuth2Token.DefaultLeeway`.
- Never writes anywhere; `ToString()` of `OAuth2Token` (override the record's) redacts the access and refresh tokens (`AccessToken = ****`).

## Out of scope

Fetching tokens (M4-20, M4-21), persistence (deferred, ADR 0013).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: key stable and sensitive to each included field and insensitive to secret and password; expiry with
  leeway and with no expiry; set, get, remove, clear and `Changed` counts; concurrent `Set` from several tasks; `ToString` redaction.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the OAuth 2.0 token model and cache`
