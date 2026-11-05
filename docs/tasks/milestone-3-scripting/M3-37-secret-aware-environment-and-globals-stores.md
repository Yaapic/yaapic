# M3-37: Core: secret-aware environment and globals stores

Status: todo
Platform: any
Depends on: M3-34, M3-33

## Goal

Decorators that move secret values between the YAML stores and the vault, so the view models and the resolver see real
values and the files never contain them.

## Design decisions

- Read first: ADR 0010 (integration), `src/Yaapic.Core/Storage/IEnvironmentStore.cs`, `IGlobalsStore.cs`, the in-memory
  doubles, `EnvironmentsViewModel`/`GlobalsViewModel` (they call the stores and keep the loaded rows).
- `Yaapic.Core.Secrets.SecretAwareEnvironmentStore(IEnvironmentStore inner, ISecretVault vault) : IEnvironmentStore` and
  `SecretAwareGlobalsStore(IGlobalsStore inner, ISecretVault vault) : IGlobalsStore`:
  - **Load**: call the inner store, then for each row with `IsSecret`: `vault.TryGet(ref)` -> value, otherwise `""` (locked,
    missing or never set). Non-secret rows are untouched.
  - **Save** (`vault` `Unlocked`): for each secret row, `vault.SetAsync(ref, row.Value)` then write the row to the inner store
    with `Value = ""`; for secret refs of this owner that exist in the vault but are no longer present as secret rows (row
    deleted, renamed, or unflagged) `RemoveAsync`; an unflagged row keeps its value in the YAML (the value was just loaded,
    so it is in the row). A vault failure aborts the save before the inner store is touched and rethrows as
    `StorageException` (message "Cannot save secret '<name>': <vault message>") so existing view-model error paths show it.
  - **Save while the vault is `Locked` or `NotCreated`**: the vault is **not** touched; secret rows are written with
    `Value = ""` (their real values stay in the vault, nothing is lost); non-secret rows save normally. Rows newly flagged as
    secret while locked are refused with `StorageException("Unlock the vault to mark '<name>' as secret")` (the UI also blocks it, M3-46).
  - `DeleteAsync(id)` (environments) also `RemoveOwnerAsync(Environment, id)` when unlocked.
  - Identity: environment rows use `SecretOwnerKind.Environment` + the environment id; globals use `Globals` + `Guid.Empty`.
- Duplicate environment (M2-42) goes through `SaveAsync` of a new environment with copied rows: the decorator must store the
  copied secret values under the new id (covered by the load/save rules; add a test).
- Not here: collection variables (M3-38), reloading when the vault state changes (M3-45/M3-47 subscribe to `StateChanged`).

## Out of scope

UI, folder variables, masking.

## Acceptance criteria

- [ ] Tests with the in-memory store and vault doubles: load fills secret values and leaves others; load while locked gives
      empty values; save unlocked writes the vault and blanks the inner rows (assert the inner store content has `Value ""`
      and `IsSecret true`); save locked keeps the vault untouched and refuses new secret flags; removed/renamed/unflagged rows
      clean the vault; vault failure leaves the inner store untouched and surfaces `StorageException`; environment delete
      removes the owner; duplicate environment keeps secrets; globals equivalents.
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

`feat(core): keep secret variable values in the vault`
