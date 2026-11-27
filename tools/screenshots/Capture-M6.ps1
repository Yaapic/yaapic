<#
.SYNOPSIS
Captures the 18 Milestone 6 WinUI screenshots (nine screens, dark and light) into docs/screenshots/m6.
.DESCRIPTION
Publish the app with Native AOT first (see README.md), then run: pwsh tools/screenshots/Capture-M6.ps1 [-Only <screen>].
Every capture is its own pwsh process and runs in English on the "m6" seed of Capture-WinUI.ps1 (a "Demo API" folder with
a passing, a failing and a skipped request, a "Health checks" folder, and a users.csv data file). The responses come from the
loopback stub on 127.0.0.1:5199 and from a Yaapic mock server on 127.0.0.1:3000; nothing leaves the machine and nothing in
the data is a real secret. A capture is retried up to three times: the folder context menu sometimes does not open when
another window takes the foreground.
#>
param([string]$Only = '', [string]$Out = (Join-Path $PSScriptRoot '..\..\docs\screenshots\m6'))
$ErrorActionPreference = 'Stop'
$capture = Join-Path $PSScriptRoot 'Capture-WinUI.ps1'
$menu = 'rclick:Demo API|wait:1500|'
$runner = $menu + 'invoke:Run folder...|wait:2500|'
$load = $menu + 'invoke:Load test...|wait:2500|'
$mocks = $menu + 'invoke:Create a mock server...|wait:3000|'
$monitors = $menu + 'invoke:Monitor folder...|wait:3000|pickcombo:Environment=local|invoke:Save|wait:800|'
$tree = 'invoke:Collections|wait:800|' # back to the folder tree: the Collections rail entry (never the status bar item named "Runner")
# Every capture is the main window (1440 x 900). The script ends by killing the app, which skips the "Close Yaapic?" prompt of a running load test.
$screens = [ordered]@{
    'runner-setup'      = @('-Steps', ($runner + 'click:Choose a CSV or JSON file...|wait:3000|openpath:{TEMP}\users.csv|wait:1200'))
    'runner-results'    = @('-Steps', ($runner + 'invoke:Run|wait:3500|clickitem:RunnerResultRowViewModel:1|wait:800'))
    'load-test-running' = @('-Steps', ($load + 'setvalue:Virtual users=8|setvalue:Duration=40|invoke:Profile|wait:800|select:Ramp up|wait:600|setvalue:Ramp-up=30|invoke:Start|wait:1000|invoke:Continue|wait:9000|focus:Stop'))
    'load-test-done'    = @('-Steps', ($load + 'setvalue:Virtual users=4|setvalue:Duration=10|invoke:Start|wait:1000|invoke:Continue|wait:14000|focus:Start'))
    'mock-servers'      = @('-Steps', ($mocks + 'invoke:Start|wait:1500|http:http://127.0.0.1:3000/users|http:http://127.0.0.1:3000/status/404|http:http://127.0.0.1:3000/missing|wait:1500|clickitem:MockLogRowViewModel:0|wait:800'))
    'monitors'          = @('-Steps', ($monitors + 'invoke:Run now|wait:4000|' + $tree + 'rclick:Health checks|wait:1500|invoke:Monitor folder...|wait:3000|pickcombo:Environment=local|invoke:Save|wait:800|invoke:Run now|wait:4000|clickitem:MonitorItemViewModel:0|wait:800'))
    'folder-menu'       = @('-Screen', '-Steps', 'rclick:Demo API|wait:1000')
    'status-bar'        = @('-Crop', '0,850,1440,50', '-Steps', ($mocks + 'invoke:Start|wait:1500|' + $tree + $monitors + 'invoke:Run now|wait:4000|' + $tree + $runner + 'setvalue:Iterations=40|setvalue:Delay=400|invoke:Run|wait:3000'))
    'rail'              = @('-Crop', '0,0,380,900', '-Steps', 'invoke:RailRunner|wait:1500')
}
foreach ($name in $screens.Keys) {
    if ($Only -and $Only -ne $name) { continue }
    foreach ($theme in 'dark', 'light') {
        $done = $false
        for ($try = 1; $try -le 3 -and -not $done; $try++) {
            & pwsh -NoProfile -File $capture -Theme $theme -Language english -Seed m6 @($screens[$name]) -Out (Join-Path $Out "$name-$theme.png")
            $done = $LASTEXITCODE -eq 0
        }
        if (-not $done) { throw "Capture $name-$theme failed." }
    }
}
