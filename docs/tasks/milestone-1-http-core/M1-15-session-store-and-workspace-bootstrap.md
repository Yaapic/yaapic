# M1-15: Session store and workspace bootstrap

Status: todo
Platform: any
Depends on: M1-13

## Goal

Persist open tabs and the active environment in `.yaapic/session.yaml`, and create the default workspace on first run.

## Design decisions

- `public sealed class FileSessionStore(string workspaceRoot) : ISessionStore`, file `.yaapic/session.yaml`, format of
  `docs/file-format.md` (internal `[YamlObject]` DTO, GUIDs as `D` strings, empty string for null).
  `LoadAsync`: missing file, invalid YAML, wrong version or invalid GUIDs -> `SessionState.Empty` (invalid GUIDs in
  `openRequests` are skipped, the rest kept). `SaveAsync`: `WorkspaceFiles.WriteAtomicAsync`.
- `public static class Workspace` in `Yaapic.Infrastructure.Storage`:
  - `string DefaultRoot()` = `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Yaapic", "Default")`.
  - `void EnsureCreated(string root)`: creates `root`, `requests/`, `environments/`, `.yaapic/`; writes `.gitignore`
    with the single line `.yaapic/` only if no `.gitignore` exists. Idempotent.
- Choosing another workspace folder is M2; M1 always opens `DefaultRoot()`.

## Out of scope

Workspace picker, several workspaces, restoring unsaved tab content (unsaved edits are lost on exit in M1).

## Acceptance criteria

- [ ] Tests on a temp directory: save/load round trip; missing file, garbage YAML, version 2 -> `Empty`; invalid GUID
      skipped; `EnsureCreated` creates the tree, is idempotent and never overwrites an existing `.gitignore`.
- [ ] 0 warnings.

## Suggested commit message

`feat(infrastructure): add session store and workspace bootstrap`
