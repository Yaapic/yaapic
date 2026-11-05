# M3-53: WinUI: status bar and console panel

Status: todo
Platform: windows
Depends on: M3-47

## Goal

A 24 px status bar at the bottom of the window with a Console toggle (and warning/error counts), and a resizable console panel
above it, as in the mockup.

## Design decisions

- Read first: `design/mockups/MainWindow.dc.html` (footer `height:24px`, items `Console`, `Cookies`, `Runner`...; console
  `section` `height:250px`, header 34 px with tabs `Console | Réseau | Scripts`, filter chips, text filter, "Effacer", rows 22 px
  monospace 12 px with time / symbol / method / message), `DSComposants2.dc.html` (chips, list rows), `MainWindow.xaml(.cs)`
  (root grid, accelerators, the splitter used between request and response, `SplitterHandle.cs`), M3-41, M3-47 (`IsConsoleOpen`,
  `ConsoleHeight`, `ToggleConsoleCommand`, `ScriptEngineWarnings`).
- **Status bar**: `Views/StatusBarView.xaml` (`UserControl`) as a new last row of the window root grid (height 24, chrome
  background token, top border `YaapicBorderSubtleBrush`): only items that exist in M3 — at the right a `Console` toggle button
  (text `Console`, accent colour when `IsConsoleOpen`, secondary otherwise) with two small count badges (warnings in the warning
  colour, errors in the error colour, hidden at 0) bound to `Console.WarnCount`/`ErrorCount`; at the left a text item that shows
  the first `ScriptEngineWarnings` line in the warning colour (tooltip lists all) and nothing otherwise. Do **not** add the mockup's
  fake items (Cookies, Runner, Proxy, HTTP/2, zoom, version, Git). The vault item arrives in M3-56 (leave a named slot).
- **Console panel**: `Views/ConsolePanelView.xaml(.cs)` (`UserControl`, `ViewModel` `ConsoleViewModel`), placed between the main
  content and the status bar; visible when `Shell.IsConsoleOpen`; height `Shell.ConsoleHeight` (default 250), a 4 px splitter on its
  top edge resizes it (reuse `SplitterHandle`; clamp 120..600; write back to `ConsoleHeight`). Header row 34 px: title `Console`
  (the `Network` and `Scripts` tabs of the mockup are **not** built: the filter chips cover them), filter chips
  `All | Log | Warn | Error` (segmented, `Filter`), a search `TextBox` (placeholder `Filter logs`, `Search`), `Clear` button
  (`ClearCommand`), `Copy` button (`CopyCommand`), close button (`ToggleConsoleCommand`). Body: `ListView`
  (virtualised, no selection) of `Lines`; row: time (disabled colour, 86 px), symbol (coloured by `Kind`), method (coloured by
  verb with `MethodBrush`) when present, message (selectable text, single line with ellipsis, full text in a tooltip), origin chip
  on the right; error rows get the error background token; header rows are bold secondary. Empty text `"No messages yet. Script
  output and network activity appear here."`. Auto-scroll to the newest line **unless** the user scrolled up (record the rule).
- Accelerators in `MainWindow`: `Ctrl+`` (backtick) -> `ToggleConsoleCommand` (the palette shows it
  too, M3-47). Esc inside the console search clears it.
- Compiled bindings only, brushes from existing resources, new `BrushMap`/`VisibilityMap` helpers only where needed.

## Out of scope

Persisting the panel state, a Network/Scripts tab split, log export, the vault item.

## Acceptance criteria

- [ ] Build 0 warnings, tests green, AOT publish 0 IL2xxx/IL3xxx.
- [ ] **Runtime check (AOT exe)** with the `scripted` fixture and the stub server: the status bar shows at the bottom in both
      themes; `Ctrl+`` and the button toggle the panel; sending the scripted request fills the console with a header line, the
      script `console.log` lines, the network line, a passed/failed test lines and the summary; filters and search work; Clear and
      Copy work (paste into a text field to check); resizing by the splitter works and is clamped; 1 000+ lines scroll smoothly
      (send a fixture request that logs 1 500 lines); the panel does not steal focus from the editor; keyboard focus visible.
      Record in Notes.
- [ ] Screenshots through `Capture-WinUI.ps1 -Select Console` (add the switch: open the panel after the send) compared with the
      mockup; differences in Notes.
- [ ] No process left running.

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

`feat(winui): add the status bar and the console panel`
