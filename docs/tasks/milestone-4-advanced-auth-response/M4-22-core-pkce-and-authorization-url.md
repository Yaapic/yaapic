# M4-22: Core: PKCE and the authorization request URL

Status: todo
Platform: any
Depends on: M4-05, M4-11

## Goal

Pure helpers generate the PKCE pair and `state`, build the authorization URL and parse the redirect.

## Design decisions

- Read first: ADR 0013 (Authorization-code redirect), RFC 7636, RFC 6749 section 4.1.
- Namespace `Yaapic.Core.Auth.OAuth2`.
- `public sealed record PkcePair(string Verifier, string Challenge)` and `public static class Pkce`: `static PkcePair Create()` (verifier = 32
  random bytes, `Base64Url` without padding = 43 characters; challenge = `Base64Url(SHA256(ASCII(verifier)))`, method S256), plus
  `static PkcePair FromVerifier(string verifier)` for tests. `Base64Url` comes from M4-11 (`Yaapic.Core.Auth.Jwt`).
- `public static class OAuth2AuthorizationRequest`: `static string NewState()` (16 random bytes, Base64Url) and
  `static Uri BuildUrl(OAuth2Settings s, string state, string codeChallenge)`: `AuthorizationUrl` plus `response_type=code`, `client_id`,
  `redirect_uri`, `state`, `code_challenge`, `code_challenge_method=S256`, `scope` and `audience` when non-empty; an existing query in
  `AuthorizationUrl` is kept and extended. An invalid or non-http(s) `AuthorizationUrl` throws `ArgumentException`.
- `public sealed record AuthorizationCallback(string? Code, string? Error, string? ErrorDescription, string? State)` and
  `static AuthorizationCallback ParseCallback(string requestTarget)` (the path and query of the redirect request line, for example
  `/callback?code=abc&state=xyz`); percent-decoding; first value wins for duplicate names.
- `static bool TryGetListenAddress(string redirectUri, out int port, out string path)`: only `http`, host `127.0.0.1` or `localhost`,
  explicit port 1..65535, else false (the flow shows `Strings.OAuth2RedirectUriInvalid`).
- Strings `OAuth2RedirectUriInvalid`, EN and FR.

## Out of scope

Listening, opening the browser, the code exchange (M4-23).

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: the RFC 7636 appendix B vector (verifier `dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk` gives challenge
  `E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM`); random verifier length and alphabet; URL contains every parameter encoded, keeps an
  existing query, omits empty scope and audience; invalid URL throws; callback parsing of success, error, encoded values, missing query;
  listen-address accepts `http://127.0.0.1:8742/callback` and `http://localhost:9000/`, rejects https, other hosts, no port, port 0.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add PKCE and the OAuth 2.0 authorization request builder`
