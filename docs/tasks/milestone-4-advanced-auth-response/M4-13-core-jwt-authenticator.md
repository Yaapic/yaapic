# M4-13: Core: JWT authenticator

Status: done
Platform: any
Depends on: M4-10, M4-12

## Goal

A request with JWT authentication is sent with a freshly signed token in the header or the query string.

## Design decisions

- Read first: ADR 0013 (JWT), M4-10 (`IAuthMethod`, extensions), M4-11, M4-12, `TimeProvider` usage in `ScriptedRequestSender`.
- `public sealed class JwtAuthMethod(TimeProvider time) : IAuthMethod`, `Kind` = `AuthKind.Jwt`, in `Yaapic.Core.Auth.Jwt`.
- `ApplyAsync`, using `auth.Jwt` (null means `JwtSettings.Default`):
  1. Parse `Payload` with `System.Text.Json.Nodes`-free code: use `JsonDocument` and require an object; an empty payload text counts as
     `{}`; anything else fails with `Strings.JwtPayloadInvalid`. Rebuild the payload with `Utf8JsonWriter`, copying every property in order,
     then appending `iat` (when `AddIssuedAt` and absent; `time.GetUtcNow().ToUnixTimeSeconds()`) and `exp` (when `ExpiresInSeconds` > 0 and
     absent; `iat`-independent: now + seconds).
  2. Header: `{"alg":"<HS256|...>","typ":"JWT"}` followed by the properties of `ExtraHeaders` (object, optional; `alg` and `typ` from it are
     ignored); invalid `ExtraHeaders` fails with `Strings.JwtHeadersInvalid`.
  3. Sign: HS with the key bytes (`Secret` UTF-8, or `Convert.FromBase64String` when `SecretIsBase64`, a bad base64 fails with
     `Strings.JwtSecretInvalid`); RS/ES with `PrivateKey`. An empty secret or key fails with `Strings.JwtKeyMissing`. Signer exceptions
     become `AuthenticationResult.Fail` with the exception message.
  4. Placement `Header`: `WithHeaderUnlessPresent("Authorization", $"{prefix} {jwt}")` (prefix trimmed; empty prefix gives the bare token);
     `Query`: `WithQueryParameter(QueryParameterName or "token", jwt)`.
- Serialization uses `Utf8JsonWriter` only (no reflection); output of header and payload has no whitespace.
- Localization: the four `Strings.Jwt*` keys, EN and FR.
- The token and key never appear in an error message.

## Out of scope

Caching tokens, the composite (M4-27), UI.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests` with a fake `TimeProvider`: header and query placement; `iat` and `exp` added only when absent and when
  enabled; claims order preserved; each error (payload not an object, bad extra headers, missing key, bad base64); a user
  `Authorization` header wins; HS256 token verified by recomputing the HMAC in the test; ES256 verified with the public key.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the JWT authentication method`
