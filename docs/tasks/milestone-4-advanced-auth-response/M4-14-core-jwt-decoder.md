# M4-14: Core: JWT decoder

Status: done
Platform: any
Depends on: M4-11

## Goal

A pure helper decodes the header and payload of a JWT (without verifying it) and tells whether it is expired.

## Design decisions

- Read first: ADR 0013 (JWT decoding), `Base64Url` from M4-11, `ResponseFormatter` (to pretty-print JSON).
- `public sealed record DecodedJwt(string HeaderJson, string PayloadJson, string Signature, string? Algorithm, DateTimeOffset? IssuedAt,
  DateTimeOffset? NotBefore, DateTimeOffset? ExpiresAt)` in `Yaapic.Core.Auth.Jwt`.
- `public static class JwtDecoder`: `static bool TryDecode(string token, out DecodedJwt? jwt, out string? error)` and
  `static JwtStatus GetStatus(DecodedJwt jwt, DateTimeOffset now)` with `enum JwtStatus { Valid, Expired, NotYetValid, NoExpiry }` (`Expired`
  when `exp` <= now; `NotYetValid` when `nbf` > now; otherwise `Valid` when `exp` exists, else `NoExpiry`).
- `TryDecode`: trims whitespace and a leading `Bearer ` (case-insensitive); needs exactly three dot-separated parts (an unsigned token
  with an empty third part is allowed); header and payload must be Base64URL JSON objects; `HeaderJson` and `PayloadJson` are returned
  pretty-printed (indented, original property order); `exp`, `nbf`, `iat` are read as numeric seconds when present and numeric (a string or
  float is ignored, not an error). Errors: `error` is an English message key-free string used only for tests; the view model localizes
  (M4-49).
- Never verifies a signature and says so in the XML doc.

## Out of scope

Signature verification, JWE, UI.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: the RFC 7515 A.1 token decodes with its header and payload; `Bearer ` prefix and whitespace; two parts,
  four parts, bad base64, non-object payload, non-JSON all fail; claims parsed; each `JwtStatus`; unsigned token; property order kept.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): decode JWTs and report their expiry`
