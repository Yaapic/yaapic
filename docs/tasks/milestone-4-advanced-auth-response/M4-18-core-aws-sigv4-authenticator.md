# M4-18: Core: AWS Signature v4 authenticator

Status: todo
Platform: any
Depends on: M4-10, M4-17

## Goal

A request with AWS Signature v4 authentication is signed and sent.

## Design decisions

- Read first: ADR 0013 (AWS Signature v4), M4-10, M4-17, `ResolvedRequest` (`BodyText`, `BodyFilePath`, `Parts`).
- `public sealed class AwsV4AuthMethod(TimeProvider time) : IAuthMethod`, `Kind` = `AwsV4`, in `Yaapic.Core.Auth.Aws`.
- `ApplyAsync` with `auth.AwsV4` (null = `Default`): empty access key, secret, region or service fails with
  `Strings.AwsSettingsMissing` (names the missing field in the message: `Strings.AwsFieldMissing(name)`). The URL must be absolute `http`
  or `https`, else `Fail`.
- Payload hash: `BodyText` not null: SHA-256 of its UTF-8 bytes; `BodyFilePath`: SHA-256 streamed from the file (an `IOException` or
  `UnauthorizedAccessException` is a `Fail` with `Strings.AwsPayloadUnreadable(path)`); `Parts` not null: `UNSIGNED-PAYLOAD`; no body:
  the empty hash.
- Add `x-amz-content-sha256: <hash>` to the request headers (`WithHeaderUnlessPresent`-style but this one **replaces** a user header of
  the same name, since a wrong value would invalidate the signature), call `AwsSigV4.Sign`, then add `Authorization` and every
  `ExtraHeaders` entry. A user `Authorization` header wins as in M4-10 (then nothing is signed and nothing added).
- Strings: `AwsSettingsMissing`/`AwsFieldMissing`/`AwsPayloadUnreadable`, EN and FR.

## Out of scope

Presigned URLs, retries, S3 multipart payload hashing.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests` with a fake `TimeProvider`: GET without body, POST with text body, file body (temp file), multipart gives
  `UNSIGNED-PAYLOAD`, session token, missing fields each named, unreadable file, user `Authorization` wins, the produced `Authorization`
  equals `AwsSigV4.Sign` called directly with the same inputs.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the AWS Signature v4 authentication method`
