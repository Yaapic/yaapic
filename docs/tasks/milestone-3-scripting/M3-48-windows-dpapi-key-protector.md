# M3-48: Windows: DPAPI key protector

Status: done
Platform: windows
Depends on: M3-36, M3-47

## Goal

On Windows the vault is created and unlocked automatically for the current user, with the data key protected by DPAPI.

## Design decisions

- Read first: ADR 0010 (protectors), M3-34 (`IKeyProtector`, `IKeyProtectorFactory`), M3-36 (`PassphraseKeyProtectorFactory`),
  `docs/dependencies.md`.
- Package `System.Security.Cryptography.ProtectedData` (MIT, first-party; check the latest stable that targets `net11.0`) added to
  `Directory.Packages.props` and referenced by `Yaapic.Infrastructure.csproj` **unconditionally** (the library is
  cross-platform; the API throws `PlatformNotSupportedException` off Windows, which the
  protector turns into `IsAvailable == false`). Verify restore, build and AOT analysis: 0 warnings. Add the dependencies row
  (version, justification, AOT result).
- `src/Yaapic.Infrastructure/Secrets/DpapiKeyProtector.cs`: `Id` `dpapi`, `RequiresPassphrase` false,
  `IsAvailable` = `OperatingSystem.IsWindows()` and a 1-byte protect/unprotect probe; `Wrap` =
  `ProtectedData.Protect(dataKey, entropy, DataProtectionScope.CurrentUser)` where `entropy` = UTF-8
  `yaapic-vault-v1|<workspaceId>` (so a blob is bound to the workspace); `Unwrap` reverses it, `CryptographicException` ->
  `VaultException("Windows could not unlock the vault for this user")`. `[SupportedOSPlatform("windows")]` annotations so the
  analyzers pass.
- `DpapiKeyProtectorFactory : IKeyProtectorFactory` returns `[dpapi, passphrase]` on Windows, `[passphrase]` elsewhere
  (class lives in Infrastructure, no App code); `Create(id, passphrase)` builds either. The WinUI `App` passes it to
  `AppComposition.CreateShell` (one line in `App.xaml.cs`).
- First creation picks `dpapi` by default (the setup dialog preselects the first choice, M3-56).

## Out of scope

The WinUI dialogs (M3-56), macOS and Linux (M3-49), enterprise DPAPI scopes.

## Acceptance criteria

- [x] Tests (Windows only, `[Fact]` skipped elsewhere with a reason): wrap/unwrap round trip, a blob for workspace A fails for
      workspace B, tampered blob fails with `VaultException`; `FileSecretVault` full cycle with the real DPAPI protector
      (create, set, restart instance -> auto `Unlocked` with the value back, `Lock`/`Unlock` is passphrase-only so `UnlockAsync` is
      not used here); factory lists `dpapi` first.
- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; AOT publish of `Yaapic.App.WinUI` 0 IL2xxx/IL3xxx; launch the published
      exe, create a secret variable (the UI lands in M3-55/M3-56: until then check with the composition test) — runtime check
      deferred to M3-56, say so in Notes.
- [x] `docs/dependencies.md` row added; no process left running.

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

`feat(infrastructure): protect the vault key with DPAPI on Windows`

## Notes

- `System.Security.Cryptography.ProtectedData` 10.0.14 (latest stable; the package ships a `net10.0` asset that `net11.0` consumes).
- `Wrap`/`Unwrap` guard with `OperatingSystem.IsWindows()` (CA1416) and raise `VaultException` elsewhere; `IsAvailable` is false off Windows.
- AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx. Launching the published exe and creating a secret variable is deferred to M3-56 (the
  UI lands there); the composition-level behaviour is covered by `VaultAutoUnlocksAfterRestart`.
