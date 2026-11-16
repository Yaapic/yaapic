# M4-29: Domain: client certificate model

Status: done
Platform: any
Depends on: M4-03

## Goal

The domain describes a client certificate entry.

## Design decisions

- Read first: ADR 0014 (Client certificates), `src/Yaapic.Domain/Environments/` for the style of a small entity with an `Id`.
- New files in `src/Yaapic.Domain/Http/`: `ClientCertificateFormat` (`Pfx`, `Pem`) and
  `public sealed record ClientCertificate(Guid Id, string Name, string HostPattern, ClientCertificateFormat Format, string CertificatePath,
  string KeyPath, bool HasPassphrase)`.
- Validation lives in a static `ClientCertificateValidator.Validate(ClientCertificate)` returning an `IReadOnlyList<ClientCertificateIssue>`
  with `enum ClientCertificateIssue { NameEmpty, HostPatternInvalid, CertificatePathEmpty, KeyPathRequired }`:
  - `HostPatternInvalid`: empty, whitespace inside, a scheme (`://`), a path (`/`), `*` not at the very start followed by `.`, a bare
    `*`, more than one `*`, port outside 1..65535 or not numeric.
  - `KeyPathRequired`: never (a PEM file may hold the key); `Pfx` ignores `KeyPath`.
  The issue list is empty for a valid entry.
- No I/O, no certificate loading here.

## Out of scope

Matching (M4-30), storage (M4-31), loading (M4-33).

## Acceptance criteria

- [x] Tests in `Yaapic.Domain.Tests`: valid entries (`api.example.com`, `*.example.com`, `api.example.com:8443`, `*.example.com:8443`,
  `localhost`, `10.0.0.5`); each invalid pattern listed above; empty name; empty certificate path; record equality.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(domain): add the client certificate model and its validation`
