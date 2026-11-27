<#
.SYNOPSIS
Captures the Yaapic WinUI window to a PNG, on a throw-away copy of the fixture workspace.
.DESCRIPTION
Publish the app with Native AOT first (see README.md). The script copies tools/screenshots/workspace to a temp folder,
uses a throw-away data dir (`--data-dir`, so the vault never lands in %APPDATA%), starts a loopback stub server on 127.0.0.1:5199, launches the exe with --workspace/--theme, resizes the window,
optionally sends keys and Ctrl+Enter, captures it with PrintWindow, then cleans up (also on failure).
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\..\src\Yaapic.App.WinUI\bin\Release\net11.0-windows10.0.26100\win-x64\publish\Yaapic.App.WinUI.exe'),
    [ValidateSet('dark', 'light')][string]$Theme = 'dark',
    [Parameter(Mandatory)][string]$Out,
    [int]$Width = 1440,
    [int]$Height = 900,
    [switch]$Send,
    [switch]$SelectAfterSend,
    [int]$WaitMs = 1500,
    [string]$Keys = '',
    [string]$Select = '',
    [string]$FocusElement = '',
    [string]$Request = 'create-user',
    [string[]]$Steps = @(),
    [switch]$Screen,
    [switch]$Certificates,
    [switch]$NoTabs,
    [switch]$NoStub,
    [string]$Crop = '',
    [string]$Window = '',
    [string[]]$Seed = @(),
    [ValidateSet('', 'available', 'downloading', 'ready', 'uptodate', 'failed')][string]$UpdateDemo = '',
    [string]$Accent = '',
    [ValidateSet('', 'english', 'french')][string]$Language = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing, UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr h, bool alt);
    [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint b, IntPtr c, uint d);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    public static bool Focus(IntPtr h) {
        uint pid; uint fg = GetWindowThreadProcessId(GetForegroundWindow(), out pid); uint me = GetCurrentThreadId();
        SystemParametersInfo(0x2001, 0, IntPtr.Zero, 0); // SPI_SETFOREGROUNDLOCKTIMEOUT = 0, session only
        AttachThreadInput(me, fg, true); keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero);
        SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 0x0003); SetWindowPos(h, new IntPtr(-2), 0, 0, 0, 0, 0x0003); // topmost toggle, NOMOVE|NOSIZE
        SwitchToThisWindow(h, true); SetForegroundWindow(h); AttachThreadInput(me, fg, false);
        return GetForegroundWindow() == h;
    }
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
'@

[void][Win32]::SetProcessDPIAware() # real pixels: the PNG size is then exactly Width x Height
$Exe = (Resolve-Path $Exe).Path
$Out = [IO.Path]::GetFullPath($Out, (Get-Location).Path)
$temp = Join-Path ([IO.Path]::GetTempPath()) ("yaapic-shot-" + [Guid]::NewGuid().ToString('N'))
$proc = $null; $stub = $null; $ps = $null
try {
    Copy-Item (Join-Path $PSScriptRoot 'workspace') $temp -Recurse
    $ids = @{ 'list-users' = '11111111-0000-0000-0000-000000000001'; 'create-user' = '11111111-0000-0000-0000-000000000002'
              'missing-item' = '11111111-0000-0000-0000-000000000003'; 'server-error' = '11111111-0000-0000-0000-000000000004'
              'slow' = '11111111-0000-0000-0000-000000000005'
              'upload-avatar' = '11111111-0000-0000-0000-000000000006'; 'secure-call' = '11111111-0000-0000-0000-000000000007'
              'big' = '11111111-0000-0000-0000-000000000008'; 'scripted' = '11111111-0000-0000-0000-000000000009'
              'noisy' = '11111111-0000-0000-0000-00000000000a'; 'lua-scripted' = '11111111-0000-0000-0000-00000000000b'; 'broken' = '11111111-0000-0000-0000-00000000000c'
              'oauth2' = '11111111-0000-0000-0000-00000000000d'; 'oauth2-code' = '11111111-0000-0000-0000-00000000000e'; 'jwt' = '11111111-0000-0000-0000-00000000000f'
              'aws' = '11111111-0000-0000-0000-000000000010'; 'changing' = '11111111-0000-0000-0000-000000000011' }
    # Throw-away RSA key for the jwt request, generated now and deleted with the temp folder (never a real key).
    $jwtFile = Join-Path $temp 'requests\jwt.request.yaml'
    $pem = ([Security.Cryptography.RSA]::Create(2048)).ExportPkcs8PrivateKeyPem() -split "`r?`n" | Where-Object { $_ }
    [IO.File]::WriteAllText($jwtFile, ([IO.File]::ReadAllText($jwtFile) -replace '__JWT_PEM__', ($pem -join "`n      ")), [Text.UTF8Encoding]::new($false))
    if ($Seed -contains 'm6') {
        # Milestone 6 demo folder "Demo API" (runner, load test, mock server, monitor) and a CSV data file; all values are fake.
        $demo = Join-Path $temp 'requests\demo-api'; New-Item -ItemType Directory $demo | Out-Null
        [IO.File]::WriteAllText((Join-Path $demo 'folder.yaml'), "version: 1`nid: 44444444-0000-0000-0000-000000000001`nname: Demo API`ndescription: Folder used by the milestone 6 captures.`nvariables: []`n", [Text.UTF8Encoding]::new($false))
        function New-Demo($n, $name, $method, $path, $pairs, $body, $dir = $demo, $pre = '') {
            $yaml = "version: 1`nid: 44444444-0000-0000-0001-0000000000$n`nname: $name`nmethod: $method`nurl: `"{{baseUrl}}$path`"`nparams: []`nheaders: []`nbody:`n  type: $(if ($body) { 'raw' } else { 'none' })`n  contentType: json`n  text: '$body'`n"
            if ($pre -or $pairs) { $yaml += "scripts:`n" }
            if ($pre) { $yaml += "  preRequest:`n    language: javascript`n    source: |`n      $pre`n" }
            if ($pairs) { $yaml += "  postResponse:`n    language: javascript`n    source: |`n" + (($pairs | ForEach-Object { "      yaapic.test(`"$($_[0])`", () => $($_[1]));" }) -join "`n") + "`n" }
            [IO.File]::WriteAllText("$dir\$(($name.ToLower()) -replace ' ','-').request.yaml", $yaml, [Text.UTF8Encoding]::new($false)) }
        New-Demo '01' 'Optional report' 'GET' '/users' @(,@('Status is 200', 'yaapic.response.to.have.status(200)')) '' $demo 'yaapic.execution.skipRequest();'
        New-Demo '02' 'Create user' 'POST' '/users' @(,@('Created', 'yaapic.response.to.have.status(201)')) '{"name":"{{name}}"}'
        New-Demo '03' 'Find missing user' 'GET' '/status/404' @(@('Status is 200', 'yaapic.response.to.have.status(200)'), @('Responds with JSON', 'yaapic.response.to.be.json')) ''
        $health = Join-Path $temp 'requests\health-checks'; New-Item -ItemType Directory $health | Out-Null
        [IO.File]::WriteAllText((Join-Path $health 'folder.yaml'), "version: 1`nid: 44444444-0000-0000-0000-000000000002`nname: Health checks`nvariables: []`n", [Text.UTF8Encoding]::new($false))
        New-Demo '04' 'Ping' 'GET' '/users' @(,@('Status is 200', 'yaapic.response.to.have.status(200)')) '' $health
        [IO.File]::WriteAllText((Join-Path $temp 'users.csv'), "name,team`nAda,platform`nGrace,compilers`nLinus,kernel`n", [Text.UTF8Encoding]::new($false))
    }
    $session = Join-Path $temp '.yaapic\session.yaml'
    $open = if ($NoTabs) { 'openRequests: []' } else { "openRequests:`n  - $($ids[$Request])" }
    [IO.File]::WriteAllText($session, "version: 1`n$open`nactiveRequest: $($ids[$Request])`nactiveEnvironment: 22222222-0000-0000-0000-000000000001`n", [Text.UTF8Encoding]::new($false))

    # A few history entries with timestamps relative to now (history.jsonl is per-user state, so it is generated here).
    $hist = foreach ($e in @(@('create-user', 2, 201, 'Created', 41, 96), @('list-users', 35, 200, 'OK', 18, 118), @('missing-item', 190, 404, 'Not Found', 12, 25),
                             @('server-error', 1500, 503, 'Service Unavailable', 33, 31), @('list-users', 3000, 200, 'OK', 22, 118))) {
        $yaml = [IO.File]::ReadAllText((Join-Path $temp "requests\$($e[0]).request.yaml"))
        [pscustomobject]@{ id = [Guid]::NewGuid().ToString('D'); at = [DateTime]::UtcNow.AddMinutes(-$e[1]).ToString('O'); request = $yaml
            statusCode = $e[2]; statusText = $e[3]; durationMs = $e[4]; sizeBytes = $e[5]; failureKind = $null; failureMessage = $null } | ConvertTo-Json -Compress
    }
    [IO.File]::WriteAllText((Join-Path $temp '.yaapic\history.jsonl'), (($hist -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))

    if (-not $NoStub) {
        $ps = [PowerShell]::Create()
        [void]$ps.AddScript({
            $l = [Net.HttpListener]::new(); $l.Prefixes.Add('http://127.0.0.1:5199/'); $l.Start()
            while ($l.IsListening) {
                $c = $l.GetContext(); $p = $c.Request.Url.AbsolutePath; $code = 200
                $body = '{"error":"not found"}'
                if ($p -eq '/users' -and $c.Request.HttpMethod -eq 'POST') { $code = 201; $body = '{"id":42,"name":"Ada Lovelace","email":"ada@example.test"}' }
                elseif ($p -eq '/users') { $body = '[{"id":1,"name":"Ada"},{"id":2,"name":"Grace"}]' }
                elseif ($p -match '^/status/(\d+)$') { $code = [int]$Matches[1]; $body = '{"title":"Problem","status":' + $code + '}' }
                elseif ($p -eq '/big') { $body = '[' + ((1..65000 | ForEach-Object { '{"id":' + $_ + ',"name":"item number ' + $_ + '","active":true,"score":1.5,"tags":["a","b"]}' }) -join ",`n") + ']' }
                elseif ($p -eq '/token') { $body = '{"access_token":"fake-access-token-0000","token_type":"Bearer","expires_in":3600,"scope":"read write"}' }
                elseif ($p -eq '/changing') {
                    $script:n++
                    $o = [ordered]@{ id = 7; name = 'Ada Lovelace'; role = $(if ($script:n -gt 1) { 'admin' } else { 'user' }); version = "1.$($script:n)" }
                    foreach ($i in 1..24) { $o["field$i"] = "stable value $i" }
                    $o.logins = 10 + $script:n
                    if ($script:n -gt 1) { $o.lastSeen = '2026-11-17T09:00:00Z' }
                    $body = $o | ConvertTo-Json -Depth 4
                    $c.Response.Headers.Add('X-Request-Count', "$($script:n)")
                    $c.Response.Headers.Add('ETag', '"v' + $script:n + '"')
                }
                elseif ($p -match '^/delay/(\d+)$') { Start-Sleep -Milliseconds ([int]$Matches[1]); $body = '{"delayed":true}' }
                $b = [Text.Encoding]::UTF8.GetBytes($body)
                $c.Response.StatusCode = $code; $c.Response.ContentType = 'application/json'
                $c.Response.OutputStream.Write($b, 0, $b.Length); $c.Response.Close()
            }
        })
        $stub = $ps.BeginInvoke()
        Start-Sleep -Milliseconds 300
    }

    if ($Accent -or $Language) {
        # Seeds the throw-away data dir with preferences.json (an accent preset id or #RRGGBB, and a language).
        New-Item -ItemType Directory -Force "$temp-data\Yaapic" | Out-Null
        $prefs = [ordered]@{}
        if ($Language) { $prefs.language = $Language }
        if ($Accent) { $prefs.accent = $Accent }
        $prefs | ConvertTo-Json | Set-Content "$temp-data\Yaapic\preferences.json" -Encoding utf8
    }

    if ($Certificates) {
        # Two throw-away self-signed certificates (a PFX and a PEM pair) listed in certificates.json; no passphrase, nothing real.
        $dir = "$temp-data\Yaapic"; New-Item -ItemType Directory -Force $dir | Out-Null
        # The certificate files sit in a short folder at the drive root so that the dialog's path fields show no user name; it is deleted below.
        $certDir = Join-Path $env:SystemDrive 'yaapic-shot-certs'; New-Item -ItemType Directory -Force $certDir | Out-Null
        $mk = { $rsa = [Security.Cryptography.RSA]::Create(2048)
            $req = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=yaapic-demo', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
            $req.CreateSelfSigned([DateTimeOffset]::UtcNow, [DateTimeOffset]::UtcNow.AddDays(30)) }
        $c1 = & $mk; $c2 = & $mk
        [IO.File]::WriteAllBytes("$certDir\demo.pfx", $c1.Export('Pfx'))
        [IO.File]::WriteAllText("$certDir\demo.pem", $c2.ExportCertificatePem())
        [IO.File]::WriteAllText("$certDir\demo.key", [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($c2).ExportPkcs8PrivateKeyPem())
        $j = [ordered]@{ version = 1; certificates = @(
            [ordered]@{ id = '33333333-0000-0000-0000-000000000001'; name = 'Staging mTLS'; hostPattern = '*.staging.example.test'; format = 'pfx'; certificatePath = "$certDir\demo.pfx"; keyPath = ''; hasPassphrase = $false },
            [ordered]@{ id = '33333333-0000-0000-0000-000000000002'; name = 'Partner API'; hostPattern = 'api.partner.example.test'; format = 'pem'; certificatePath = "$certDir\demo.pem"; keyPath = "$certDir\demo.key"; hasPassphrase = $false }) }
        $j | ConvertTo-Json -Depth 4 | Set-Content "$dir\certificates.json" -Encoding utf8
    }

    $appArgs = @('--workspace', "`"$temp`"", '--data-dir', "`"$temp-data`"", '--theme', $Theme)
    if ($UpdateDemo) { $appArgs += '--update-demo', $UpdateDemo }
    $proc = Start-Process $Exe -ArgumentList $appArgs -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ($proc.MainWindowHandle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200; $proc.Refresh() }
    if ($proc.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Main window did not appear.' }
    $h = $proc.MainWindowHandle
    [void][Win32]::SetWindowPos($h, [IntPtr]::Zero, 0, 0, $Width, $Height, 0x0044) # SWP_NOZORDER | SWP_SHOWWINDOW
    Start-Sleep -Milliseconds $WaitMs
    if (($Select -and -not $SelectAfterSend) -or $FocusElement) {
        # UI Automation by element name: Select invokes a button by AutomationId (e.g. BodyTab), FocusElement focuses a field by name.
        $root = [Windows.Automation.AutomationElement]::FromHandle($h)
        $find = { param($prop, $n) $root.FindFirst([Windows.Automation.TreeScope]::Descendants,
            (New-Object Windows.Automation.PropertyCondition($prop, $n))) }
        if ($Select -and -not $SelectAfterSend) {
            $el = & $find ([Windows.Automation.AutomationElement]::AutomationIdProperty) $Select; if (-not $el) { throw "Element '$Select' not found." }
            $pat = $null
            if ($el.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$pat)) { $pat.Invoke() }
            elseif ($el.TryGetCurrentPattern([Windows.Automation.TogglePattern]::Pattern, [ref]$pat)) { $pat.Toggle() }
            else { throw "Element '$Select' cannot be invoked." }
            Start-Sleep -Milliseconds 500
        }
        if ($FocusElement) {
            $el = & $find ([Windows.Automation.AutomationElement]::NameProperty) $FocusElement; if (-not $el) { throw "Element '$FocusElement' not found." }
            $el.SetFocus(); Start-Sleep -Milliseconds 300
        }
    }
    if ($Keys) {
        # Keys are never sent unless our window really has focus (they would land in another application).
        $focused = $false
        for ($i = 0; $i -lt 10 -and -not $focused; $i++) { $focused = [Win32]::Focus($h); Start-Sleep -Milliseconds 300 }
        if (-not $focused) { throw "Could not bring the Yaapic window to the foreground (foreground=$([Win32]::GetForegroundWindow()), ours=$h); keys not sent." }
        [Windows.Forms.SendKeys]::SendWait($Keys); Start-Sleep -Milliseconds 500
    }
    $focusH = $h
    foreach ($step in ($Steps -split '\|')) { # '|' also separates steps, so they survive `pwsh -File`
        # Steps: invoke:<AutomationId or name>, toggle:<name>, focus:<name>, keys:<SendKeys>, vk:ctrl+<hex virtual key>, type:<text>, wait:<ms>.
        $step = $step.Replace('{TEMP}', $temp) # {TEMP}: the throw-away workspace folder (e.g. the seeded users.csv)
        $kind, $arg = $step -split ':', 2
        # Elements are searched in every top-level window of the app (the main window first, then tool windows and dialogs).
        $roots = @(try { [Windows.Automation.AutomationElement]::FromHandle($h) } catch { }) + @([Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,
            (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id))) | Where-Object { $_.Current.NativeWindowHandle -ne [int]$h })
        $roots = @($roots | Sort-Object { if ($_.Current.NativeWindowHandle -eq [int]$focusH) { 0 } else { 1 } }) # the window brought to the front by front: is searched first
        $findAny = { param($prop, $n) foreach ($r in $roots) { $e = $r.FindFirst([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition($prop, $n))); if ($e) { return $e } } }
        $byName = { param($n) & $findAny ([Windows.Automation.AutomationElement]::NameProperty) $n }
        $byId = { param($n) & $findAny ([Windows.Automation.AutomationElement]::AutomationIdProperty) $n }
        switch ($kind) {
            'wait' { Start-Sleep -Milliseconds ([int]$arg) }
            'openpath' { # openpath:<file>: types a path into the system Open dialog (it owns the foreground and its file name box has the focus) and presses Enter.
                [Windows.Forms.SendKeys]::SendWait('^a' + [regex]::Replace($arg, '[+^%~(){}\[\]]', '{$0}')); Start-Sleep -Milliseconds 300
                [Windows.Forms.SendKeys]::SendWait('{ENTER}'); Start-Sleep -Milliseconds 1500
            }
            'clickitem' { # clickitem:<name part>:<index>: clicks the centre of the nth element whose name contains the text (rows of a list).
                $part, $idx = $arg -split ':', 2
                $all = @(); foreach ($r in $roots) { $all += @($r.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.Name -like "*$part*" }) }
                if ($all.Count -le [int]$idx) { throw "Step '$step': only $($all.Count) element(s)." }
                $br = $all[[int]$idx].Current.BoundingRectangle
                [void][Win32]::Focus($focusH); [void][Win32]::SetCursorPos([int]($br.X + $br.Width / 2) - 6, [int]($br.Y + $br.Height / 2)); Start-Sleep -Milliseconds 200
                [Win32]::mouse_event(1, 6, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 200; [Win32]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [Win32]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 700
            }
            'setvalue' { # setvalue:<edit name part>=<text>: types a number into a NumberBox (its edit is named "<header> Minimal... Maximal...") and tabs out.
                $part, $text = $arg -split '=', 2
                $all = @(); foreach ($r in $roots) { $all += @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit))) | Where-Object { $_.Current.Name -like "*$part*" }) }
                if (-not $all) { throw "Step '$step': edit not found." }
                [void][Win32]::Focus($focusH); $all[0].SetFocus(); Start-Sleep -Milliseconds 200
                [Windows.Forms.SendKeys]::SendWait('^a' + $text + '{TAB}'); Start-Sleep -Milliseconds 400
            }
            'http' { # http:<url>: a GET sent by the script itself (e.g. to the mock server), errors ignored.
                try { [void](Invoke-WebRequest $arg -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop) } catch { }
            }
            'pickcombo' { # pickcombo:<combo name>=<item text>: opens a combo box and selects the item (the popup is a window of its own).
                $cname, $iname = $arg -split '=', 2
                $combo = $null; foreach ($r in $roots) { $combo = $r.FindFirst([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.AndCondition((New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::ComboBox)), (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, $cname))))); if ($combo) { break } }
                if (-not $combo) { throw "Step '$step': combo box not found." }
                $combo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 700
                $item = $null
                foreach ($r in @([Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id))))) {
                    $item = $r.FindFirst([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.AndCondition((New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::ListItem)), (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, $iname))))); if ($item) { break } }
                if (-not $item) { throw "Step '$step': item not found." }
                $item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 600
            }
            'probe' { # probe:<url>: prints whether the URL answers (e.g. a mock server) and whether the app process is still alive.
                $ok = try { (Invoke-WebRequest $arg -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop).StatusCode } catch { if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'no answer' } }
                $proc.Refresh(); Write-Host "PROBE $arg -> $ok; app process exited: $($proc.HasExited); top-level windows: $(@([Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)))).Count)"
            }
            'front' { # front:<title part>: later keys/clicks go to that tool window (front: alone returns to the main window).
                $focusH = $h
                if ($arg) { $w = $roots | Where-Object { $_.Current.Name -like "*$arg*" } | Select-Object -First 1; if (-not $w) { throw "Step '$step': window not found." }; $focusH = [IntPtr]$w.Current.NativeWindowHandle }
            }
            'tops' { # tops: prints every top-level window of the desktop (finds system pickers).
                foreach ($e in [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children, [Windows.Automation.Condition]::TrueCondition)) { if ($e.Current.Name -or $e.Current.ProcessId -in @(Get-Process PickerHost -ErrorAction SilentlyContinue | ForEach-Object Id)) { Write-Host ("  {0} | pid {1} | {2}" -f $e.Current.Name, $e.Current.ProcessId, $e.Current.ClassName) } }
            }
            'dump' { # dump:<name filter>: prints the control type and name of every element in every app window (debug aid).
                foreach ($r in $roots) { Write-Host "== $($r.Current.Name)"; foreach ($e in $r.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)) {
                    if (-not $arg -or $e.Current.Name -like "*$arg*" -or $e.Current.AutomationId -like "*$arg*") { Write-Host ("  {0} | {1} | {2}" -f $e.Current.ControlType.ProgrammaticName, $e.Current.Name, $e.Current.AutomationId) } } }
            }
            'savepath' {
                # savepath:<file>: fills the system Save dialog (file name edit, AutomationId 1001) of the picker process, then Enter.
                # The system picker runs in a PickerHost process of its own, never in the app process.
                $hostIds = @(Get-Process PickerHost -ErrorAction SilentlyContinue | ForEach-Object Id)
                $idCond = { param($i) New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::AutomationIdProperty, $i) }
                $edit = $null
                foreach ($e in [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Descendants, (& $idCond '1001'))) { if ($e.Current.ProcessId -in $hostIds -and $e.Current.ClassName -eq 'Edit') { $edit = $e; break } }
                if (-not $edit) { throw "Step '$step': Save dialog not found." }
                # The dialog owns the foreground and opens with the file name box focused: type the path and press Enter.
                [Windows.Forms.SendKeys]::SendWait('^a' + [regex]::Replace($arg, '[+^%~(){}\[\]]', '{$0}')); Start-Sleep -Milliseconds 300
                [Windows.Forms.SendKeys]::SendWait('{ENTER}'); Start-Sleep -Milliseconds 1500
            }
            { $_ -in 'invoke', 'toggle', 'focus', 'select' } {
                $el = $null
                for ($try = 0; $try -lt 8 -and -not $el; $try++) { # a menu or window may still be opening
                    $roots = @(try { [Windows.Automation.AutomationElement]::FromHandle($h) } catch { }) + @([Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,
                        (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id))) | Where-Object { $_.Current.NativeWindowHandle -ne [int]$h })
        $roots = @($roots | Sort-Object { if ($_.Current.NativeWindowHandle -eq [int]$focusH) { 0 } else { 1 } }) # the window brought to the front by front: is searched first
                    $el = & $byId $arg; if (-not $el) { $el = & $byName $arg }; if (-not $el) { Start-Sleep -Milliseconds 400 }
                }
                if (-not $el) { throw "Step '$step': element not found." }
                $pat = $null
                if ($kind -eq 'focus') { $el.SetFocus() }
                elseif ($kind -eq 'select' -and $el.TryGetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pat)) { $pat.Select() }
                elseif ($kind -eq 'toggle' -and $el.TryGetCurrentPattern([Windows.Automation.TogglePattern]::Pattern, [ref]$pat)) { $pat.Toggle() }
                elseif ($el.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$pat)) { $pat.Invoke() }
                elseif ($el.TryGetCurrentPattern([Windows.Automation.TogglePattern]::Pattern, [ref]$pat)) { $pat.Toggle() }
                elseif ($el.TryGetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pat)) { $pat.Expand() }
                elseif ($el.TryGetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pat)) { $pat.Select() }
                else { throw "Step '$step': element supports no pattern." }
                Start-Sleep -Milliseconds 500
            }
            { $_ -in 'keys', 'type', 'click', 'rclick', 'hover', 'vk', 'wheel' } {
                $focused = $false
                if (-not $focusH) { $focusH = $h }
                for ($i = 0; $i -lt 10 -and -not $focused; $i++) { $focused = [Win32]::Focus($focusH); Start-Sleep -Milliseconds 300 }
                if (-not $focused) { throw "Step '$step': could not bring the Yaapic window to the foreground." }
                if ($kind -in 'click', 'rclick', 'hover') {
                    # Window-relative pixel position "x,y" (the window sits at 0,0).
                    if ($arg -notmatch '^\d+,\d+$') { # an element name or AutomationId: use the centre of its bounding rectangle
                        $el = & $byId $arg; if (-not $el) { $el = & $byName $arg }; if (-not $el) { throw "Step '$step': element not found." }
                        $br = $el.Current.BoundingRectangle; $arg = "$([int]($br.X + $br.Width / 2)),$([int]($br.Y + $br.Height / 2))"
                    }
                    $xy = $arg -split ','; [void][Win32]::SetCursorPos([int]$xy[0] - 12, [int]$xy[1]); Start-Sleep -Milliseconds 200
                    foreach ($dx in 4, 4, 4) { [Win32]::mouse_event(1, [uint32]$dx, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 80 } # real move events: hover popups ignore SetCursorPos alone
                    [void][Win32]::SetCursorPos([int]$xy[0], [int]$xy[1]); [Win32]::mouse_event(1, 1, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 400
                    if ($kind -eq 'click') { [Win32]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [Win32]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero) }
                    if ($kind -eq 'rclick') { [Win32]::mouse_event(8, 0, 0, 0, [UIntPtr]::Zero); [Win32]::mouse_event(16, 0, 0, 0, [UIntPtr]::Zero) }
                    Start-Sleep -Milliseconds 700; continue
                }
                if ($kind -eq 'wheel') {
                    # wheel:x,y,ticks: scrolls down by that many notches at a window pixel (negative: up), e.g. inside a dialog's ScrollViewer.
                    $p = $arg -split ','; [void][Win32]::SetCursorPos([int]$p[0], [int]$p[1]); Start-Sleep -Milliseconds 200
                    [Win32]::mouse_event(0x0800, 0, 0, [uint32][BitConverter]::ToUInt32([BitConverter]::GetBytes([int](-120 * [int]$p[2])), 0), [UIntPtr]::Zero)
                    Start-Sleep -Milliseconds 600; continue
                }
                if ($kind -eq 'vk') {
                    # vk:<mods>+<hex>: a raw virtual-key press independent of the keyboard layout, e.g. vk:ctrl+C0 for Ctrl+` on a US layout.
                    $mods, $hex = $arg -split '\+', 2; $code = [Convert]::ToByte($hex, 16)
                    if ($mods -eq 'ctrl') { [Win32]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero) }
                    [Win32]::keybd_event($code, 0, 0, [UIntPtr]::Zero); [Win32]::keybd_event($code, 0, 2, [UIntPtr]::Zero)
                    if ($mods -eq 'ctrl') { [Win32]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero) }
                    Start-Sleep -Milliseconds 500; continue
                }
                if ($kind -eq 'type') { $arg = [regex]::Replace($arg, '[+^%~(){}\[\]]', '{$0}') }
                [Windows.Forms.SendKeys]::SendWait($arg); Start-Sleep -Milliseconds 500
            }
            default { throw "Unknown step '$step'." }
        }
    }
    if ($Send) {
        # UI Automation instead of Ctrl+Enter: it needs no focus, so it also works when another window is in front.
        $root = [Windows.Automation.AutomationElement]::FromHandle($h)
        $btn = $root.FindFirst([Windows.Automation.TreeScope]::Descendants,
            (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'Send')))
        if (-not $btn) { throw 'Send button not found.' }
        $btn.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds $WaitMs
    }
    if ($Send -and $SelectAfterSend -and $Select) {
        # -Select -SelectAfterSend: invoke the element (e.g. -Select Console) once the send has finished.
        $root = [Windows.Automation.AutomationElement]::FromHandle($h)
        $el = $root.FindFirst([Windows.Automation.TreeScope]::Descendants,
            (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::AutomationIdProperty, $Select)))
        if (-not $el) { throw "Element '$Select' not found." }
        $pat = $null
        if ($el.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$pat)) { $pat.Invoke() }
        elseif ($el.TryGetCurrentPattern([Windows.Automation.TogglePattern]::Pattern, [ref]$pat)) { $pat.Toggle() }
        else { throw "Element '$Select' cannot be invoked." }
        Start-Sleep -Milliseconds 700
    }

    if ($Window) { # -Window <title part>: capture that tool window of the app instead of the main window (at its default size)
        $w = [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,
            (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id))) | Where-Object { $_.Current.Name -like "*$Window*" -and $_.Current.NativeWindowHandle -ne [int]$h } | Select-Object -First 1
        if (-not $w) { throw "Window '$Window' not found." }
        $h = [IntPtr]$w.Current.NativeWindowHandle; [void][Win32]::Focus($h); Start-Sleep -Milliseconds 400
    }
    $proc.Refresh()
    if ($proc.HasExited) { throw "Yaapic exited with code $($proc.ExitCode) before the capture." }
    $r = New-Object Win32+RECT; [void][Win32]::GetWindowRect($h, [ref]$r)
    $bmp = [Drawing.Bitmap]::new($r.R - $r.L, $r.B - $r.T)
    $g = [Drawing.Graphics]::FromImage($bmp)
    if ($Screen) {
        # Menu and combo-box popups are separate top-level windows that PrintWindow does not include: copy the screen instead.
        [void][Win32]::Focus($h); Start-Sleep -Milliseconds 300
        $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
        # The 8 px invisible resize border (right and bottom) shows whatever is behind the window: repaint it from PrintWindow.
        $pw = [Drawing.Bitmap]::new($bmp.Width, $bmp.Height); $pg = [Drawing.Graphics]::FromImage($pw); $pdc = $pg.GetHdc()
        [void][Win32]::PrintWindow($h, $pdc, 2); $pg.ReleaseHdc($pdc); $pg.Dispose()
        foreach ($b in @([Drawing.Rectangle]::new($bmp.Width - 8, 0, 8, $bmp.Height), [Drawing.Rectangle]::new(0, $bmp.Height - 8, $bmp.Width, 8))) { $g.DrawImage($pw, $b, $b, [Drawing.GraphicsUnit]::Pixel) }
        $pw.Dispose(); $g.Dispose()
    } else {
        $dc = $g.GetHdc(); [void][Win32]::PrintWindow($h, $dc, 2) # PW_RENDERFULLCONTENT
        $g.ReleaseHdc($dc); $g.Dispose()
    }
    New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
    if ($Crop) { # -Crop x,y,w,h: keep only that window rectangle (e.g. the status bar)
        $c = [int[]]($Crop -split ','); $part = $bmp.Clone([Drawing.Rectangle]::new($c[0], $c[1], $c[2], $c[3]), $bmp.PixelFormat); $bmp.Dispose(); $bmp = $part
    }
    $bmp.Save($Out, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    Write-Host "Saved $Out"
}
catch { Write-Host "ERROR: $_" -ForegroundColor Red; $failed = 1 }
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    # GetContext blocks inside the runspace: do not wait for it, the process exit ends it.
    if ($ps) { [void]$ps.BeginStop($null, $null) }
    foreach ($d in $temp, "$temp-data", (Join-Path $env:SystemDrive 'yaapic-shot-certs')) { if (Test-Path $d) { Remove-Item $d -Recurse -Force } } # the data dir holds the throw-away vault: the real %APPDATA%\Yaapic is never touched
}
# The blocked stub-server runspace would keep the host alive for minutes: leave explicitly.
[Environment]::Exit([int]$failed)
