# M4-06: Domain: JWT and AWS Signature v4 settings

Status: todo
Platform: any
Depends on: M4-05

## Goal

The domain can describe a JWT signing authentication and an AWS Signature v4 authentication. Digest needs nothing new (it reuses
`RequestAuth.Username` and `Password`).

## Design decisions

- Read first: ADR 0013, M4-05, `RequestAuth.cs`.
- New files in `src/Yaapic.Domain/Http/`: `JwtAlgorithm` (`Hs256`, `Hs384`, `Hs512`, `Rs256`, `Rs384`, `Rs512`, `Es256`, `Es384`, `Es512`),
  `JwtPlacement` (`Header`, `Query`).
- `public sealed record JwtSettings(JwtAlgorithm Algorithm, string Secret, bool SecretIsBase64, string PrivateKey, string Payload,
  string ExtraHeaders, bool AddIssuedAt, int ExpiresInSeconds, JwtPlacement Placement, string HeaderPrefix, string QueryParameterName)`
  with `static JwtSettings Default`: `Hs256`, empty strings, `Payload` = `{}`, `AddIssuedAt` true, `ExpiresInSeconds` 0, `Header`,
  `HeaderPrefix` = `Bearer`, `QueryParameterName` = `token`.
- `public sealed record AwsV4Settings(string AccessKeyId, string SecretAccessKey, string SessionToken, string Region, string Service)`
  with `static AwsV4Settings Default` (all empty).
- `RequestAuth` gets `public JwtSettings? Jwt { get; init; }` and `public AwsV4Settings? AwsV4 { get; init; }`.
- The auth comparison extended in M4-05 also covers these two properties (test: differing only in `Jwt.Payload`, only in
  `AwsV4.Region`).

## Out of scope

Signing, resolution, YAML, UI.

## Acceptance criteria

- [ ] Tests in `Yaapic.Domain.Tests`: defaults, equality, `with`, the two equivalence cases.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(domain): add JWT and AWS Signature v4 settings`
