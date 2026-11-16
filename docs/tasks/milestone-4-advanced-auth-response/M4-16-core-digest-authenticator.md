# M4-16: Core: Digest authenticator

Status: todo
Platform: any
Depends on: M4-10, M4-15

## Goal

A request with Digest authentication is preceded by a probe, then sent with the computed `Authorization` header.

## Design decisions

- Read first: ADR 0013 (Digest), M4-10, M4-15, `IHttpTransport`, `FakeHttpTransport`.
- `public sealed class DigestAuthMethod(IHttpTransport transport, Func<string> clientNonce) : IAuthMethod` (`Kind` = `Digest`); a second
  constructor without the nonce function uses 16 random bytes in lowercase hex.
- `ApplyAsync`:
  1. Username and password from `auth.Username` and `auth.Password`; both empty fails with `Strings.DigestCredentialsMissing`.
  2. Probe: `request with { BodyText = null, BodyFilePath = null, Parts = null, BodyContentType = null, PendingAuth = null }` and without any
     `Authorization` header, sent through `transport` (same timeout and redirect settings). A transport failure returns
     `Fail(failure.Message)`.
  3. When the probe status is not 401, or no `WWW-Authenticate` header carries a parsable Digest challenge: return the request unchanged
     (no auth header).
  4. Otherwise build the header with M4-15 (`requestUri` = absolute path plus query of `request.Url`) and return
     `WithHeaderUnlessPresent("Authorization", value)`. `NotSupportedException` becomes `Fail` with its message.
- `WWW-Authenticate` header values are matched case-insensitively and joined with `, ` when several are present.
- Only one probe per send; `stale=true` is not retried (the real request carries the fresh nonce anyway).
- Localization key `Strings.DigestCredentialsMissing`, EN and FR.

## Out of scope

Nonce caching across sends, `auth-int`, proxies.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests` with `FakeHttpTransport` and a fixed nonce: probe has no body or auth and keeps method, URL, headers;
  401 with a challenge yields the expected header; a 200 probe leaves the request unchanged; a 401 with a Basic-only challenge leaves it
  unchanged; transport failure; missing credentials; a user `Authorization` header wins; POST body is not in the probe but is in the
  returned request.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the Digest authentication method`
