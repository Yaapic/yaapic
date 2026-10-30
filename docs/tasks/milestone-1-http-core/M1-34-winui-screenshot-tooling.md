# M1-34: WinUI screenshot tooling

Status: todo
Platform: windows
Depends on: M1-27

## Goal

Let every visual card (M1-35 onward) start the published WinUI app on a known workspace, in a given theme, and capture
the window to a PNG without touching the user's real workspace.

## Design decisions

- **Command-line options** parsed in `App.OnLaunched` (`Environment.GetCommandLineArgs()`, simple loop, no library):
  - `--workspace <dir>`: workspace root passed to `AppComposition.CreateShell` instead of `Workspace.DefaultRoot()`.
  - `--theme dark|light|system`: initial theme, applied through the same code path as the existing Theme menu
    actions in `MainWindow.xaml.cs` (expose one `internal` method on `MainWindow` if needed; no duplication).
  - Unknown options are ignored. No other behaviour change. The App project stays free of business logic.
- **Fixture workspace** `tools/screenshots/workspace/` written by hand in file format v1 (`docs/file-format.md`):
  - `requests/`: `list-users.request.yaml` (GET `{{baseUrl}}/users?page=1`, header `Accept: application/json`),
    `create-user.request.yaml` (POST `{{baseUrl}}/users`, header `Content-Type: application/json`, raw JSON body of
    6-10 lines), `missing-item.request.yaml` (GET `{{baseUrl}}/status/404`), `server-error.request.yaml`
    (GET `{{baseUrl}}/status/503`), `slow.request.yaml` (GET `{{baseUrl}}/delay/10000`).
  - `environments/local.env.yaml` (`baseUrl` = `http://127.0.0.1:5199`) and `environments/staging.env.yaml`
    (`baseUrl` = `https://staging.example.test`).
  - `.yaapic/session.yaml` opening `create-user` and `list-users`, `create-user` active, `local` active.
  - Fixed GUIDs, so screenshots are reproducible.
- **Script** `tools/screenshots/Capture-WinUI.ps1` (PowerShell 7, no module to install):
  - Parameters: `-Exe` (default: the AOT publish output path of `Yaapic.App.WinUI` for the host RID), `-Theme`
    (`dark` | `light`), `-Out` (PNG path), `-Width 1440 -Height 900`, `-Send` (switch), `-WaitMs` (default 1500),
    `-Keys <string>` (keystrokes in `System.Windows.Forms.SendKeys` syntax sent to the window after start, before
    `-Send`; later cards use it to open dialogs through their accelerators), `-Request <slug>` (default `create-user`: the copied session file is rewritten so this request is open and
    active), `-NoTabs` (switch: empty `openRequests`), `-NoStub` (switch: do not start the stub server, to capture a
    connection failure).
  - Copies the fixture workspace to a fresh temp folder, starts a loopback stub server on `127.0.0.1:5199`
    (`System.Net.HttpListener` in a background runspace) answering `/users` with a small JSON array, `POST /users` with
    `201` and a JSON object, `/status/{code}` with that status and a small JSON problem body, `/delay/{ms}` after the
    delay. Then starts the exe with `--workspace <temp> --theme <theme>`, waits for the main window handle, resizes it
    to `Width` x `Height` (`SetWindowPos`), optionally sends Ctrl+Enter to the window when `-Send` is set and waits for
    the response, captures the window with `PrintWindow` (flag `PW_RENDERFULLCONTENT`) into a PNG, then stops the exe
    and the stub server and deletes the temp folder, even on failure (`try`/`finally`).
  - Win32 calls through a small inline `Add-Type` C# snippet (script only, never shipped).
- `tools/screenshots/README.md` (English): what the script does, the publish command to run first, examples
  (`-Theme dark -Out docs/screenshots/m1/shell-dark.png`), and the rule that screenshots are taken from the AOT build.
- `.gitignore`: nothing new; output PNGs are committed only by the cards that ask for them.

## Out of scope

Any visual change to the app, UI automation beyond Ctrl+Enter, a CI job, committing screenshots (M1-45 does).

## Acceptance criteria

- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green.
- [ ] AOT publish of `Yaapic.App.WinUI` 0 IL2xxx/IL3xxx warnings; the published exe started without arguments still
      uses `Workspace.DefaultRoot()`.
- [ ] `Capture-WinUI.ps1 -Theme dark` and `-Theme light` each produce a non-empty 1440x900 PNG showing the fixture
      requests in the sidebar and the `create-user` tab; with `-Send` the response area shows the stub `201`. The PNGs
      are inspected (Read tool) and not committed.
- [ ] No Yaapic or stub-server process is left running after the script (also after a failed run).

## Suggested commit message

`chore(winui): add screenshot tooling and fixture workspace`
