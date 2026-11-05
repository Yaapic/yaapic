# M3-49: macOS Keychain and Linux Secret Service key protectors

Status: todo
Platform: unix
Depends on: M3-36, M3-47

## Goal

On macOS and Linux the vault can be unlocked automatically through the platform's secret store, with the passphrase protector
as the fallback.

## Design decisions

- Read first: ADR 0010 (protector list; Linux uses `secret-tool` on stdin, never arguments), M3-34 (`IKeyProtector`),
  M3-36, Apple's Security framework docs for `SecKeychainAddGenericPassword`/`SecKeychainFindGenericPassword` (legacy but
  available) or `SecItemAdd`/`SecItemCopyMatching` (modern CFDictionary API, needs CoreFoundation P/Invokes: choose the legacy
  functions if the modern ones need more than ~15 P/Invoke declarations; record the choice).
- **macOS** `MacKeychainKeyProtector` (`Id` `keychain`): `[SupportedOSPlatform("macos")]`, `LibraryImport("/System/Library/Frameworks/Security.framework/Security")`
  declarations (blittable only), service `Yaapic`, account `<workspaceId>`. `Wrap` = generate nothing: the **data key itself is the
  stored secret**; `WrappedKey.Bytes` holds a short marker (`keychain-v1`) because the key lives in the Keychain; `Unwrap` reads the
  item and returns the 32 bytes (errors -> `VaultException("The macOS Keychain refused access")`). Updating an existing item is
  delete + add. `IsAvailable` = `OperatingSystem.IsMacOS()`. **This card cannot be tested on Windows or Linux**: write the P/Invoke
  layer, the logic and the unit tests with an internal `IKeychainApi` seam (fake in tests), and state clearly in Notes that the
  real Keychain path needs a macOS run (CI `macos-latest` runs the tests with the fake, plus one real round trip test marked
  `[Fact]` on macOS only, creating and deleting an item named `Yaapic.Test.<guid>`).
- **Linux** `SecretToolKeyProtector` (`Id` `secret-service`): runs `secret-tool` (`Process.Start` with `ArgumentList`, redirected
  stdin/stdout/stderr, 5 s timeout): store = `secret-tool store --label "Yaapic vault" service Yaapic account <workspaceId>` with
  the base64 data key written to **stdin**; lookup = `secret-tool lookup service Yaapic account <workspaceId>`. `IsAvailable` =
  `OperatingSystem.IsLinux()` and `secret-tool` found on `PATH` and a probe `secret-tool lookup service Yaapic account __probe__`
  exiting with 0 or 1 (a DBus failure exits with another code or times out -> unavailable with the reason). Never log the key,
  never put it in arguments or environment variables. Process APIs are AOT safe (no reflection).
- `UnixKeyProtectorFactory : IKeyProtectorFactory` (Infrastructure): macOS `[keychain, passphrase]`, Linux
  `[secret-service (when available), passphrase]`; the Avalonia `Program` passes it to `AppComposition.CreateShell`.
- If the Keychain or Secret Service turns out infeasible in AOT (P/Invoke or process problems), stop and report: the fallback is
  passphrase-only on Unix, decided by the user.

## Out of scope

Windows, Avalonia dialogs (M3-63), keyring other than Secret Service (KWallet without the Secret Service API).

## Acceptance criteria

- [ ] Tests with the seams: wrap/unwrap logic, error mapping, unavailable reasons (no `secret-tool`, probe failure, non-macOS),
      factory ordering; on Linux CI a real `secret-tool` test is **skipped** (no D-Bus session on the runner) with a clear reason;
      on macOS CI the real round trip test runs.
- [ ] `dotnet build Yaapic.CrossPlatform.slnf -c Release` 0 warnings; AOT publish of `Yaapic.App.Avalonia` on the host RID without
      new IL2xxx/IL3xxx (on Windows host the Unix code compiles but is unused: run the analyzers anyway).
- [ ] Notes list what was verified on which OS and what still needs a macOS/Linux run.

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

`feat(infrastructure): protect the vault key with Keychain and Secret Service`
