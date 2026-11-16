# M4-15: Core: Digest challenge parsing and response computation

Status: todo
Platform: any
Depends on: M4-01

## Goal

Pure code parses a `WWW-Authenticate: Digest` challenge and computes the `Authorization: Digest` header value (RFC 7616).

## Design decisions

- Read first: ADR 0013 (Digest). Namespace `Yaapic.Core.Auth.Digest`, folder `src/Yaapic.Core/Auth/Digest/`.
- `public sealed record DigestChallenge(string Realm, string Nonce, string? Opaque, string Algorithm, IReadOnlyList<string> Qop, bool Stale)`.
  `static bool TryParse(string headerValue, out DigestChallenge? challenge)`: the value may hold several challenges (`Basic realm=...,
  Digest realm=...`) or several `WWW-Authenticate` headers joined by the caller; pick the first `Digest` one. Parameters are
  comma-separated `name=value` or `name="quoted value"` with `\"` escapes; names case-insensitive; `algorithm` defaults to `MD5`;
  `qop="auth,auth-int"` split on comma. A challenge without `realm` or `nonce` fails.
- `public static class DigestResponse`: `static string BuildHeader(DigestChallenge challenge, string method, string requestUri,
  string username, string password, string clientNonce)` returns the full value `Digest username="...", realm="...", nonce="...",
  uri="...", algorithm=..., response="...", qop=auth, nc=00000001, cnonce="...", opaque="..."`.
  - Algorithms: `MD5`, `MD5-sess`, `SHA-256`, `SHA-256-sess` (case-insensitive); anything else throws `NotSupportedException`.
  - `qop`: `auth` is used when offered (response = H(HA1:nonce:nc:cnonce:qop:HA2)); no qop at all uses the RFC 2069 form
    (H(HA1:nonce:HA2), and no `qop`, `nc`, `cnonce` in the header); only `auth-int` throws `NotSupportedException`.
  - `requestUri` is the path and query of the URL (`/dir/index.html?x=1`); the caller extracts it.
  - UTF-8 for all hashing; lowercase hex; values escaped (`"` and `\`) in the header.
- `MD5` is used because the protocol requires it: suppress nothing silently; if an analyzer flags `MD5`, add a targeted
  `[SuppressMessage]` with the justification text "required by RFC 7616 digest authentication" (the one allowed suppression of this card).

## Out of scope

Sending the probe (M4-16), `auth-int`, `userhash`, nonce reuse.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: the RFC 2617 section 3.5 example (`Mufasa` / `Circle Of Life`, `/dir/index.html`, the published nonce,
  cnonce `0a4f113b`) reproduces the published response `6629fae49393a05397450978507c4ef1`; the RFC 7616 SHA-256 example reproduces its
  published response; MD5-sess; no-qop form; `auth-int` only throws; parsing of quoted commas, multiple schemes, missing realm, case
  insensitivity, `stale=true`.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): parse digest challenges and compute the digest response`
