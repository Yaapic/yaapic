# M4-17: Core: AWS Signature v4 signer

Status: done
Platform: any
Depends on: M4-01

## Goal

Pure code computes an AWS Signature v4 `Authorization` header for a request.

## Design decisions

- Read first: ADR 0013 (AWS Signature v4). Namespace `Yaapic.Core.Auth.Aws`, folder `src/Yaapic.Core/Auth/Aws/`.
- `public sealed record AwsSigningInput(string Method, Uri Url, IReadOnlyList<KeyValueEntry> Headers, string PayloadHash, string
  AccessKeyId, string SecretAccessKey, string? SessionToken, string Region, string Service, DateTimeOffset Time)`.
- `public sealed record AwsSignature(string Authorization, string AmzDate, IReadOnlyList<KeyValueEntry> ExtraHeaders)` where
  `ExtraHeaders` are the headers the caller must add (`x-amz-date`, `x-amz-security-token` when a token is set).
- `public static class AwsSigV4`: `static AwsSignature Sign(AwsSigningInput input)` and `static string HashPayload(ReadOnlySpan<byte>)`
  (lowercase hex SHA-256) plus the constants `EmptyPayloadHash` and `UnsignedPayload`.
- Algorithm as AWS documents it: canonical request (method, URI-encoded path with each segment encoded once except for `s3` which is
  not double-encoded, sorted canonical query with `Uri.EscapeDataString`-style RFC 3986 encoding, canonical headers lowercase-trimmed and
  sorted, signed headers, payload hash), string to sign `AWS4-HMAC-SHA256\n<date-time>\n<date>/<region>/<service>/aws4_request\n<hash>`,
  signing key through the four HMAC steps, `Authorization: AWS4-HMAC-SHA256 Credential=<key>/<scope>, SignedHeaders=<list>,
  Signature=<hex>`.
- Signed headers: `host` (host plus `:port` when non-default), `x-amz-date`, `x-amz-security-token` when present,
  `x-amz-content-sha256` is **added by the caller to `Headers`** (M4-18) and therefore signed, and `content-type` when present in
  `Headers`. No other header is signed (keeps the signature stable when proxies add headers).
- All time handling is UTC, `yyyyMMddTHHmmssZ`.

## Out of scope

Applying it to a request (M4-18), presigned URLs, SigV4a, chunked uploads.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests` with AWS's published test-suite vectors (`get-vanilla`, `get-vanilla-query-order-key-case`,
  `post-vanilla-query`, `post-x-www-form-urlencoded`, `get-header-value-trim`; credential `AKIDEXAMPLE` / `wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY`,
  region `us-east-1`, service `service`, time `20150830T123600Z`): canonical request not exposed, but the final signature and header match
  the published ones byte for byte. Add: session token header, a custom port, non-ASCII path, `s3` path handling.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): compute AWS Signature v4 signatures`
