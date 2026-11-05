<#
.SYNOPSIS
Captures the Yaapic Avalonia window to a PNG, on a throw-away copy of the fixture workspace (Windows, macOS, Linux).
.DESCRIPTION
Publish the app with Native AOT first (see README.md). The script copies tools/screenshots/workspace to a temp folder,
starts a loopback stub server on 127.0.0.1:5199 and runs the exe with the launch options, which make the app render its
own window to the PNG and exit. Cleans up (also on failure).
#>
param(
    [string]$Exe = '',
    [ValidateSet('dark', 'light')][string]$Theme = 'dark',
    [Parameter(Mandatory)][string]$Out,
    [int]$Width = 1440,
    [int]$Height = 900,
    [switch]$Send,
    [int]$WaitMs = 1500,
    [string]$Select = '',
    [string]$Open = '',
    [string]$Request = 'create-user',
    [switch]$NoTabs,
    [switch]$NoStub
)
$ErrorActionPreference = 'Stop'
if (-not $Exe) {
    $rid = if ($IsWindows) { 'win-x64' } elseif ($IsMacOS) { if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'osx-arm64' } else { 'osx-x64' } } else { 'linux-x64' }
    $name = if ($IsWindows) { 'Yaapic.App.Avalonia.exe' } else { 'Yaapic.App.Avalonia' }
    $Exe = Join-Path $PSScriptRoot "../../src/Yaapic.App.Avalonia/bin/Release/net11.0/$rid/publish/$name"
}
if ($IsLinux -and -not $env:DISPLAY -and -not $env:WAYLAND_DISPLAY) { throw 'No display found (DISPLAY/WAYLAND_DISPLAY unset): run under a desktop session or xvfb-run.' }
$Exe = (Resolve-Path $Exe).Path
$Out = [IO.Path]::GetFullPath($Out, (Get-Location).Path)
$temp = Join-Path ([IO.Path]::GetTempPath()) ("yaapic-shot-" + [Guid]::NewGuid().ToString('N'))
$proc = $null; $stub = $null; $ps = $null; $errFile = $null
try {
    Copy-Item (Join-Path $PSScriptRoot 'workspace') $temp -Recurse
    $ids = @{ 'list-users' = '11111111-0000-0000-0000-000000000001'; 'create-user' = '11111111-0000-0000-0000-000000000002'
              'missing-item' = '11111111-0000-0000-0000-000000000003'; 'server-error' = '11111111-0000-0000-0000-000000000004'
              'slow' = '11111111-0000-0000-0000-000000000005'; 'big' = '11111111-0000-0000-0000-000000000008' }
    $session = Join-Path $temp '.yaapic\session.yaml'
    $openList = if ($NoTabs) { 'openRequests: []' } else { "openRequests:`n  - $($ids[$Request])" }
    [IO.File]::WriteAllText($session, "version: 1`n$openList`nactiveRequest: $($ids[$Request])`nactiveEnvironment: 22222222-0000-0000-0000-000000000001`n", [Text.UTF8Encoding]::new($false))

    if (-not $NoStub) {
        $ps = [PowerShell]::Create()
        [void]$ps.AddScript({
            $l = [Net.HttpListener]::new(); $l.Prefixes.Add('http://127.0.0.1:5199/'); $l.Start()
            while ($l.IsListening) {
                $c = $l.GetContext(); $p = $c.Request.Url.AbsolutePath; $code = 200
                $body = '{"error":"not found"}'
                if ($p -eq '/users' -and $c.Request.HttpMethod -eq 'POST') { $code = 201; $body = '{"id":42,"name":"Ada Lovelace","email":"ada@example.test"}' }
                elseif ($p -eq '/big') { $sb = [Text.StringBuilder]::new('['); for ($i = 0; $i -lt 40000; $i++) { if ($i) { [void]$sb.Append(',') }; [void]$sb.Append('{"id":' + $i + ',"name":"user-' + $i + '","active":true,"score":1.5,"tags":["a","b"],"note":null}') }; $body = $sb.Append(']').ToString() }
                elseif ($p -eq '/users') { $body = '[{"id":1,"name":"Ada"},{"id":2,"name":"Grace"}]' }
                elseif ($p -match '^/status/(\d+)$') { $code = [int]$Matches[1]; $body = '{"title":"Problem","status":' + $code + '}' }
                elseif ($p -match '^/delay/(\d+)$') { [Threading.Thread]::Sleep([int]$Matches[1]); $body = '{"delayed":true}' }
                $b = [Text.Encoding]::UTF8.GetBytes($body)
                $c.Response.StatusCode = $code; $c.Response.ContentType = 'application/json'
                $c.Response.OutputStream.Write($b, 0, $b.Length); $c.Response.Close()
            }
        })
        $stub = $ps.BeginInvoke()
        Start-Sleep -Milliseconds 300
    }

    New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
    if (Test-Path $Out) { Remove-Item $Out -Force }
    $argv = @('--workspace', $temp, '--theme', $Theme, '--size', "${Width}x${Height}", '--screenshot', $Out, '--wait-ms', $WaitMs)
    if ($Send) { $argv += '--send' }
    if ($Select) { $argv += '--select', $Select }
    if ($Open) { $argv += '--open', $Open }
    $errFile = Join-Path ([IO.Path]::GetTempPath()) "$([IO.Path]::GetFileName($temp)).err"
    $proc = Start-Process $Exe -ArgumentList $argv -PassThru -RedirectStandardError $errFile
    if (-not $proc.WaitForExit(60000)) { throw 'Yaapic did not exit within 60 s.' }
    if ($proc.ExitCode -ne 0) { throw "Yaapic exited with code $($proc.ExitCode): $(Get-Content $errFile -Raw)" }
    if (-not (Test-Path $Out) -or (Get-Item $Out).Length -eq 0) { throw "No screenshot was written to $Out." }
    Write-Host "Saved $Out"
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    # GetContext blocks inside the runspace: do not wait for it, the process exit ends it.
    if ($ps) { [void]$ps.BeginStop($null, $null) }
    if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
    if ($errFile -and (Test-Path $errFile)) { Remove-Item $errFile -Force }
}
