# Release screenshots (R-06, R-07)

Taken from the Native AOT builds with the scripted update service (`--update-demo <mode>`, no network, nothing written to disk).

| File | What it shows |
|---|---|
| `winui-update-available-dark.png`, `-light.png` | Banner: "Version 1.4.0 is available." with Download |
| `winui-update-downloading-dark.png` | Banner while downloading (42 %), with Cancel |
| `winui-update-ready-dark.png` | Banner after the download, with Restart to update |
| `winui-updates-dialog-dark.png` | Settings > Updates and about..., up to date |
| `winui-updates-dialog-failed-light.png` | The same dialog after a failed manual check |

```powershell
dotnet publish src/Yaapic.App.WinUI -c Release -r win-x64 -p:PublishAot=true
pwsh tools/screenshots/Capture-WinUI.ps1 -Theme dark -UpdateDemo available -NoTabs -Out docs/screenshots/updates/winui-update-available-dark.png
pwsh tools/screenshots/Capture-WinUI.ps1 -Theme dark -UpdateDemo downloading -NoTabs -Steps 'invoke:Download|wait:800' -Out docs/screenshots/updates/winui-update-downloading-dark.png
pwsh tools/screenshots/Capture-WinUI.ps1 -Theme dark -UpdateDemo uptodate -NoTabs -Steps 'invoke:Settings|wait:500|keys:{DOWN}{DOWN}{DOWN}{ENTER}|wait:800' -Out docs/screenshots/updates/winui-updates-dialog-dark.png
```

Modes: `available`, `downloading`, `ready`, `uptodate`, `failed`. Run one capture per `pwsh` process.
