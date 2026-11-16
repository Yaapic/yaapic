# M4-30: Core: client certificate matching and provider port

Status: done
Platform: any
Depends on: M4-29

## Goal

Pure code picks the client certificate entry that applies to a URL, behind a port the transport can use.

## Design decisions

- Read first: ADR 0014 (Matching), M4-29. Namespace `Yaapic.Core.Certificates`, folder `src/Yaapic.Core/Certificates/`.
- `public static class ClientCertificateMatcher`: `static ClientCertificate? Find(IReadOnlyList<ClientCertificate> entries, Uri url)`.
  - Only `https` URLs match. The effective port of the URL is used (443 default).
  - Specificity rank (lower is better): 0 exact host with explicit port in the pattern, 1 exact host without port, 2 wildcard with port,
    3 wildcard without port. A pattern without a port matches any port. Ties go to the earliest entry. Case-insensitive host comparison.
  - `*.example.com` matches `a.example.com` and `a.b.example.com`, not `example.com`.
  - Invalid entries (per `ClientCertificateValidator`) are skipped.
- `public interface IClientCertificateProvider { Task<ClientCertificateLookup> FindAsync(Uri url, CancellationToken ct); }` with
  `public sealed record ClientCertificateLookup(string? CacheKey, X509Certificate2? Certificate, string? Error)` (`CacheKey` is the entry
  id, `null` means no entry matches; `Certificate` null with an error means the entry matched but could not be loaded). The provider owns
  the certificate lifetime; callers never dispose it. Implemented by M4-33.
  Core may reference `System.Security.Cryptography.X509Certificates` (part of the framework).
- `FakeClientCertificateProvider` in `tests/Yaapic.TestDoubles/` (maps host to a lookup).

## Out of scope

The list store (M4-31), loading files (M4-33).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: each rank beats the next; ties; wildcard depth; bare domain not matched by a wildcard; port rules;
  `http` never matches; invalid entries skipped; case-insensitive.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): match client certificates to request URLs`
