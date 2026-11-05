# M3-36: Infrastructure: file secret vault, passphrase protector and workspace id

Status: todo
Platform: any
Depends on: M3-35

## Goal

`FileSecretVault : ISecretVault`: the encrypted per-user, per-workspace vault file, with the `passphrase` protector.

## Design decisions

- Read first: ADR 0010 (location, file shape, states), M3-34 (ports), M3-35 (cipher), `src/Yaapic.Infrastructure/Storage/Workspace.cs`,
  `FileSessionStore.cs` (atomic write style), `FileHistoryStore.cs` (System.Text.Json source-generated context example).
- **Workspace id**: `Workspace.EnsureCreated` also creates `.yaapic/workspace.id` (a GUID "D" plus newline) when missing;
  `public static string ReadWorkspaceId(string root)` returns it (creating it if absent). The file is inside `.yaapic/`,
  already git-ignored. A workspace folder copied elsewhere keeps its id (so does its vault location only on the same machine).
- `public interface IVaultLocation { string GetVaultPath(string workspaceId); }` with `DefaultVaultLocation` (user data
  directory: `Environment.GetFolderPath(SpecialFolder.ApplicationData)` on Windows, `~/Library/Application Support` on macOS,
  `$XDG_DATA_HOME` or `~/.local/share` on Linux, then `Yaapic/vaults/<id>.vault`) and a test location pointing to a temp dir.
- `VaultFileDto` (internal, `System.Text.Json` source generation through `VaultJsonContext`, camelCase, indented,
  `JsonSerializerOptions` fixed in the context, no reflection) exactly as ADR 0010: `version`, `protector`, `wrappedKey` (base64),
  `kdf` (`algorithm`, `iterations`, `salt`) or null, `entries[]` (`kind`, `owner`, `name`, `nonce`, `ciphertext`, `tag`). Unknown
  `version` -> `VaultException("Unsupported vault version")`; invalid JSON -> `VaultException("The vault file is corrupt")` and the
  state stays `Locked` with the file untouched (never overwritten by a failed read).
- `public sealed class FileSecretVault(string workspaceRoot, IVaultLocation location, IKeyProtectorFactory protectors) : ISecretVault`:
  - `InitializeAsync`: no file -> `NotCreated`; file with a protector that does not need a passphrase -> try
    `Unwrap`, on success decrypt every entry into a `Dictionary<SecretRef, string>` cache and become `Unlocked`, on failure
    `Locked` (the UI explains); passphrase protector -> `Locked`.
  - `CreateAsync(protectorId, passphrase)`: generate the data key, wrap, write an empty vault atomically (temp file in the same
    directory, `File.Move(overwrite)`, `File.SetUnixFileMode(0600)` on Unix, create the directory with mode 0700), state `Unlocked`.
  - `UnlockAsync(passphrase)`: PBKDF2 on a background thread; false on wrong passphrase.
  - `Lock()`: clear the data key (`DataKey.Clear`), the cache and the secret-values list; state `Locked`.
  - `SetAsync`/`RemoveAsync`/`RemoveOwnerAsync`: update the cache, re-encrypt the changed entries and rewrite the file atomically
    under a `SemaphoreSlim`; a write failure throws `VaultException` and leaves cache and file consistent (write first, update
    the cache after).
  - `TryGet` and `GetSecretValues` read the cache without locking beyond a volatile snapshot (immutable dictionary swap).
- `PassphraseKeyProtector(string passphrase) : IKeyProtector` (`Id` = `passphrase`, `RequiresPassphrase` true) using
  `PassphraseKdf`; `PassphraseKeyProtectorFactory : IKeyProtectorFactory` (`Available()` = just the passphrase option; the
  per-platform factories of M3-48/M3-49 wrap it and add their protector first).

## Out of scope

OS protectors, decorators, UI, protector migration.

## Acceptance criteria

- [ ] Tests on a temp directory with a deterministic fake protector and the real passphrase protector (reduce PBKDF2 cost in
      tests through an internal iteration override, never in production code paths): create -> set -> lock -> unlock with the
      right and a wrong passphrase -> values back; a second `FileSecretVault` instance (restart) reads the same file; file
      content contains neither plaintext values nor the passphrase (scan the bytes for a distinctive value); tampering with one
      entry: that single entry is dropped from the cache and listed in `LoadWarnings` (new `IReadOnlyList<string>` property), the rest
      load; swapped entries (same ciphertext under another name) fail the AAD check; corrupt JSON and unknown version leave the
      file untouched; `RemoveOwnerAsync`; concurrent `SetAsync` calls all persist; Unix file mode 0600 (skipped on Windows);
      `Workspace.ReadWorkspaceId` creates and then keeps the id; `GetSecretValues` empty when locked.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

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

`feat(infrastructure): add the encrypted file secret vault`
