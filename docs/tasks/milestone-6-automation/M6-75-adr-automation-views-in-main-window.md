# M6-75: ADR 0023: automation views in the main window

Status: done
Platform: any
Depends on: M6-49, M6-56

## Goal

Record the product decision of 2026-11-27 ("the Runner, Load test, Mock servers and Monitors screens get their buttons in the left
menu and open in the current window, not in a new one") as ADR 0023, so the refactor cards M6-76 to M6-83 have one reference, and
point the UI sections of ADRs 0018 to 0021 at it.

## Design decisions

- Read first: ADRs 0018 to 0021 (section "UI"), M6-41 (shell entries), M6-43 and M6-50 (the tool window hosts this ADR removes),
  `docs/adr/README.md` (format: status, options considered, Native AOT risks, decision). ADR 0022 is reserved for the command-line
  runner (M6-65): use **0023** even if 0022 does not exist yet.
- `docs/adr/0023-automation-views-in-main-window.md`, status `Accepted`, "See also" ADRs 0018 to 0021. Sections:
  - **Context**: since M6-43/M6-50 each tool opens in its own non-modal window (`ToolWindow` on WinUI, `ToolWindowHost` on Avalonia);
    the product owner asked for the tools to be reachable from the left menu and to show in the current window. The main window is
    a 48 px rail (Collections, History; Settings at the bottom), a 272 px sidebar and the main area (request tabs). Minimum window
    width 900, so the main area is 580 dip wide at the minimum with the sidebar, 852 without it (the tool windows had a minimum of 800).
  - **Options considered**:
    1. Keep the tool windows (status quo). Rejected by the product owner.
    2. Show each tool as a tab of the request `TabView`/`TabControl`. Rejected: the tabs are `RequestEditorViewModel`s saved in the
       session; mixing types breaks the session, Ctrl+W/Ctrl+S and the tab templates.
    3. Entries in the 272 px sidebar listing servers and monitors (Postman style). Rejected: duplicates the master lists the four views
       already have, and needs new sidebar view models.
    4. **Rail entries that switch the main area** (chosen): one group of four icon buttons on the rail; the selected tool replaces the
       request area; the view models already live as long as the shell (M6-41), so state is kept for free.
    5. Keep an optional "Open in a separate window". Rejected for now: it doubles the hosting code for no request.
  - **Native AOT risks**: none new; the change removes code (two window hosts, the dialog owner override). No package.
  - **Decision**, copy these rules exactly:
    - `ShellSection` (ViewModels): `Requests` (default), `Runner`, `LoadTest`, `MockServers`, `Monitors`; `ShellViewModel.Section` says what the
      main area shows. It is not saved in the session: Yaapic always starts on `Requests`.
    - Rail: after History, a 1 px separator then four entries in this order: Runner (`YiRunner`), Load test (`YiTimeline`; the icon kit has
      no load icon, a dedicated one is a design follow-up), Mock servers (`YiMock`), Monitors (`YiMonitor`). Icon only, tooltip and
      accessible name = the tool title (`runner.title`, `load.title`, `mock.title`, `monitor.title`). Hidden when the tools are not attached.
      One rail entry is selected at a time: Collections or History while `Section` is `Requests` (by sidebar mode), else the tool.
    - While a tool is shown, the request area **and the 272 px sidebar** are hidden (the tool gets the width; its own list is its
      navigation). The rail, title bar, banners, console panel and status bar stay.
    - Back to the requests: the Collections or History rail entry, or anything that shows a tab (new tab, Ctrl+T, opening a request from
      the palette or history, next/previous tab). The active tab is the one that was active before. Closing a tab never changes the section.
      Ctrl+W and Ctrl+S do nothing while a tool is shown.
    - Entry points: the folder context menu items, the palette entries and the status bar items show the tool in the main window (same
      folder pre-selection as before: the Runner and Load test load the folder, a mock server is generated from it, a monitor is created
      for it). They never open a window.
    - State: one view model per tool for the shell's lifetime (M6-41) and one view per tool created once and only hidden when another
      section is shown: a run, a load test or a mock server keeps going, and selections, splitters and scroll positions are kept.
    - Closing Yaapic while a collection run or a load test is in progress asks first (`IDialogService.ConfirmAsync`, "Close
      Yaapic?"), because their results would be lost; Cancel keeps Yaapic open. Running mock servers do not count: closing stops them
      silently, as before (ADR 0020). A scheduled monitor does not count either. Then the unsaved-tab prompts run as before.
    - Dialogs and file pickers are owned by the main window. `ToolWindow` (WinUI), `ToolWindowHost` (Avalonia), the dialog services'
      active-window override and the shell's `RunnerRequested`/`LoadTestRequested`/`MockServersRequested`/`MonitorsRequested` events are
      removed. `LineChart` stays.
  - **Out of scope**: an "open in a separate window" option, keyboard shortcuts for the four tools, a resizable or collapsible sidebar,
    saving the section in the session, a new load-test icon.
- `docs/adr/README.md`: add the row `| [0023](0023-automation-views-in-main-window.md) | Automation views in the main window | Accepted |`
  (after the 0022 row when it exists, else after 0021).
- ADR 0018, section "UI": the first sentence becomes "A Runner view shown in the main window (ADR 0023), opened from the rail, the folder
  context menu ("Run folder..."), the palette ("Run collection...") and the status bar "Runner" item (which shows the progress `3/20`
  while a run is going)."; the last line "closing the window does not stop the run" becomes "showing another view does not stop the run".
- ADR 0019, "UI": "A Load test window (non-modal, one at a time) opened from the folder context menu ("Load test...") and the palette:"
  becomes "A Load test view shown in the main window (ADR 0023), opened from the rail, the folder context menu ("Load test...") and the
  palette:". "Limits and safety": "before the first start of each window session" becomes "before the first start after Yaapic starts".
- ADR 0020, "UI": "A Mock servers window (non-modal, one at a time) opened from the palette" becomes "A Mock servers view shown in the main
  window (ADR 0023), opened from the rail, the palette".
- ADR 0021: "Notifications": "which opens the Monitors window" becomes "which shows the Monitors view"; "UI": "A Monitors window
  (non-modal, one at a time) opened from the palette" becomes "A Monitors view shown in the main window (ADR 0023), opened from the rail,
  the palette". Change nothing else in ADRs 0018 to 0021.

## Out of scope

Code (M6-76 to M6-81), user docs (M6-82), screenshots (M6-83).

## Acceptance criteria

- [x] `docs/adr/0023-automation-views-in-main-window.md` exists with the sections and rules above; the ADR index lists it.
- [x] ADRs 0018 to 0021 carry the replacements above and nothing else (`git diff` shows only those lines).
- [x] Markdown links resolve (open each relative link target).

## Suggested commit message

`docs(adr): add ADR 0023 for the automation views in the main window`
