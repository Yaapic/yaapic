# M4-12: Core: JWT signing with RSA and ECDSA

Status: todo
Platform: any
Depends on: M4-11

## Goal

`JwtSigner` also signs with RS256/384/512 and ES256/384/512 from a PEM private key.

## Design decisions

- Read first: ADR 0013 (JWT), M4-11.
- Add `static string Sign(string headerJson, string payloadJson, JwtAlgorithm algorithm, string privateKeyPem)`.
  - RS: `RSA.Create()` then `ImportFromPem(pem)`, `SignData(data, HashAlgorithmName.SHA256/384/512, RSASignaturePadding.Pkcs1)`.
  - ES: `ECDsa.Create()` then `ImportFromPem(pem)`, `SignData(data, hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)` (the JWS
    format, not DER). Reject a curve that does not match the algorithm (P-256 for ES256, P-384 for ES384, P-521 for ES512) with an
    `InvalidOperationException("The key does not match ES256")`-style message.
  - An empty or invalid PEM throws `InvalidOperationException` with a message that never contains key material.
  - Keys are disposed after use.
- The `byte[]` overload of M4-11 keeps rejecting RS/ES, and the PEM overload rejects HS, with `ArgumentException` naming the algorithm.
- Test keys are generated at test time (`RSA.Create(2048)`, `ECDsa.Create(ECCurve.NamedCurves.nistP256)`, `ExportPkcs8PrivateKeyPem`) so
  no key file is committed; verification uses the matching public key from the same object.

## Out of scope

PS256, EdDSA, key files, claims.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: each RS and ES algorithm signs and the signature verifies with the public key (ES in the
  IEEE P1363 format, 64/96/132 bytes); the RFC 7515 appendix A.2 (RS256) published token is **verified** with its public key; PKCS#1 and
  PKCS#8 PEM both work; wrong curve rejected; garbage PEM rejected without leaking the text; HS vs PEM overload misuse.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): sign JWTs with RSA and ECDSA`
