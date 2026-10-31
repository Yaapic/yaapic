<#
.SYNOPSIS
Captures the Yaapic WinUI window to a PNG, on a throw-away copy of the fixture workspace.
.DESCRIPTION
Publish the app with Native AOT first (see README.md). The script copies tools/screenshots/workspace to a temp folder,
starts a loopback stub server on 127.0.0.1:5199, launches the exe with --workspace/--theme, resizes the window,
optionally sends keys and Ctrl+Enter, captures it with PrintWindow, then cleans up (also on failure).
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\..\src\Yaapic.App.WinUI\bin\Release\net11.0-windows10.0.19041.0\win-x64\publish\Yaapic.App.WinUI.exe'),
    [ValidateSet('dark', 'light')][string]$Theme = 'dark',
    [Parameter(Mandatory)][string]$Out,
    [int]$Width = 1440,
    [int]$Height = 900,
    [switch]$Send,
    [int]$WaitMs = 1500,
    [string]$Keys = '',
    [string]$Select = '',
    [string]$FocusElement = '',
    [string]$Request = 'create-user',
    [switch]$NoTabs,
    [switch]$NoStub
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
              'slow' = '11111111-0000-0000-0000-000000000005' }
    $session = Join-Path $temp '.yaapic\session.yaml'
    $open = if ($NoTabs) { 'openRequests: []' } else { "openRequests:`n  - $($ids[$Request])" }
    [IO.File]::WriteAllText($session, "version: 1`n$open`nactiveRequest: $($ids[$Request])`nactiveEnvironment: 22222222-0000-0000-0000-000000000001`n", [Text.UTF8Encoding]::new($false))

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
                elseif ($p -match '^/delay/(\d+)$') { Start-Sleep -Milliseconds ([int]$Matches[1]); $body = '{"delayed":true}' }
                $b = [Text.Encoding]::UTF8.GetBytes($body)
                $c.Response.StatusCode = $code; $c.Response.ContentType = 'application/json'
                $c.Response.OutputStream.Write($b, 0, $b.Length); $c.Response.Close()
            }
        })
        $stub = $ps.BeginInvoke()
        Start-Sleep -Milliseconds 300
    }

    $proc = Start-Process $Exe -ArgumentList '--workspace', "`"$temp`"", '--theme', $Theme -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ($proc.MainWindowHandle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200; $proc.Refresh() }
    if ($proc.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Main window did not appear.' }
    $h = $proc.MainWindowHandle
    [void][Win32]::SetWindowPos($h, [IntPtr]::Zero, 0, 0, $Width, $Height, 0x0044) # SWP_NOZORDER | SWP_SHOWWINDOW
    Start-Sleep -Milliseconds $WaitMs
    if ($Select -or $FocusElement) {
        # UI Automation by element name: Select invokes a button by AutomationId (e.g. BodyTab), FocusElement focuses a field by name.
        $root = [Windows.Automation.AutomationElement]::FromHandle($h)
        $find = { param($prop, $n) $root.FindFirst([Windows.Automation.TreeScope]::Descendants,
            (New-Object Windows.Automation.PropertyCondition($prop, $n))) }
        if ($Select) {
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
    if ($Send) {
        # UI Automation instead of Ctrl+Enter: it needs no focus, so it also works when another window is in front.
        $root = [Windows.Automation.AutomationElement]::FromHandle($h)
        $btn = $root.FindFirst([Windows.Automation.TreeScope]::Descendants,
            (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'Send')))
        if (-not $btn) { throw 'Send button not found.' }
        $btn.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds $WaitMs
    }

    $proc.Refresh()
    if ($proc.HasExited) { throw "Yaapic exited with code $($proc.ExitCode) before the capture." }
    $r = New-Object Win32+RECT; [void][Win32]::GetWindowRect($h, [ref]$r)
    $bmp = [Drawing.Bitmap]::new($r.R - $r.L, $r.B - $r.T)
    $g = [Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc()
    [void][Win32]::PrintWindow($h, $dc, 2) # PW_RENDERFULLCONTENT
    $g.ReleaseHdc($dc); $g.Dispose()
    New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
    $bmp.Save($Out, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    Write-Host "Saved $Out"
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    # GetContext blocks inside the runspace: do not wait for it, the process exit ends it.
    if ($ps) { [void]$ps.BeginStop($null, $null) }
    if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
}
