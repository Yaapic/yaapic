# M6-81: ViewModel: remove the tool window events

Status: todo
Platform: any
Depends on: M6-78, M6-80

## Goal

No view opens a tool window any more (M6-78, M6-80): remove the shell events that asked for them (ADR 0023), so `Section` is the only
way the shell shows a tool.

## Design decisions

- Read first: M6-76, `src/Yaapic.ViewModels/Shell/ShellViewModel.Automation.cs`, `tests/Yaapic.ViewModels.Tests/AutomationShellTests.cs`,
  `CommandsPaletteSourceTests.cs`, `PaletteSourcesTests.cs`.
- Delete `RunnerRequested`, `LoadTestRequested`, `MockServersRequested`, `MonitorsRequested` and their `Invoke` calls. Keep the names
  `RequestRunner()`, `RequestLoadTest()`, `RequestMockServers()`, `RequestMonitors()` (the palette, the status bars and the folder
  commands call them); their doc comments become "Shows the Runner in the main area; does nothing when the tools are not attached." (and
  the matching tool). Update the `AttachAutomation` and `Runner`..`Monitors` doc comments that mention windows.
- Tests that counted the events assert `Section` instead (same cases: not attached -> unchanged; each entry -> its section).

## Out of scope

Renaming the `Request*` methods; view changes.

## Acceptance criteria

- [ ] `rtk grep -rn "RunnerRequested\|LoadTestRequested\|MockServersRequested\|MonitorsRequested" src tests` finds nothing (outside `obj/`).
- [ ] Both front-ends build; architecture tests green; 0 warnings, XML docs, all tests green.

## Suggested commit message

`refactor(viewmodels): remove the tool window events from the shell`
