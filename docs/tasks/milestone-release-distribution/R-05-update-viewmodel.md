# R-05: Update ViewModel and command palette entries

Status: todo
Platform: any
Depends on: R-03

## Goal

The state machine the two UIs bind to, tested with the fake service.

## Design decisions

- `UpdateViewModel`: states `Idle`, `Checking`, `Available`, `Downloading(percent)`, `ReadyToRestart`, `UpToDate`, `Failed`,
  `Unsupported`. Commands: `CheckNow`, `Download`, `RestartAndApply`, `Dismiss` (hides the banner for this version only).
- Settings section ViewModel: channel picker, "check automatically" toggle, the current version, last check time.
- Palette entries: "Check for updates", "Restart to update" (only when ready).
- The startup check runs through `UpdateScheduler` after the shell is shown, never blocking startup, cancelled on exit.
- No UI text hard-coded outside resources already used by the other ViewModels.

## Out of scope

Views.

## Acceptance criteria

- [ ] Transition tests for every state including cancel during download and failure then retry.
- [ ] A dismissed version does not reappear until a newer one exists.
- [ ] `Unsupported` hides the banner and disables the commands.

## Suggested commit message

`feat: add the update view model and palette entries`
