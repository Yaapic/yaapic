# M2-45: Shell: globals

Status: todo
Platform: any
Depends on: M2-44

## Goal

`ShellViewModel` exposes and loads the globals editor.

## Design decisions

- `ShellViewModel` gains the constructor parameter `GlobalsViewModel globals` (after `environments`), the property
  `Globals`, and `InitializeAsync` awaits `Globals.LoadAsync()` right after `Environments.LoadAsync(...)`.
- `RequestCloseAsync` is unchanged: unsaved environment and globals edits are in memory only, as for environments in M1.
- `AppComposition` passes the same `GlobalsViewModel` it gave to the provider (M2-44).
- Update the tests that build a `ShellViewModel`.

## Out of scope

Views, a prompt for unsaved globals when closing.

## Acceptance criteria

- [ ] Tests: after `InitializeAsync` the globals of a temp workspace (`globals.yaml` with one variable) are in
      `Shell.Globals.Variables`; a send from the shell resolves `{{thatVariable}}` (add to the end-to-end test of M1-32 or
      a new `AppCompositionTests` case); a corrupt `globals.yaml` leaves `Globals.LastError` set and the shell usable.
- [ ] 0 warnings.

## Suggested commit message

`feat: expose the globals editor from the shell`
