# M3-56: WinUI: vault dialogs, status bar item and DPAPI wiring

Status: todo
Platform: windows
Depends on: M3-45, M3-48, M3-53, M3-55

## Goal

The user can set up, unlock and lock the vault in the WinUI app, sees its state in the status bar, and DPAPI is the default
protector.

## Design decisions

- Read first: `src/Yaapic.App.WinUI/Views/EnvironmentsDialog.xaml(.cs)` and `FolderSettingsDialog.xaml(.cs)` (dialog patterns, the
  one-dialog rule), `Services/` (dialog service), `App.xaml.cs` (where `CreateShell` is called), M3-45 (`VaultViewModel`),
  M3-48 (`DpapiKeyProtectorFactory`), M3-53 (the named status bar slot).
- `App.xaml.cs`: pass `new DpapiKeyProtectorFactory()` to `AppComposition.CreateShell`.
- `Views/VaultDialog.xaml(.cs)` (`ContentDialog`, one per mode chosen by `Vault.State` at open time):
  - **Setup** (`NotCreated`): explanation text (`"Secret variables are encrypted and stored outside the workspace, on this computer
    only."`), a protector `RadioButtons` list from `Protectors` (label, and for `passphrase` the hint `"You will type it each
    time you open the workspace"`), `PasswordBox` passphrase + confirmation shown only when `SelectedProtector.RequiresPassphrase`,
    inline validation text from the view model, primary `Create vault` (`CanSubmitSetup`), `LastError` in an `InfoBar`.
  - **Unlock** (`Locked`): `PasswordBox`, primary `Unlock`, error `Wrong passphrase`, Enter submits.
  - Both clear the password boxes when closed.
  - `Vault.SetupRequested`/`UnlockRequested` open the dialog (wire in `MainWindow.xaml.cs`, like `EnvironmentsRequested`).
- Status bar item (slot of M3-53): lock icon + text `Vault locked` / `Vault unlocked` / `No vault`, button style like the Console
  toggle; click -> `RequestSetupCommand`/`RequestUnlockCommand`, or when `Unlocked` a flyout with `Lock vault`
  (`LockCommand`); tooltip shows `ProtectorLabel`.
- Palette commands (M3-47) already call the same commands; verify they open the dialogs.

## Out of scope

Changing passphrase, switching protector, deleting the vault, Avalonia (M3-63).

## Acceptance criteria

- [ ] Build 0 warnings, tests green, AOT publish 0 IL2xxx/IL3xxx.
- [ ] **Runtime check (AOT exe)** on a fresh workspace folder (delete `.yaapic/workspace.id` and the user vault file of the test
      workspace before starting; list what was deleted in Notes): the status bar shows `No vault`; setup with DPAPI creates the
      vault without a passphrase and the state becomes `Vault unlocked`; set a secret variable and send a request that uses it
      (the stub server echo shows the real value, the console shows `••••` for a `console.log` of it); restart the app: the vault is
      unlocked automatically and the value is back; lock from the status bar, secret rows empty, `{{secret}}` reported unresolved
      with the locked hint; setup of a **passphrase** vault in a second fresh workspace, restart, unlock with a wrong then the right
      passphrase; Esc cancels; dark and light; keyboard-only operation of the dialogs. Record in Notes.
- [ ] The vault file contains no plaintext secret (open it, record the check).
- [ ] No process left running.

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

`feat(winui): add vault setup and unlock dialogs and the status bar item`
