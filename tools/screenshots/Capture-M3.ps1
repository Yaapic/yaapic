<#
.SYNOPSIS
Captures the 18 Milestone 3 WinUI screenshots (nine screens, dark and light) into docs/screenshots/m3.
.DESCRIPTION
Publish the app with Native AOT first (see README.md), then run: pwsh tools/screenshots/Capture-M3.ps1 [-Only <screen>].
Every capture is its own pwsh process (the stub server keeps its port otherwise). The app runs with a throw-away data
directory, so the vault used by secret-variables is created through the setup dialog with an obviously fake passphrase
and never lands in %APPDATA%\Yaapic; all token values below are fake.
#>
param([string]$Only = '', [string]$Out = (Join-Path $PSScriptRoot '..\..\docs\screenshots\m3'))
$ErrorActionPreference = 'Stop'
$capture = Join-Path $PSScriptRoot 'Capture-WinUI.ps1'
# A masked secret is read-only and a first character masks the cell (follow-up F1), so each value is typed as one character, Reveal, then the rest.
# Creates the passphrase vault through the setup dialog (click on the Passphrase radio, then the two fields, then Enter).
$vault = 'invoke:Vault|wait:500|click:435,524|wait:500|focus:Passphrase|type:fake-passphrase-123|focus:Confirm passphrase|type:fake-passphrase-123'
$screens = [ordered]@{
    'scripts-pre-request' = @('-Request', 'scripted', '-Steps', 'invoke:ScriptsTab|wait:600')
    'scripts-post-lua'    = @('-Request', 'lua-scripted', '-Steps', 'invoke:ScriptsTab|invoke:Post-response|wait:600')
    'folder-scripts'      = @('-Steps', 'select:Users API|keys:+{F10}|keys:{DOWN}{DOWN}{DOWN}{ENTER}|wait:1000|wheel:720,500,10')
    'console'             = @('-Request', 'scripted', '-Send', '-Select', 'Console', '-SelectAfterSend')
    'tests-tab'           = @('-Request', 'scripted', '-WaitMs', '4000', '-Send', '-Select', 'TestsTab', '-SelectAfterSend')
    'script-error'        = @('-Request', 'broken', '-Send')
    'secret-variables'    = @('-Steps', "$vault|keys:{ENTER}|wait:2500|keys:^+e|wait:1000|click:127,219|wait:800|click:900,303|type:s|wait:600|click:1219,303|wait:300|click:900,303|type:k_test_FAKE_0000|wait:500|click:900,350|type:r|wait:600|click:1219,350|wait:300|click:900,350|type:t_test_FAKE_1111|wait:500|click:1219,350|wait:500")
    'vault-setup'         = @('-Steps', "$vault|wait:300")
    'status-bar'          = @('-Request', 'scripted', '-Send', '-Select', 'Console', '-SelectAfterSend', '-Crop', '0,832,1440,68')
}
foreach ($name in $screens.Keys) {
    if ($Only -and $Only -ne $name) { continue }
    foreach ($theme in 'dark', 'light') {
        & pwsh -NoProfile -File $capture -Theme $theme @($screens[$name]) -Out (Join-Path $Out "$name-$theme.png")
        if ($LASTEXITCODE -ne 0) { throw "Capture $name-$theme failed." }
    }
}
