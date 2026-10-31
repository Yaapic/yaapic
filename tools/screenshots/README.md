# WinUI screenshot tooling

`Capture-WinUI.ps1` starts the published WinUI app on a throw-away copy of `workspace/` (a fixture workspace in file
format v1), against a loopback stub server on `127.0.0.1:5199`, and captures the window to a PNG with `PrintWindow`.
Screenshots are always taken from the **Native AOT build**.

```powershell
dotnet publish src/Yaapic.App.WinUI -c Release -r win-x64 -p:PublishAot=true
pwsh tools/screenshots/Capture-WinUI.ps1 -Theme dark -Out docs/screenshots/m1/shell-dark.png
pwsh tools/screenshots/Capture-WinUI.ps1 -Theme light -Send -Out docs/screenshots/m1/response-light.png
```

Parameters: `-Exe`, `-Theme dark|light`, `-Out`, `-Width 1440 -Height 900`, `-Send` (invokes the Send button through UI Automation, then waits),
`-WaitMs 1500`, `-Keys` (`SendKeys` syntax, sent before `-Send`), `-Request <slug>` (open and active request,
default `create-user`), `-Select <AutomationId>` (invoke a button before the keys, e.g. `BodyTab`), `-FocusElement <name>` (focus a field by its automation name), `-NoTabs` (no open request), `-NoStub` (no stub server, to capture a connection failure).

The app itself accepts `--workspace <dir>` and `--theme dark|light|system`. The script always cleans up the app, the
stub server and the temp folder, even on failure. The window must be on an unlocked interactive desktop.

`-Send` doubles as the AOT smoke test of the response path: the script fails if the app dies before the capture (a
successful response once crashed the AOT build, see the M1-26 notes). Use `-Send` on the published exe after any change
to the response panel.

Run one capture per `pwsh` process: in a loop inside a single process the earlier stub server keeps the port and responses never arrive.
