# M3-44: Persist the variable changes made by scripts

Status: todo
Platform: any
Depends on: M3-43, M3-08

## Goal

`pm.environment.set`, `pm.collectionVariables.set`, `pm.globals.set` and their `unset`/`clear` survive the send: the changes
recorded by the overlay are written through the existing view models and stores, and the highlighting refreshes.

## Design decisions

- Read first: `src/Yaapic.ViewModels/Requests/VariableScopeProvider.cs` and `IVariableScopeProvider.cs` (`SetVariableAsync`,
  `GetWritableScopes`, the three write paths from M2-71), M3-08 (`VariableChange`), M3-43 (`IScriptVariableApplier`).
- `IVariableScopeProvider` gains `Task<string?> UnsetVariableAsync(Guid requestId, VariableScopeKind scope, string name)` (removes
  the row of that exact name; no error when absent; same scope checks and error strings as `SetVariableAsync`) and
  `Task<IReadOnlyList<string>> ApplyChangesAsync(Guid requestId, IReadOnlyList<VariableChange> changes)`. `FakeVariableScopeProvider`
  records both. Implementation in `VariableScopeProvider`: group the changes **by scope**, apply every set/unset of a scope to the
  editor view model first and **save once per scope** (environment, globals, collection folder), return one error message per failed
  scope; a change for a scope that is not in `GetWritableScopes(requestId)` is skipped silently (the overlay already downgraded it to
  local). `Local` and `Dynamic` changes never reach the provider.
- Secret rows: an upsert on an existing secret row keeps `IsSecret` and writes the new value through the normal save (the
  `SecretAware*` decorators then store it in the vault); an upsert creating a new row is **not** secret.
- `VariableScopeProvider` implements `IScriptVariableApplier` (or a small `ScriptVariableApplier` class in the same folder that
  delegates to `ApplyChangesAsync`): `ApplyAsync(requestId, changes)` returns the error messages.
- After the write, `ScopesChanged` is raised once (not once per variable) so the inline highlighting and popups refresh; verify
  the M2-71 provider fix (active-environment variable edits raise the event) is in place and do not double-raise.
- Known limitation to record in Notes: saving the environment or globals also saves unsaved edits of the same scope held by an
  open dialog (same limitation as M2-71).

## Out of scope

Conflict handling with a dialog open, undo of script writes, a "variables changed by script" UI.

## Acceptance criteria

- [ ] Tests with the real view models and in-memory stores: set then read back through `GetScopes` for each scope; unset
      removes the row; 5 changes to the environment save once (count store writes); mixed scopes save once each; non-writable
      scope skipped; store failure returns a message for that scope only and the other scopes still save; existing secret row
      keeps `IsSecret`; `ScopesChanged` raised exactly once per `ApplyChangesAsync`; the end-to-end case: a pre-request script
      (fake engine calling the host) sets an environment variable and the next `Tokenize` of the URL shows it resolved.
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

`feat(viewmodels): persist variable changes made by scripts`
