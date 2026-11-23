<#
.SYNOPSIS
Captures the 16 Milestone 5 WinUI screenshots (eight screens, dark and light) into docs/screenshots/m5.
.DESCRIPTION
Publish the app with Native AOT first (see README.md), then run: pwsh tools/screenshots/Capture-M5.ps1 [-Only <screen>].
Every capture is its own pwsh process and runs in English. The sample import files are generated here, put on the clipboard
(the dialog has a paste box, so no file picker is involved) and never written to disk; the previous clipboard text is restored
at the end. Nothing in them is a real secret. Export saves through the system file picker: the script types a path under a
temporary folder that is deleted afterwards.
#>
param([string]$Only = '', [string]$Out = (Join-Path $PSScriptRoot '..\..\docs\screenshots\m5'))
$ErrorActionPreference = 'Stop'
$capture = Join-Path $PSScriptRoot 'Capture-WinUI.ps1'
$saveDir = Join-Path ([IO.Path]::GetTempPath()) ("yaapic-m5-export-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $saveDir | Out-Null

# A Postman collection with a pre-request script (script review), a GraphQL body, a dynamic variable and saved examples (warnings).
$postman = [ordered]@{
    info = [ordered]@{ name = 'Pet Shop'; schema = 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json' }
    item = @(
        [ordered]@{ name = 'Pets'; item = @(
            [ordered]@{ name = 'List pets'; request = [ordered]@{ method = 'GET'; url = 'http://127.0.0.1:5199/users?limit=10'; description = 'Lists the pets.' } },
            [ordered]@{ name = 'Create pet'; event = @([ordered]@{ listen = 'prerequest'; script = [ordered]@{ exec = @('pm.environment.set("ts", Date.now());') } })
                request = [ordered]@{ method = 'POST'; url = 'http://127.0.0.1:5199/users'; header = @(@{ key = 'X-Trace'; value = '{{$guid}}' })
                    body = [ordered]@{ mode = 'raw'; raw = '{"name":"Rex"}' } }
                response = @([ordered]@{ name = 'Created'; code = 201 }) },
            [ordered]@{ name = 'Search'; request = [ordered]@{ method = 'POST'; url = 'http://127.0.0.1:5199/users'
                body = [ordered]@{ mode = 'graphql'; graphql = [ordered]@{ query = '{ pets { id } }' } } } }) })
} | ConvertTo-Json -Depth 12 -Compress
# A HAR with an API call and static resources (shows the "Include images, fonts and styles" option and aggregated notes).
$entry = { param($url, $mime) [ordered]@{ startedDateTime = '2026-01-01T10:00:00.000Z'; time = 12
    request = [ordered]@{ method = 'GET'; url = $url; httpVersion = 'HTTP/1.1'; headers = @(); queryString = @(); cookies = @(); headersSize = -1; bodySize = 0 }
    response = [ordered]@{ status = 200; statusText = 'OK'; httpVersion = 'HTTP/1.1'; headers = @(); cookies = @(); content = [ordered]@{ size = 10; mimeType = $mime }; redirectURL = ''; headersSize = -1; bodySize = 10 }
    cache = @{}; timings = [ordered]@{ send = 0; wait = 12; receive = 0 } } }
$har = [ordered]@{ log = [ordered]@{ version = '1.2'; creator = [ordered]@{ name = 'demo'; version = '1' }
    entries = @((& $entry 'http://127.0.0.1:5199/users' 'application/json'), (& $entry 'http://127.0.0.1:5199/static/app.css' 'text/css'),
                (& $entry 'http://127.0.0.1:5199/static/logo.png' 'image/png'), (& $entry 'http://127.0.0.1:5199/static/font.woff2' 'font/woff2')) } } | ConvertTo-Json -Depth 12 -Compress

$pasteBox = 'focus:...or paste a cURL command or the content of a file'
$clip = @{} # screen name -> text put on the clipboard just before its capture
$paste = "$pasteBox|keys:^v|wait:500|invoke:Preview|wait:1200"
$importMenu = 'invoke:New request|wait:1200'
$openImport = "$importMenu|invoke:Import...|wait:1500"
$screens = [ordered]@{
    'import-empty'          = @('-Steps', $openImport)
    'import-postman-report' = @('-Steps', ($openImport + '|' + $paste))
    'import-har'            = @('-Steps', ($openImport + '|' + $paste))
    'import-error'          = @('-Screen', '-Steps', ($openImport + '|' + $paste + '|invoke:Format|wait:600'))
    'export-collection'     = @('-Steps', "keys:^k|wait:500|type:Export a collection|wait:500|keys:{ENTER}|wait:1000|invoke:Export...|wait:2500|savepath:$saveDir\pet-shop.postman_collection.json|wait:800")
    'export-environments'   = @('-Steps', "keys:^k|wait:500|type:Export a collection|wait:500|keys:{ENTER}|wait:1000|focus:Format|keys:{DOWN}|wait:800")
    'sidebar-menu'          = @('-Screen', '-Steps', $importMenu)
    'folder-menu'           = @('-Screen', '-Steps', 'rclick:180,211|wait:600')
}
$clip['import-postman-report'] = $postman; $clip['import-har'] = $har; $clip['import-error'] = 'this is not an importable document'
$saved = Get-Clipboard -Raw -ErrorAction SilentlyContinue
try {
    foreach ($name in $screens.Keys) {
        if ($Only -and $Only -ne $name) { continue }
        if ($clip[$name]) { Set-Clipboard -Value $clip[$name] }
        foreach ($theme in 'dark', 'light') {
            & pwsh -NoProfile -File $capture -Theme $theme -Language english @($screens[$name]) -Out (Join-Path $Out "$name-$theme.png")
            if ($LASTEXITCODE -ne 0) { throw "Capture $name-$theme failed." }
        }
    }
}
finally {
    if ($saved) { Set-Clipboard -Value $saved } else { Set-Clipboard -Value ' ' }
    Remove-Item $saveDir -Recurse -Force -ErrorAction SilentlyContinue
}
