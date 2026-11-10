# M3-34: Core: vault ports and test doubles

Status: done
Platform: any
Depends on: M3-33

## Goal

The ports of the secret vault and in-memory doubles, so the decorators, view models and UI can be built and tested before
any cryptography exists.

## Design decisions

- Read first: ADR 0010. Namespace `Yaapic.Core.Secrets`, folder `src/Yaapic.Core/Secrets/`.
- `public enum SecretOwnerKind { Environment, Collection, Globals }`;
  `public readonly record struct SecretRef(SecretOwnerKind Kind, Guid OwnerId, string Name)` with `static SecretRef Globals(string name)`
  (owner `Guid.Empty`).
- `public enum VaultState { NotCreated, Locked, Unlocked }`.
- `public sealed class VaultException(string message, Exception? inner = null) : Exception` (wrong passphrase, corrupt
  file, protector failure; messages are user-presentable).
- `public sealed record WrappedKey(byte[] Bytes, VaultKdf? Kdf)` and `public sealed record VaultKdf(string Algorithm, int Iterations, byte[] Salt)`.
- `public interface IKeyProtector`: `string Id { get; }` (`passphrase`, `dpapi`, `keychain`, `secret-service`),
  `bool RequiresPassphrase { get; }`, `bool IsAvailable(out string? reason)`, `WrappedKey Wrap(ReadOnlySpan<byte> dataKey, string workspaceId)`,
  `byte[] Unwrap(WrappedKey wrapped, string workspaceId)` (throws `VaultException` on failure). The passphrase protector is
  constructed with the passphrase (M3-36); OS protectors need no input.
- `public interface IKeyProtectorFactory`: `IReadOnlyList<IKeyProtector> Available()` (the platform protector first, the
  passphrase protector last), `IKeyProtector? Create(string id, string? passphrase)`. Composition
  provides the per-platform implementation (M3-48, M3-49); M3-36 provides the passphrase one.
- `public interface ISecretVault`:
  `VaultState State { get; }`, `string? ProtectorId { get; }`, `event EventHandler? StateChanged`,
  `Task InitializeAsync(CancellationToken ct)` (reads the file; tries automatic unlock when the stored protector does not
  need a passphrase), `Task CreateAsync(string protectorId, string? passphrase, CancellationToken ct)`,
  `Task<bool> UnlockAsync(string passphrase, CancellationToken ct)` (false = wrong passphrase),
  `void Lock()`, `bool TryGet(SecretRef secret, out string value)` (decrypted cache; false when locked or missing),
  `Task SetAsync(SecretRef, string value, CancellationToken)`, `Task RemoveAsync(SecretRef, CancellationToken)`,
  `Task RemoveOwnerAsync(SecretOwnerKind kind, Guid ownerId, CancellationToken)`, `IReadOnlyList<string> GetSecretValues()`
  (decrypted values of length >= 4, longest first, empty when locked: used by the redactor).
  `SetAsync`/`RemoveAsync` on a locked or missing vault throw `VaultException("The vault is locked")`.
- Doubles in `tests/Yaapic.TestDoubles/`: `InMemorySecretVault` (state machine with a configurable passphrase, `Seed(...)`
  helper, records calls, raises `StateChanged`), `FakeKeyProtectorFactory`.

## Out of scope

Cryptography and files (M3-35, M3-36), decorators (M3-37), UI.

## Acceptance criteria

- [x] Tests for `InMemorySecretVault`: state transitions (`NotCreated` -> create -> `Unlocked` -> `Lock` -> `UnlockAsync`),
      wrong passphrase returns false, set/get/remove/remove-owner, locked set throws, `GetSecretValues` ordering and the
      length-4 floor, `StateChanged` raised once per transition.
- [x] Architecture tests green (Core still references only Domain), 0 warnings, XML docs, all tests green.

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

`feat(core): add secret vault ports and in-memory doubles`
