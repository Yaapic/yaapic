# M3-60: Avalonia: status bar and console panel

Status: todo
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

- [ ] Build `Yaapic.CrossPlatform.slnf` 0 warnings, tests green, AOT publish 0 new IL2xxx/IL3xxx.
- [ ] **Runtime check on the published exe**: the M3-53 steps (status bar in both themes, toggle by shortcut and button, send fills
      the console incl. header/log/network/test lines, filters, search, Clear, Copy, splitter resize and clamp, 1 500 lines scroll
      smoothly, focus not stolen, keyboard focus visible). Record in Notes.
- [ ] `Capture-Avalonia.ps1 -Select Console` screenshot compared with the mockup; differences in Notes.
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

`feat(avalonia): add the status bar and the console panel`
