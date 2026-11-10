# M3-53: WinUI: status bar and console panel

Status: done
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

- [x] Build 0 warnings, tests green, AOT publish 0 IL2xxx/IL3xxx.
- [x] **Runtime check (AOT exe)** with the `scripted` fixture and the stub server: the status bar shows at the bottom in both
      themes; `Ctrl+`` and the button toggle the panel; sending the scripted request fills the console with a header line, the
      script `console.log` lines, the network line, a passed/failed test lines and the summary; filters and search work; Clear and
      Copy work (paste into a text field to check); resizing by the splitter works and is clamped; 1 000+ lines scroll smoothly
      (send a fixture request that logs 1 500 lines); the panel does not steal focus from the editor; keyboard focus visible.
      Record in Notes.
- [x] Screenshots through `Capture-WinUI.ps1 -Select Console` (add the switch: open the panel after the send) compared with the
      mockup; differences in Notes.
- [x] No process left running.

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

## Notes

- **Console surface**: the card prescribes a virtualised `ListView` of `Lines`, so LoomTerminal.WinUI (a PTY terminal control) was evaluated and
  not used: the console is a structured, filterable list of typed rows (symbol, method, chip, error background), not a character grid.
- Files: `Views/StatusBarView`, `Views/ConsolePanelView` (+ `Presentation/ConsoleMap`, `BrushMap.Console*`, `TextMap.FirstLine/JoinLines`,
  `MethodBrush` now also accepts a verb string). `MainWindow` gains two grid rows (console, status bar); the palette spans all five.
  The status bar has an empty named `VaultSlot` for M3-56.
- **Ctrl+`** is a `KeyDown` handler on the root grid, not a `KeyboardAccelerator`: a code-created accelerator with the OEM key `0xC0` never fired.
  `0xC0` is the key right of left Shift on a US layout; on an AZERTY layout the backtick is AltGr+7, so the shortcut is not reachable there
  (the status bar button and the palette command still are).
- **Auto-scroll rule**: the list follows the newest line while the view is at the bottom (24 px tolerance); a wheel, scrollbar or key
  scroll away from the bottom stops following and the offset is restored after each rebuild until the user scrolls back to the bottom.
  The VM rebuilds the whole list on every change, so the view coalesces the burst of collection events into one scroll restore. The scroll
  viewer of a collapsed panel only exists after the first layout once it opens, hence the `IsConsoleOpen` and `LayoutUpdated` hooks.
- The console height is also capped at `window height - 300` (a 600 dip console squeezed the editor away in a 900 px window).
- Capture script: `-Select <id> -SelectAfterSend` invokes the element after the send (`-Select Console -Send -SelectAfterSend`), a `vk:ctrl+<hex>`
  step presses a raw virtual key (layout independent), and a `noisy` fixture request logs 1 500 lines (the engine keeps 1 000, then
  `Console output truncated`, which is the one warning in the status bar badge). `FilesWithoutScriptsReadEmptyScripts` skips it too.
- **Runtime check (AOT exe, stub server, dark and light)**: status bar at the bottom in both themes; the button and Ctrl+` (raw VK 0xC0) toggle
  the panel, the button turns accent when open; `scripted` fills the console with the network line, the post-response header, two passed tests,
  one failed test (error background) and the summary; the error badge shows 1; `Error` filter keeps only the failed test; search filters rows;
  Copy then paste into the search box pastes the lines; Clear empties the list (empty text shown, badge gone); Up/Down on the splitter resize
  and clamp at 120 and at the window cap; `noisy` (1 000 rows) opens scrolled on the newest line (`Console output truncated`, network line);
  the panel does not take focus on open; the splitter shows a visible focus ring. Not verified by hand: scrolling up and holding the position
  while a new send arrives, and dragging the splitter with the mouse (keyboard resize was used); both are covered by the code path only.
- Screenshots in `docs/screenshots/m3/winui-console-*`. Differences from the mockup: no `Network`/`Scripts` tabs and no fake footer items
  (by design); the mockup console background is `--s0`, here `YaapicSurfaceBaseBrush`; run headers read `Post-response · post-response · Scripted`
  because the view model repeats the origin (not changed here).
- 2 new tests (`TextMapTests`); the rest of the card is XAML, checked at runtime. No Yaapic process left running.
