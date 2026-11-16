# M4-32: Vault: certificate passphrases

Status: done
Platform: any
Depends on: M4-29

## Goal

The vault can hold the passphrase of a client certificate.

## Design decisions

- Read first: ADR 0010 and ADR 0014 (Passphrase), `src/Yaapic.Core/Secrets/SecretOwnerKind.cs`, `SecretRef.cs`, `FileSecretVault.cs` (how
  `kind` is written and used in the AES-GCM associated data), `InMemorySecretVault`.
- `SecretOwnerKind` gains `Certificate` **appended** (existing numeric values and the written text of the other kinds stay unchanged: check how the
  kind is serialized in the vault file and in the associated data `yaapic-vault-v1|<kind>|<ownerId>|<name>`, and make `Certificate` serialize as
  the text `certificate`; a vault written before this card must still open and decrypt).
- `SecretRef.Certificate(Guid certificateId)` helper returning `new(SecretOwnerKind.Certificate, certificateId, "passphrase")`.
- `ISecretVault.RemoveOwnerAsync(SecretOwnerKind.Certificate, id, ct)` already exists: confirm it works for the new kind (used when an entry
  is deleted by M4-44).
- `FileSecretVault` and `InMemorySecretVault`: no behaviour change besides accepting the new kind. A vault file containing an unknown kind
  text must keep failing as it does today (do not weaken that).

## Out of scope

UI, using the passphrase (M4-33).

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests` and `Yaapic.Core.Tests`: set/get/remove/remove-owner for a certificate secret on both vault
  implementations; the associated data differs from the environment kind with the same id and name (a swapped entry fails to decrypt); a vault
  fixture written with the old code (hex or file committed in the test project) still opens; `GetSecretValues` includes certificate
  passphrases of length >= 4 so the redactor hides them.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(vault): store client certificate passphrases`
