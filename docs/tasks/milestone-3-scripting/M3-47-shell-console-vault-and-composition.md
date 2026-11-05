# M3-47: Shell, palette and composition: engines, console, vault

Status: todo
Platform: any
Depends on: M3-17, M3-25, M3-38, M3-43, M3-44, M3-45, M3-46

## Goal

Everything is wired: `AppComposition.CreateShell` builds the engines, the scripted sender, the vault with its decorators, the
console and the vault view model; the shell exposes the console panel state, palette commands and engine warnings.

## Design decisions

- Read first: `src/Yaapic.Composition/AppComposition.cs`, `ShellViewModel.cs`, `Palette/CommandsPaletteSource.cs`,
  `tests/Yaapic.Infrastructure.Tests/Composition/`, M3-14 (arch rule allowing Composition -> Scripting).
- `AppComposition.CreateShell(string workspaceRoot, IDialogService dialogs, IKeyProtectorFactory? platformProtectors = null)`:
  - Engines: `ScriptEngineRegistry` with `QuickJsEngine` and `LuaEngine`; `ScriptedRequestSender(sender, registry, TimeProvider.System,
    redactor)`; `CollectionTreeScriptChainProvider`; one `ConsoleViewModel`; `ScriptSendServices` passed to every editor created
    by the tab factory (the existing `FromDefinition` call).
  - Vault: `FileSecretVault(workspaceRoot, new DefaultVaultLocation(), protectors)` where `protectors` =
    `platformProtectors ?? new PassphraseKeyProtectorFactory()`; the three `SecretAware*` store decorators wrap the file stores
    **before** they are given to the view models; `SecretRedactor`; `VaultViewModel`; the shell implements `IVaultReloader`
    (reload environments, globals and the collection tree) and `IVaultStateSource` is the vault view model.
  - Each App project passes its platform factory (M3-48 WinUI, M3-49 Avalonia); until then they pass null (passphrase only).
- `ShellViewModel` gains: `ConsoleViewModel Console`, `VaultViewModel Vault`, `bool IsConsoleOpen` (observable), `double
  ConsoleHeight` (default 250, clamped 120..600, matches the mockup `height:250px`), `ToggleConsoleCommand`, `ClearConsoleCommand`
  (clear + keep open), `OpenConsoleCommand`, `IReadOnlyList<string> ScriptEngineWarnings` (one line per unavailable engine from
  `IsAvailable`, such as `"JavaScript engine unavailable: <reason>"`, evaluated in `InitializeAsync`), `bool HasScriptEngineWarnings`.
  Opening the console automatically on the first **script error** or failed test of a send is **not** done (the response tab
  shows them); the status bar indicator (views) shows the counts.
- Session: `IsConsoleOpen` and `ConsoleHeight` are **not** persisted (no session-file change).
- Palette (`CommandsPaletteSource`): `Toggle console` (shortcut text `Ctrl+\``), `Clear console`, `Set up the secret vault...`
  (only when `NotCreated`), `Unlock the vault...` (when `Locked`), `Lock the vault` (when `Unlocked`), each calling the shell/vault
  commands; update the palette tests that count commands.
- `InitializeAsync` also initialises the vault (`Vault.InitializeAsync`) **before** the environments and globals load, so an
  automatically unlocked vault fills the secrets on the first load.

## Out of scope

Views, the OS protectors themselves, the E2E tests.

## Acceptance criteria

- [ ] Composition tests (`Yaapic.Infrastructure.Tests/Composition`, real files, fake dialogs, `FakeScriptEngine` registered
      through an internal seam or the real engines when the natives are present — mark the real-engine test with a trait and skip
      it with a clear message when the library is absent): shell creation succeeds with and without natives; a script-bearing
      request sends and fills console and tests; secret variable saved through the environments view model is absent from the
      YAML and present in the vault file as ciphertext; lock -> reload shows empty secret values; console toggle/clear;
      palette commands and their enabled state per vault state; engine warnings when a library is unavailable.
- [ ] Existing shell, composition and palette tests updated only where the command count changed, otherwise green.
- [ ] 0 warnings, XML docs, architecture tests green (`Yaapic.ViewModels` still never references `Yaapic.Scripting`).

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

`feat: wire script engines, console and secret vault into the shell`
