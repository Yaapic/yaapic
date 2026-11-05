# M3-46: Secret rows in the key/value editor and masking in variable hints

Status: todo
Platform: any
Depends on: M3-33, M3-45

## Goal

Variable tables let the user mark a row secret, secret values are masked everywhere they are displayed, and secret rows are
read-only while the vault is locked.

## Design decisions

- Read first: `src/Yaapic.ViewModels/Editing/KeyValueEditorViewModel.cs`, `KeyValueRowViewModel.cs`, `EnvironmentItemViewModel.cs`,
  `GlobalsViewModel.cs`, `FolderSettingsViewModel.cs`, `Requests/VariableHint.cs`, `VariablePopupViewModel.cs`,
  `Core/Variables/VariableScopes.cs`, `EnvironmentVariableSource.cs`.
- `KeyValueRowViewModel`: new `bool IsSecret` (observable), `bool IsRevealed` (observable, UI only, never persisted, reset on
  `Lock`), `string DisplayValue` (`"••••••••"` when secret, not revealed and the value is not empty, otherwise `Value`),
  `bool CanEditValue` (`!IsSecret || secretsUnlocked`), `ToggleRevealCommand`, `ToggleSecretCommand` (refused with `LastError` text
  `"Unlock the vault to mark a variable as secret"` when the vault is not `Unlocked`; turning a secret **off** keeps the value as
  plain text and shows a one-time hint `"This value will be stored in the workspace file."`).
- `KeyValueEditorViewModel`: `bool AllowSecrets` (default **false**; set true by the environment, globals and folder editors at
  their construction: params, headers, urlencoded and form-data editors never show it), `bool SecretsAvailable`
  (vault unlocked; set through a new `void SetVaultState(VaultState)` that `EnvironmentsViewModel`, `GlobalsViewModel` and
  `FolderSettingsViewModel` call when the vault changes: give them a `VaultViewModel`-independent hook,
  `IVaultStateSource` with `VaultState State` and `StateChanged`, implemented by `VaultViewModel` and faked in tests).
  `Load(entries)` and the entries output keep `IsSecret` (the M3-33 flag) so saves round trip it.
- Resolution/hints: `VariableScopes.TryResolve(name, out value, out scope, out bool isSecret)` overload (the existing one calls it);
  `VariableHint` gains `bool IsSecret`; `VariablePopupViewModel` exposes `DisplayValue` (masked when secret and not revealed),
  `IsSecret`, `RevealCommand`, and never puts a secret value in `EditableValue` when it is unresolved. Secret values still
  resolve normally when sending.
- Locked vault: secret rows load with `Value ""` (M3-37), so the variable is **unresolved** and the existing unresolved-variable
  warning shows; additionally `RequestEditorViewModel`/response panel add the hint line `"Vault is locked: secret variables are not
  available."` to `UnresolvedWarning` when any unresolved name is a known secret row of the active scopes that is empty because
  of the lock (implement through the hint: `IsSecret` true and value empty).

## Out of scope

Creating a secret from the popup form, views, changing the protector.

## Acceptance criteria

- [ ] Tests: toggle secret (unlocked) and refusal (locked); masking text, reveal and reset on lock; `AllowSecrets` false editors
      never expose the flag; flag round trip through `EnvironmentsViewModel` save with the secret-aware store and in-memory
      vault (YAML row blank, vault holds the value, reload shows the value, lock then reload shows empty); popup masks the value
      and `Reveal` shows it; hint `IsSecret`; locked-vault warning text; existing editor and popup tests unchanged and green.
- [ ] 0 warnings, XML docs, all tests green.

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

`feat(viewmodels): mark variables secret and mask their values`
