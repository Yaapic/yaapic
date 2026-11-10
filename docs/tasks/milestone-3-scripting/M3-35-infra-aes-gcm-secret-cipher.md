# M3-35: Infrastructure: AES-GCM secret cipher and key derivation

Status: done
Platform: any
Depends on: M3-34

## Goal

The pure cryptographic helpers of the vault: encrypt and decrypt one secret with its identity bound as associated data,
wrap a data key with a passphrase. No files.

## Design decisions

- Read first: ADR 0010 (cryptography), `System.Security.Cryptography` docs for `AesGcm` (use the constructor that takes the tag
  size), `Rfc2898DeriveBytes.Pbkdf2`.
- `src/Yaapic.Infrastructure/Secrets/`:
  - `internal static class SecretCipher`: `EncryptedSecret Encrypt(ReadOnlySpan<byte> dataKey, SecretRef secret, string plaintext)`
    (random 12-byte nonce from `RandomNumberGenerator`, 16-byte tag, AAD = UTF-8 `yaapic-vault-v1|<Kind>|<OwnerId:D>|<Name>`)
    and `string Decrypt(ReadOnlySpan<byte> dataKey, SecretRef secret, EncryptedSecret value)`; `CryptographicException`
    from a wrong key, a tampered byte or a different `SecretRef` is wrapped into `VaultException("The vault entry cannot be
    decrypted")`. `sealed record EncryptedSecret(byte[] Nonce, byte[] Ciphertext, byte[] Tag)`.
  - `internal static class DataKey`: `byte[] Generate()` (32 random bytes), `static void Clear(byte[])`
    (`CryptographicOperations.ZeroMemory`).
  - `internal static class PassphraseKdf`: `WrappedKey Wrap(ReadOnlySpan<byte> dataKey, string passphrase)` (random 16-byte salt,
    PBKDF2-HMAC-SHA512, **600 000** iterations, 32-byte key, AES-256-GCM wrap with AAD `yaapic-vault-v1|wrap`, layout
    `nonce(12) | ciphertext | tag(16)` in `Bytes`, `Kdf` = (`pbkdf2-sha512`, iterations, salt)) and
    `byte[] Unwrap(WrappedKey wrapped, string passphrase)` (a wrong passphrase fails the tag check -> `VaultException("Wrong
    passphrase")`; unknown algorithm or iterations below 100 000 or above 10 000 000 -> `VaultException("Unsupported vault key
    derivation")` so a crafted file cannot freeze the app).
- Empty passphrase is rejected (`ArgumentException`); the minimum length rule (8 characters) is a UI rule (M3-45), not here.
- Everything is synchronous and CPU-bound (PBKDF2 takes about 0.3 s): callers run it on a background thread (document it).

## Out of scope

The file format, the vault service, OS protectors.

## Acceptance criteria

- [x] Tests: encrypt/decrypt round trip (empty, ASCII, unicode, 1 MB); nonce differs between two encryptions of the same
      value; flipping any byte of nonce/ciphertext/tag fails; decrypting under another `SecretRef` (name, owner, kind) fails;
      wrong key fails; PBKDF2 wrap/unwrap round trip, wrong passphrase, unsupported algorithm, iteration bounds; a known-answer
      test for PBKDF2-HMAC-SHA512 against a published vector (RFC 6070 style vector for SHA-512 with a small iteration count
      through `Rfc2898DeriveBytes.Pbkdf2` itself, to guard the parameter order).
- [x] No reflection, AOT analyzers clean, 0 warnings, XML docs where public, all tests green.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (published exes, stub servers, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m3-scripting` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`feat(infrastructure): add the AES-GCM secret cipher and passphrase key wrapping`
