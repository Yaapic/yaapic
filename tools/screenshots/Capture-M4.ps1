<#
.SYNOPSIS
Captures the 20 Milestone 4 WinUI screenshots (ten screens, dark and light) into docs/screenshots/m4.
.DESCRIPTION
Publish the app with Native AOT first (see README.md), then run: pwsh tools/screenshots/Capture-M4.ps1 [-Only <screen>].
Every capture is its own pwsh process (the stub server keeps its port otherwise) and runs in English. All secrets, keys and
certificates are throw-away values generated at capture time (an RSA key for the JWT request, two self-signed certificates, an
HS256 token signed with a fake secret) and deleted with the temp folder.
#>
param([string]$Only = '', [string]$Out = (Join-Path $PSScriptRoot '..\..\docs\screenshots\m4'))
$ErrorActionPreference = 'Stop'
$capture = Join-Path $PSScriptRoot 'Capture-WinUI.ps1'
# An expired HS256 token signed with a fake secret, for the JWT decoder.
$b64 = { param($t) [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($t)).TrimEnd('=').Replace('+', '-').Replace('/', '_') }
$head = & $b64 '{"alg":"HS256","typ":"JWT"}'
$pay = & $b64 '{"sub":"demo-user","iss":"yaapic-screenshots","aud":"demo-api","iat":1700000000,"exp":1700003600}'
$sig = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes('fake-secret')).ComputeHash([Text.Encoding]::UTF8.GetBytes("$head.$pay"))
$token = "$head.$pay." + [Convert]::ToBase64String($sig).TrimEnd('=').Replace('+', '-').Replace('/', '_')
# The request pane is only ~300 px high when the panels are stacked: the auth screens use the side-by-side layout (full height).
# Shrinks the request pane with the splitter (keyboard, 16 px per step) so that a tall response fits.
$up = 'focus:Resize request and response panels|keys:{UP 40}|wait:300|click:1000,510|wait:300|hover:1000,760|wheel:1000,760,1|wait:300'
$palette = { param($text) "keys:^k|wait:500|type:$text|wait:500|keys:{ENTER}|wait:1000" }
$screens = [ordered]@{
    'auth-oauth2'          = @('-Request', 'oauth2', '-Steps', "invoke:Send|wait:1500|invoke:AuthTab|invoke:Layout|wait:800|hover:700,500|wheel:700,500,10|wait:400")
    'auth-oauth2-code'     = @('-Request', 'oauth2-code', '-Steps', "invoke:AuthTab|invoke:Layout|wait:800")
    'auth-jwt'             = @('-Request', 'jwt', '-Steps', "invoke:AuthTab|invoke:Layout|wait:800")
    'auth-aws'             = @('-Request', 'aws', '-Steps', "invoke:AuthTab|invoke:Layout|wait:800")
    'response-timing'      = @('-Request', 'list-users', '-Steps', "invoke:Send|wait:1500|invoke:TimingTab|$up")
    'response-diff'        = @('-Request', 'changing', '-Steps', "invoke:Send|wait:1500|invoke:Send|wait:1500|invoke:DiffTab|invoke:Layout|click:46,112|wait:800")
    'code-generation'      = @('-Request', 'create-user', '-Steps', ((& $palette 'Generate code') + '|focus:Language|keys:{DOWN}{DOWN}|wait:800'))
    'certificates'         = @('-Certificates', '-Steps', ((& $palette 'Client certificates') + '|click:210,200|wait:800'))
    'jwt-decoder'          = @('-Steps', ((& $palette 'Decode JWT') + "|focus:Paste a JWT|type:$token|wait:800"))
    'settings-http-version' = @('-Request', 'changing', '-Steps', 'invoke:SettingsTab|invoke:Layout|wait:800')
}
foreach ($name in $screens.Keys) {
    if ($Only -and $Only -ne $name) { continue }
    foreach ($theme in 'dark', 'light') {
        & pwsh -NoProfile -File $capture -Theme $theme -Language english @($screens[$name]) -Out (Join-Path $Out "$name-$theme.png")
        if ($LASTEXITCODE -ne 0) { throw "Capture $name-$theme failed." }
    }
}
