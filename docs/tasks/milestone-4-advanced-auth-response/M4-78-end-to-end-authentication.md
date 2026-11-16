# M4-78: End-to-end tests: authentication

Status: todo
Platform: any
Depends on: M4-27, M4-61

## Goal

The real composed pipeline (resolver, authenticators, transport) is tested against local stub servers for every new auth kind.

## Design decisions

- Read first: `tests/Yaapic.Infrastructure.Tests/EndToEnd/` (`LoopbackServer`, the Milestone 3 end-to-end test classes for style and how they build the composition), M4-27, M4-61.
- New `Milestone4AuthenticationEndToEndTests` in `tests/Yaapic.Infrastructure.Tests/EndToEnd/` using the real `HttpClientTransport`, `RequestSender`, `RequestAuthenticator.CreateDefault`,
  `OAuth2TokenService` with `InMemoryOAuth2TokenCache`, a stub **identity and API server** (`LoopbackServer` extended or a new `AuthStubServer` in the same folder) with these endpoints:
  `POST /token` (client credentials and password and refresh_token and authorization_code grants, validates Basic header or body credentials, returns `access_token` `t1`, `expires_in`, `refresh_token`),
  `GET /authorize` (redirects to the `redirect_uri` with `code` and `state`, used with a `FakeExternalBrowser` that performs a plain `HttpClient` GET of the URL so the real
  `TcpLoopbackRedirectReceiver` receives the redirect), `GET /api` (requires `Authorization: Bearer t1`, returns 200 else 401), `GET /digest` (RFC 7616 digest with a fixed nonce, validates the response), `GET /jwt` (validates
  an HS256 token with the shared secret and returns its claims), `GET /aws` (recomputes the SigV4 signature with the known secret and fixed clock, returns 200 or 403).
- Scenarios: client credentials send (token requested once, reused on the second send, a new fetch after a fake clock moves past expiry); refresh-token path; password grant; authorization-code with PKCE end
  to end through the real loopback receiver (the stub checks `code_verifier` against the challenge it saw); wrong client secret gives `AuthenticationFailed` with the server's `error_description` and no token
  in the message; Digest 200, wrong password 401 passes through as a normal 401 response; JWT HS256 claims (`iat`, `exp`) as configured; AWS 200 and a tampered secret 403; a user-set
  `Authorization` header is kept and no token request is made; secrets never appear in any failure message (assert on the strings).
- No real internet access; ports are chosen by binding 0; every listener and receiver is disposed; the test names state the scenario.

## Out of scope

mTLS and timings (M4-79), the UI.

## Acceptance criteria

- [ ] The scenarios above, all green and deterministic (no sleeps over 200 ms; the fake `TimeProvider` drives expiry).
- [ ] No listener or process left open (run the class twice in one session).
- [ ] Architecture tests green, 0 warnings, all tests green.

## Suggested commit message

`test: add end-to-end tests for OAuth 2.0, JWT, Digest and AWS authentication`
