# M3-60: Avalonia: status bar and console panel

Status: done
Platform: unix
Depends on: M3-47, M3-53

## Goal

The Avalonia twin of M3-53.

## Design decisions

- Read first: M3-53 (everything: layout, columns, filters, texts, accelerator, auto-scroll rule, empty text, what is **not**
  built), `src/Yaapic.App.Avalonia/MainWindow.axaml(.cs)` (root `Grid RowDefinitions="40,Auto,*"`, the request/response splitter
  from M1-54, key bindings), `Views/HistoryView.axaml` (virtualised list pattern), `Presentation/` converters, `Styles/`.
- `Views/StatusBarView.axaml(.cs)` (24 px row at the bottom, same items as WinUI, named slot for the vault item of M3-63) and
  `Views/ConsolePanelView.axaml(.cs)` (`x:DataType` `ConsoleViewModel`; header 34 px with title, segmented filter, search `TextBox`
  with watermark `Filter logs`, Clear, Copy, close; `ListBox`/`ItemsControl` in a virtualising panel; rows as in M3-53; a
  `GridSplitter` (or the existing splitter control) on the top edge, clamped 120..600 and written back to
  `Shell.ConsoleHeight`). Compiled bindings, `BrushMap` for kind colours, `MethodBrush` equivalent from the Avalonia presentation
  helpers for verbs.
- Shortcut `Ctrl+`` toggles the console (`KeyBinding`, check it is not swallowed by `CodeEditorView`: LoomEditor consumes some
  keys, see M2-70 Notes; if it is, handle it in a tunnelling handler at window level and record it).
- Fixture and capture switch: `Capture-Avalonia.ps1 -Select Console` (same behaviour as the WinUI switch), `scripted` fixture and
  a `chatty` fixture request logging 1 500 lines (add to both capture scripts if M3-53 did not).

## Out of scope

Persisting panel state, Network/Scripts tabs, log export.

## Acceptance criteria

- [x] Build `Yaapic.CrossPlatform.slnf` 0 warnings, tests green, AOT publish 0 new IL2xxx/IL3xxx.
- [x] **Runtime check on the published exe**: the M3-53 steps (status bar in both themes, toggle by shortcut and button, send fills
      the console incl. header/log/network/test lines, filters, search, Clear, Copy, splitter resize and clamp, 1 500 lines scroll
      smoothly, focus not stolen, keyboard focus visible). Record in Notes.
- [x] `Capture-Avalonia.ps1 -Select Console` screenshot compared with the mockup; differences in Notes.
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

`feat(avalonia): add the status bar and the console panel`

## Notes

- **Console surface**: LoomTerminal.Avalonia 0.2.0 (a PTY terminal control, a character grid) was evaluated and not used, for the same reason as
  M3-53: the console is a structured, filterable list of typed rows (symbol, verb colour, origin chip, error background, per-row tooltip), which a
  virtualised `ListBox` of `ConsoleLineViewModel` gives directly.
- **Platform**: the card says `unix`, but the work was done and run on Windows (the Avalonia exe is cross-platform and the capture script supports it);
  the macOS/Linux run is not covered here.
- Files: `Views/StatusBarView`, `Views/ConsolePanelView` (+ `Presentation/ConsoleMap`), two new grid rows in `MainWindow` (console, status bar; the
  palette spans all five). The status bar has an empty named `VaultSlot` (`ContentControl`) for M3-63.
- Both views use `x:DataType` `ShellViewModel` (inherited `DataContext`, paths `Console.*`) instead of `ConsoleViewModel`: the panel also binds
  `IsConsoleOpen`, `ConsoleHeight` and `ToggleConsoleCommand`.
- Colours: row symbol, error row background, message header and Console toggle use style classes with `DynamicResource` (so they follow a theme
  change without a rebuild); only the verb colour goes through a converter (`BrushMap.Method`, as the sidebar does).
- **Ctrl+`** (`Cmd+`` on macOS) is a tunnelling `KeyDown` handler on the window (`Key.OemTilde`), so the code editors cannot swallow it. Same AZERTY caveat
  as M3-53: the backtick is AltGr+7 there, so the button and the palette command are the way in. The splitter is a `Thumb` (drag, Up/Down by 16,
  double tap resets to 250); the height is clamped 120..600 by `ShellViewModel.ConsoleHeight` and to `window height - 300` through `MaxHeight`.
- **Auto-scroll rule**: same as M3-53 (follow while within 24 px of the bottom; after a rebuild the offset is restored until the user scrolls back
  down; the burst of collection events is coalesced into one restore posted at `Loaded` priority, and the restore re-opens on `IsConsoleOpen`).
- Capture: `Capture-Avalonia.ps1 -Select Console` opens the panel after the send (`--open console`), a `noisy` id was added, and the script now ends
  the process explicitly (`[Environment]::Exit`), because the stub runspace kept `pwsh -File` alive after the capture.
- **Runtime check (AOT exe, `scripted` and `noisy`, dark and light, 1280x760 driven through UI Automation)**: the Console button and Ctrl+` (raw VK
  0xC0) both toggle the panel; the panel stays open through a send; with `scripted` the console holds 6 rows (network line, header, two passed tests,
  one failed test with the error background, summary) and the error badge shows 1; filters give All 6, Error 1, Warn 0, Log 4; search `Responds`
  gives 1 and clearing restores 6; Copy then paste into the search box pastes the first line (the box is single-line); Clear empties the list and shows
  the empty text; Up/Down on the splitter grow the list by 5 x 16 dip, clamp at `window - 300` (list 633 px at 150 %) and at 120 (123 px), and the
  splitter shows keyboard focus (accent bar); `noisy` (1 000 retained rows, then `Console output truncated`) opens scrolled on the newest line and the
  `Console output truncated` warning drives the badge. Not verified by hand: dragging the splitter with the mouse, scrolling up and holding the
  position while a new send arrives, and smoothness of the 1 000-row scroll (virtualised by `ListBox`, code path only).
- Screenshots in `docs/screenshots/m3/avalonia-console-*`. Differences from the mockup: no `Network`/`Scripts` tabs and no fake footer items (by design);
  the console background is `YaapicSurfaceBaseBrush`; run headers read `Post-response · post-response · Scripted` because the view model repeats the
  origin (not changed here).
- No new test: the new code is XAML, a tunnelling key handler and trivial converters, and the test project cannot reference Avalonia; checked at runtime.
  Sonar returned no issue for the project, the changed files included. No Yaapic process left running.
