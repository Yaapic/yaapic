# M6-82: Docs: automation views in the main window

Status: todo
Platform: any
Depends on: M6-81

## Goal

The user and contributor docs describe the tools as views of the main window reached from the rail (ADR 0023), not as windows.

## Design decisions

- Read first: ADR 0023, `docs/automation.md`, `docs/architecture.md` (the paragraph after the M6 diagram, "The shell opens the four tools
  through `ToolWindowHost`..."), the "Milestone 6 summary" of `docs/tasks/README.md`.
- `docs/automation.md`:
  - Intro: "They open as separate, non-modal windows (one of each at a time) from the folder context menu, the command palette and the
    status bar." becomes "Each one has an entry on the left rail (under Collections and History) and opens in the main window, in place of
    the request tabs; the folder context menu, the command palette and the status bar lead to the same views. Collections or History,
    or opening a request, brings the request tabs back as you left them. A tool keeps working while another view is shown."
  - The entry-point table gets a first column "Rail" (Runner, Load test, Mock servers, Monitors: the tool's icon and tooltip).
  - Replace "window" by "view" where it means the tool: "that order exists only in the open window" -> "that order is kept while Yaapic
    runs"; "The window previews" -> "The Runner previews"; "closing the window does not stop the run" -> "showing another view does not
    stop the run"; "exported by hand from the window" -> "exported by hand from the Runner"; "The window shows live tiles" -> "The view
    shows live tiles"; "The window shows the" (monitors) -> "The Monitors view shows the"; "The window shows the reader's message" ->
    "The Runner shows the reader's message"; "exported from the Runner window" -> "exported from the Runner". Leave the CLI wording
    around M6-74 otherwise unchanged.
  - Load test confirmation: "The first start in each window session asks" -> "The first start after Yaapic starts asks".
  - Add one sentence where closing is described (mock servers: "...stop when the app closes"): "Closing Yaapic while a run, a load test or
    a mock server is running asks first."
- `docs/architecture.md`: replace "The shell opens the four tools through `ToolWindowHost` (one window of each kind, `ShowOrActivate`): the
  folder context menu, the palette (...) and the status bar items (...)." with "`ShellViewModel.Section` (`ShellSection`: `Requests`,
  `Runner`, `LoadTest`, `MockServers`, `Monitors`) says what the main area shows; the rail commands (`ShowRunnerCommand`...), the folder
  context menu, the palette (`palette.runCollection`, `palette.loadTest`, `palette.mockServers`, `palette.monitors`) and the status bar
  items (Runner progress, "Mocks: {n} running", "Monitors: {n} failing") set it, and `TabsViewModel.TabShown` sets it back to `Requests`
  (ADR 0023)."; "closing the main window stops the mock servers" becomes "closing the main window asks first while a run, a load test or
  a mock server is running, then stops the mock servers". Keep the rest of the paragraph.
- `docs/tasks/README.md`, Milestone 6 summary: in "Delivered", "the tool windows, folder menu, palette and status bar entries on both
  front-ends" becomes "the four views in the main window with rail entries, folder menu, palette and status bar entries on both front-ends
  (ADR 0023, refactor M6-75 to M6-83)"; add ADR [0023](../adr/0023-automation-views-in-main-window.md) to the documentation list.
- End-to-end tests (`Milestone6*EndToEndTests`) do not use windows or shell events: check with `rtk grep` and record "no change" in Notes.

## Out of scope

Screenshots (M6-83); ADR text (M6-75).

## Acceptance criteria

- [ ] `rtk grep -n -i "window" docs/automation.md` shows no tool described as a window (the main window and OS wording may stay).
- [ ] `docs/architecture.md` has no `ToolWindowHost`; the README summary is updated; Markdown links resolve.

## Suggested commit message

`docs: describe the automation views in the main window`
