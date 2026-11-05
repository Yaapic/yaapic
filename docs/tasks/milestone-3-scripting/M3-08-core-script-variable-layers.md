# M3-08: Core: script variable layers and change tracking

Status: done
Platform: any
Depends on: M3-07

## Goal

The variable half of `IScriptHost`: a layered overlay over the environment, collection and globals that scripts read and
write during one send, remembers every change, and feeds the normal resolver afterwards.

## Design decisions

- Read first: `src/Yaapic.Core/Variables/VariableScopes.cs`, `VariableScopeKind.cs`, `EnvironmentVariableSource.cs`,
  `src/Yaapic.ViewModels/Requests/VariablePopupViewModel.cs` and `VariableContext.cs` (they switch over `VariableScopeKind`).
- `VariableScopeKind` gains `Local` **after** `Dynamic` (so existing numeric order is untouched). `VariableScopes` gains
  an optional last parameter `IReadOnlyList<KeyValueEntry>? Local = null`; `CreateSource()` and `TryResolve` consult
  `Local` **first**. Grep every `switch` / label mapping over `VariableScopeKind` (popup labels, write scopes) and handle
  `Local` explicitly (label `"Local"`, never writable through the provider). Existing tests stay green unchanged.
- `public sealed record VariableChange(VariableScopeKind Scope, string Name, string? Value)`: `Value == null` means
  *unset*; `public bool IsUnset`. A "clear scope" is recorded as one unset per existing enabled row.
- `public sealed class ScriptVariableOverlay(VariableScopes initial, bool hasEnvironment, bool hasCollection)` in
  `Yaapic.Core.Scripting`:
  - Holds mutable copies of the three scope lists (rows keep their `IsSecret` flag (M3-33); copy whole
    `KeyValueEntry` records) and a `Local` dictionary.
  - `TryGet(ScriptScope scope, string name, out string value)`: `Layered` = Local, Environment, Collection, Globals,
    Dynamic (dynamic variables are only visible through `Layered` and `ReplaceIn`, never listed); other scopes read that
    layer only (a disabled row is invisible, like the resolver).
  - `Set(ScriptScope scope, string name, string value)`: `Layered` and `Local` write the local dictionary; `Environment`
    writes the environment list when `hasEnvironment`, `Collection` when `hasCollection`, `Globals` always. If the target
    scope does not exist, write `Local` instead and set `Warnings` (a list of strings such as
    `"No active environment: the value of 'x' only lives for this send"`). Upsert by exact name: an existing row (enabled or
    not) gets the value and becomes enabled; otherwise a row is appended. Every write to a persisted scope adds a
    `VariableChange`; repeated writes of one name collapse to the last value (keep first-write order).
  - `Unset`, `Clear`, `List(scope)`, `ReplaceIn(string)` (same `{{ name }}` rules as `VariableResolver`, resolves through
    `ToScopes().CreateSource()`; unresolved tokens stay as written).
  - `VariableScopes ToScopes()` builds the current scopes including `Local`; `IReadOnlyList<VariableChange> Changes`;
    `IReadOnlyList<string> Warnings`.
- Pure, synchronous, no I/O, no threading (the host serialises calls; document it).

## Out of scope

Persisting the changes (M3-44), the host wrapper that adds request/response (M3-11), secrets masking (M3-46).

## Acceptance criteria

- [x] Tests (`Yaapic.Core.Tests`): layer precedence for `Layered`; each scope read/write; upsert of an existing and of a
      disabled row; write to a missing environment / collection falls back to Local with a warning and **no**
      `VariableChange`; collapse of repeated writes; unset and clear produce unset changes for persisted scopes only;
      `ReplaceIn` with local, environment, dynamic (`{{$guid}}` replaced by a GUID) and unresolved names; `ToScopes()`
      resolves a Local value with highest priority; existing resolver and popup tests unchanged and green.
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

`feat(core): add layered script variables with change tracking`
