# M1-47: Avalonia screenshot tooling

Status: done
Platform: unix
Depends on: M1-31, M2-22

## Goal

Let every Avalonia visual card (M1-48 onward) start the published Avalonia app on a known workspace, in a given theme,
in a given state, and capture the window to a PNG without touching the user's real workspace. Same purpose as M1-34
(read it first), but it must work on Windows, macOS and Linux.

## Context

The WinUI script `tools/screenshots/Capture-WinUI.ps1` uses Win32 (`PrintWindow`, UI Automation, `SendKeys`), so it
cannot be reused. Avalonia can render its own window to a bitmap, which is independent of the OS and of window focus.
`App.axaml.cs` always opens `Workspace.DefaultRoot()` and has no command-line options (see the notes of M1-28).

## Design decisions

- **Command-line options**, parsed in `App.OnFrameworkInitializationCompleted` from `desktop.Args` (simple loop, no
  library, unknown options ignored). All of them are inert unless given, so a normal start is unchanged:
  - `--workspace <dir>`: workspace root for `AppComposition.CreateShell` instead of `Workspace.DefaultRoot()`.
  - `--theme dark|light|system`: initial theme, through the same code path as the existing `OnThemeDark` /
    `OnThemeLight` / `OnThemeSystem` handlers (extract one `internal` method on `MainWindow`; no duplication).
  - `--size <w>x<h>`: window client size (default keeps the XAML size).
  - `--send`: once the shell is initialised, run the active tab's `SendCommand` and wait for the response state to
    leave `Sending`.
  - `--select <name>`: select a section tab of the active request by header text (`Params`, `Headers`, `Body`, later
    `Auth`, `Settings`), by walking the `TabControl` in code-behind.
  - `--open <name>`: `environments` opens the environments window; `unsaved` types one character in the request name
    and opens the unsaved-changes prompt through the close-tab path. This card implements these two; later cards add
    names for the windows they restyle (for example `prompt`).
  - `--screenshot <png>`: after a settle delay (`--wait-ms`, default 1500) render the focused top-level window
    (`RenderTargetBitmap` of the window at 1x scale, so the PNG is exactly the requested size) to the file, then close
    the app with exit code 0. On any exception exit with code 1 and write the message to stderr.
  The App project stays free of business logic: this is view-only wiring in a new
  `src/Yaapic.App.Avalonia/Services/CaptureOptions.cs` (parser, `internal`) and `Services/WindowCapture.cs`. Both carry
  XML docs. No reflection (Native AOT).
- **Fixture workspace**: reuse `tools/screenshots/workspace/` (M1-34). Do not duplicate it.
- **Script** `tools/screenshots/Capture-Avalonia.ps1` (PowerShell 7, runs on every OS):
  - Parameters: `-Exe` (default: the AOT publish output of `Yaapic.App.Avalonia` for the host RID and OS), `-Theme`,
    `-Out`, `-Width 1440 -Height 900`, `-Send`, `-WaitMs`, `-Request <slug>` (default `create-user`), `-NoTabs`,
    `-NoStub`, `-Select`, `-Open`. They map one to one to the options above.
  - Copy the fixture to a fresh temp folder and rewrite its `session.yaml` exactly like `Capture-WinUI.ps1` does
    (copy that logic, do not edit the WinUI script), start the loopback stub server on `127.0.0.1:5199` (copy its
    block: `HttpListener` in a background runspace), run the exe with the options, wait for it to exit, check that the
    PNG exists and is not empty, and clean up in `finally` (kill the exe if still alive, delete the temp folder).
  - No OS-specific call; on Linux without a display the script prints a clear error instead of hanging.
- `tools/screenshots/README.md`: add an Avalonia section (publish command per OS, examples, why captures come from the
  AOT build). `.gitignore`: nothing new.

## Out of scope

Any visual change to the app, a CI job, committing screenshots (M1-57), keyboard simulation (the options replace it).

## Acceptance criteria

- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; the option parser has a unit test (put the
      parser where the existing tests can reach it: `internal` class plus `InternalsVisibleTo`, or move it to a small
      testable type; pick the way the repository already uses).
- [x] AOT publish of `Yaapic.App.Avalonia` for the host RID has 0 IL2xxx/IL3xxx warnings; started without arguments it
      still uses `Workspace.DefaultRoot()`.
- [x] `Capture-Avalonia.ps1 -Theme dark` and `-Theme light` each produce a non-empty 1440x900 PNG showing the fixture
      requests in the sidebar and the `create-user` tab; with `-Send` the response area shows the stub `201`;
      `-Open environments` shows the environments window. The PNGs are inspected and not committed.
- [x] No Yaapic or stub-server process is left running after the script, also after a failed run.

## Suggested commit message

`chore(avalonia): add screenshot tooling and launch options`
