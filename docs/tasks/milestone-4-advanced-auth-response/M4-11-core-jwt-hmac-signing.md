# M4-11: Core: JWT signing with HMAC

Status: done
Platform: any
Depends on: M4-01

## Goal

A pure signer produces compact JWTs with HS256, HS384 and HS512.

## Design decisions

- Read first: ADR 0013 (JWT). Namespace `Yaapic.Core.Auth.Jwt`, folder `src/Yaapic.Core/Auth/Jwt/`.
- `public static class Base64Url { string Encode(ReadOnlySpan<byte>); byte[] Decode(string) }` (RFC 4648 section 5, no padding; `Decode`
  throws `FormatException` on invalid text, accepts missing padding).
- `public sealed class JwtSigner` with `static string Sign(string headerJson, string payloadJson, JwtAlgorithm algorithm, byte[] hmacKey)`
  for the three HS algorithms (`HMACSHA256/384/512.HashData`). Result `base64url(header).base64url(payload).base64url(signature)`; header
  and payload are used as given (the caller builds them, M4-13). RS and ES algorithms throw `NotSupportedException` here (M4-12 adds them
  by extending the same class; keep the algorithm switch in one place).
- `JwtAlgorithm` lives in `Yaapic.Domain`; Core may reference Domain.
- `static string HeaderJson(JwtAlgorithm algorithm, IReadOnlyDictionary<string,string>? extra)` is **not** here (M4-13 owns header
  building).

## Out of scope

RS/ES (M4-12), claims handling (M4-13), decoding (M4-14).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests` with fixed vectors: the RFC 7515 appendix A.1 token (HS256, the published key and payload) is
  reproduced byte for byte; HS384 and HS512 against values computed independently (document how in a test comment); `Base64Url`
  round trip, padding, `+/` replaced, invalid text throws.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): sign JWTs with HMAC`
