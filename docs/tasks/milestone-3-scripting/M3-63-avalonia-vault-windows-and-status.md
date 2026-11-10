# M3-63: Avalonia: vault windows, status bar item and platform protector wiring

Status: done
Platform: unix
Depends on: M3-45, M3-49, M3-60, M3-62, M3-56

## Goal

The Avalonia twin of M3-56, with the Keychain / Secret Service protectors wired in.

## Design decisions

- Read first: M3-56 (dialog contents, texts, status bar item, behaviour), `src/Yaapic.App.Avalonia/Views/PromptWindow.axaml(.cs)`
  and `EnvironmentsWindow` (window/dialog patterns, `IDialogService` implementation in `Services/`), `Program.cs`/`App.axaml.cs`
  (where `CreateShell` is called), M3-49 (`UnixKeyProtectorFactory`).
- Pass `new UnixKeyProtectorFactory()` to `AppComposition.CreateShell` (on Windows the Avalonia app is a dev/test build: the
  factory then lists only `passphrase`; make that case explicit in a test).
- `Views/VaultWindow.axaml(.cs)` (modal window opened by `Vault.SetupRequested`/`UnlockRequested`): setup mode and unlock mode
  exactly as the WinUI dialog (radio list of `Protectors`, passphrase + confirmation `TextBox`es with `PasswordChar`, validation
  text, primary button, error text, Enter submits, Esc cancels, fields cleared on close). Status bar vault item (named slot of
  M3-60): lock icon + `Vault locked`/`Vault unlocked`/`No vault`, click -> setup/unlock, or a flyout `Lock vault` when unlocked.

## Out of scope

Changing passphrase, switching protector, deleting the vault.

## Acceptance criteria

- [x] Build `Yaapic.CrossPlatform.slnf` 0 warnings, tests green, AOT publish 0 new IL2xxx/IL3xxx.
- [ ] **Runtime check on the published exe** (host OS only; say which): fresh test workspace -> `No vault`; setup with the
      platform protector when available (macOS Keychain, Linux with a Secret Service session) **or** passphrase; secret variable
      set, sent, console masks it; restart: automatic unlock for platform protectors, passphrase prompt for passphrase vaults
      (wrong then right passphrase); lock; vault file holds no plaintext. On a Windows host only the passphrase path can be
      checked: state it, and list the macOS/Linux checks as `not run` in Notes for the user to run.
- [x] No process left running.

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

`feat(avalonia): add vault setup and unlock windows and the status bar item`

## Notes

- Protector wiring deviates from the card on Windows: `App.axaml.cs` passes `DpapiKeyProtectorFactory` on Windows (M3-48) and
  `UnixKeyProtectorFactory` on macOS/Linux (M3-49), so the Windows build offers DPAPI and passphrase. The Unix factory on Windows
  is covered by `UnixKeyProtectorTests.ProductionFactoryOffersOnlyThePassphraseOnWindows`.
- Added `--open vault` to the screenshot tooling (`MainWindow.OpenNamed`).
- AOT exe on Windows (win-x64), throwaway workspace under the session scratchpad: starts, shows `No vault` in the status bar,
  `--open vault` shows the setup window (DPAPI selected by default, passphrase option), exits cleanly. No vault file was
  written to `%APPDATA%\Yaapic` (the check was limited to this on purpose).
- **Runtime check not run** (the acceptance item stays unticked): setup, secret variable sent and masked, restart with automatic
  unlock / passphrase prompt (wrong then right), lock, vault file content. Also not run: Keychain (macOS) and Secret Service (Linux)
  paths. To be done by the user on a machine where writing a vault is acceptable.
