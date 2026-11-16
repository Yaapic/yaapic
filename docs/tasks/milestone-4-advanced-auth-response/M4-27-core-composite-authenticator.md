# M4-27: Core: the composite authenticator

Status: todo
Platform: any
Depends on: M4-13, M4-16, M4-18, M4-26

## Goal

One `IRequestAuthenticator` routes a pending authentication to the method of its kind.

## Design decisions

- Read first: M4-10 (`IAuthMethod`, `IRequestAuthenticator`), the four methods.
- `public sealed class RequestAuthenticator(IEnumerable<IAuthMethod> methods) : IRequestAuthenticator` in `Yaapic.Core.Auth`: builds a
  dictionary by `Kind` (a duplicate kind throws `ArgumentException` at construction); `AuthenticateAsync` returns the request unchanged when
  `PendingAuth` is null, calls the method of `PendingAuth.Kind` with `request with { PendingAuth = null }` (so a method can never loop),
  and returns `Fail(Strings.AuthNotAvailable)` when no method is registered for the kind.
- `public static RequestAuthenticator CreateDefault(IHttpTransport transport, IOAuth2TokenService tokens, TimeProvider time)` registering
  `JwtAuthMethod`, `DigestAuthMethod`, `AwsV4AuthMethod` and `OAuth2AuthMethod` (composition uses it, M4-61).
- End-to-end test at the `RequestSender` level: a request with each kind goes through `RequestSender(transport, RequestAuthenticator.CreateDefault(...))`
  and the transport receives the expected header.

## Out of scope

Composition wiring (M4-61), auth inheritance (M4-28).

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: routing per kind with fake methods; unchanged when no pending auth; unknown kind failure; duplicate
  kinds rejected; `PendingAuth` cleared before the method runs; the `RequestSender` integration for JWT (HS256), AWS (fixed clock), OAuth 2.0
  (client credentials with `FakeHttpTransport` answering the token request then the API request) and Digest (401 probe then 200).
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): route pending authentication to its method`
