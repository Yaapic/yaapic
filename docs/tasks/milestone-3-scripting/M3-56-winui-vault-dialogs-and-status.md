# M3-56: WinUI: vault dialogs, status bar item and DPAPI wiring

Status: in progress
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

- [x] Build 0 warnings, tests green, AOT publish 0 IL2xxx/IL3xxx.
- [ ] **Runtime check (AOT exe)** on a fresh workspace folder (delete `.yaapic/workspace.id` and the user vault file of the test
      workspace before starting; list what was deleted in Notes): the status bar shows `No vault`; setup with DPAPI creates the
      vault without a passphrase and the state becomes `Vault unlocked`; set a secret variable and send a request that uses it
      (the stub server echo shows the real value, the console shows `••••` for a `console.log` of it); restart the app: the vault is
      unlocked automatically and the value is back; lock from the status bar, secret rows empty, `{{secret}}` reported unresolved
      with the locked hint; setup of a **passphrase** vault in a second fresh workspace, restart, unlock with a wrong then the right
      passphrase; Esc cancels; dark and light; keyboard-only operation of the dialogs. Record in Notes.
- [ ] The vault file contains no plaintext secret (open it, record the check).
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

`feat(winui): add vault setup and unlock dialogs and the status bar item`

## Notes

Runtime check, AOT exe, driven through UI Automation on throwaway workspaces under `%TEMP%` (nothing deleted; the vault files
created by this check are `%APPDATA%\Yaapicaults\<id>.vault` of those temp workspaces):

- Verified: status bar `No vault`; DPAPI setup creates the vault without a passphrase and the state becomes `Vault unlocked`;
  restart: unlocked automatically; status bar click on an unlocked vault opens the `Lock vault` flyout and locks it; passphrase
  setup (hint `Use at least 8 characters`, `Passphrases do not match`, Enter submits), restart locked, wrong passphrase shows
  `Wrong passphrase` and stays open, right passphrase unlocks, Esc cancels; dark and light screenshots of the setup dialog.
- The vault file holds only the protector id and wrapped/encrypted data (`"protector": "dpapi"`, `wrappedKey`, 462 bytes);
  no secret variable was stored, so the no-plaintext check is only structural.
- Second pass (maximized window, stub echo server on 127.0.0.1:18080): a secret global `tok` is masked in the table; the request
  `.../echo?x={{tok}}` echoes the real value; the console shows `••••` for `console.log(pm.variables.get('tok'))` and the request
  line shows `x=••••`; after a restart the vault unlocks by itself and the value is back; neither the vault file (712 bytes) nor
  the workspace folder contains the plaintext value.
- **Open defect**: after `Lock vault`, the same request is sent with `x=` (empty) but the response panel and the console show no
  `Vault is locked: secret variables are not available.` warning, although `RequestEditorViewModel.UsesLockedSecret` should add
  it. The locked secret rows of the table were not inspected either. The card stays in progress until this is understood
  (likely M3-55 scope: the hint of a secret global is probably not marked secret while locked).
- Not checked: palette commands, a keyboard-only pass beyond Enter/Esc.
- AOT finding: binding `RadioButtons.ItemsSource` to an `IReadOnlyList<T>` kills the process (stowed XAML exception), as already
  documented in `ListMap`; fixed with `ListMap.Protectors`.
