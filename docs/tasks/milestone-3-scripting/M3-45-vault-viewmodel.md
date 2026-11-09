# M3-45: Vault ViewModel

Status: done
Platform: any
Depends on: M3-34, M3-37

## Goal

The state machine behind the vault UI: set up, unlock, lock, and reload the secret-bearing view models when the state changes.

## Design decisions

- Read first: ADR 0010 (states, protector choice, passphrase rules), M3-34 (`ISecretVault`, `IKeyProtectorFactory`),
  `EnvironmentsViewModel`, `GlobalsViewModel`, `CollectionTreeViewModel` (their load/refresh methods), `IDialogService`.
- `src/Yaapic.ViewModels/Vault/VaultViewModel.cs` (`ObservableObject`), constructor `(ISecretVault vault, IKeyProtectorFactory protectors,
  IVaultReloader reloader)` where `IVaultReloader.ReloadAsync()` is a tiny interface the shell implements to refresh environments,
  globals and the collection tree (so this view model does not know them):
  - `VaultState State`, `string StatusText` (`"No vault"`, `"Vault locked"`, `"Vault unlocked"`), `string? ProtectorLabel`
    (`"Windows account (DPAPI)"`, `"macOS Keychain"`, `"Secret Service"`, `"Passphrase"`), `bool IsUnlocked`, `bool NeedsPassphrase`
    (state `Locked`), `bool CanSetUp` (`NotCreated`).
  - Setup form: `IReadOnlyList<VaultProtectorChoice> Protectors` (`sealed record VaultProtectorChoice(string Id, string Label,
    bool RequiresPassphrase)`, from the factory, the platform protector first, `passphrase` always last), `SelectedProtector`,
    `Passphrase`, `ConfirmPassphrase`, `CanSubmitSetup` (passphrase rules only when the protector needs one: **at least 8
    characters**, confirmation equal), `SetupCommand` (async: `vault.CreateAsync`; success -> reload; failure -> `LastError`).
  - Unlock: `UnlockPassphrase`, `UnlockCommand` (false -> `LastError = "Wrong passphrase"`, success clears the field and reloads),
    `LockCommand` (`vault.Lock()` then reload so secret values disappear from open editors).
  - `LastError`, `IsBusy`, `InitializeAsync()` (calls `vault.InitializeAsync`, then raises the first reload).
  - Subscribes to `vault.StateChanged`: updates the properties and calls `reloader.ReloadAsync()` once per change (not for no-op
    transitions). Passphrase text is cleared after every submit and on lock.
  - `event EventHandler? SetupRequested` / `UnlockRequested` raised by `RequestSetupCommand` / `RequestUnlockCommand` (so the status
    bar item and the palette open the right dialog in each UI: `NotCreated` -> setup, `Locked` -> unlock, `Unlocked` -> lock).
- The shell implements `IVaultReloader` (M3-47).

## Out of scope

Changing passphrase, switching protector, deleting the vault, views.

## Acceptance criteria

- [x] Tests with `InMemorySecretVault` and a recording reloader: state texts, protector choices order, setup validation matrix
      (short passphrase, mismatch, OS protector without passphrase), successful setup/unlock/lock reload exactly once, wrong
      passphrase message, passphrase fields cleared, busy flag during an awaited slow vault, `RequestXCommand` raises the right
      event per state.
- [x] 0 warnings, XML docs, all tests green.

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

`feat(viewmodels): add the vault view model`
