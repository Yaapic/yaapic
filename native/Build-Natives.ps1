#Requires -Version 7
<#
.SYNOPSIS
    Builds the QuickJS-NG and Lua 5.4 shared libraries from the pinned upstream sources in native/versions.json.
.DESCRIPTION
    Downloads the source archives into artifacts/native/_src (hash checked, a mismatch fails), builds them with CMake and
    writes quickjs-ng and lua54 (platform names) to artifacts/native/<rid>/. A second run with the same versions.json does
    nothing unless -Force is given. No binary is ever committed.
.PARAMETER Rid
    Runtime identifier to build for: win-x64, linux-x64 or osx-arm64. Defaults to the host RID.
.PARAMETER Configuration
    CMake build configuration (default Release).
.PARAMETER Force
    Rebuild even when the libraries are up to date.
#>
[CmdletBinding()]
param(
    [string] $Rid,
    [string] $Configuration = 'Release',
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = Split-Path -Parent $PSScriptRoot
$versionsPath = Join-Path $PSScriptRoot 'versions.json'
$versions = Get-Content $versionsPath -Raw | ConvertFrom-Json
$artifacts = Join-Path $repo 'artifacts/native'
$srcDir = Join-Path $artifacts '_src'

if (-not $Rid) {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    $os = if ($IsWindows) { 'win' } elseif ($IsMacOS) { 'osx' } else { 'linux' }
    $Rid = "$os-$arch"
}

$names = switch -Wildcard ($Rid) {
    'win-*' { @{ qjs = 'quickjs-ng.dll'; lua = 'lua54.dll'; qjsBuilt = 'qjs.dll' } }
    'osx-*' { @{ qjs = 'libquickjs-ng.dylib'; lua = 'liblua54.dylib'; qjsBuilt = 'libqjs.dylib' } }
    'linux-*' { @{ qjs = 'libquickjs-ng.so'; lua = 'liblua54.so'; qjsBuilt = 'libqjs.so' } }
    default { throw "Unsupported RID '$Rid' (supported: win-x64, linux-x64, osx-arm64)." }
}

$outDir = Join-Path $artifacts $Rid
$stamp = Join-Path $outDir '.built'
$signature = (Get-FileHash $versionsPath -Algorithm SHA256).Hash + "|$Rid|$Configuration|" +
    (Get-FileHash $PSCommandPath -Algorithm SHA256).Hash +
    (Get-FileHash (Join-Path $PSScriptRoot 'lua/CMakeLists.txt') -Algorithm SHA256).Hash +
    (Get-FileHash (Join-Path $PSScriptRoot 'lua/yaapic_lua.c') -Algorithm SHA256).Hash

if (-not $Force -and (Test-Path $stamp) -and (Get-Content $stamp -Raw).Trim() -eq $signature -and
    (Test-Path (Join-Path $outDir $names.qjs)) -and (Test-Path (Join-Path $outDir $names.lua))) {
    Write-Host "Native libraries for $Rid are up to date ($outDir)."
    return
}

function Invoke-Native {
    param([string] $Exe, [string[]] $Arguments)
    & $Exe @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "'$Exe $($Arguments -join ' ')' failed with exit code $LASTEXITCODE." }
}

function Get-Source {
    param($Entry)
    $archive = Join-Path $srcDir $Entry.archive
    if (-not (Test-Path $archive)) {
        New-Item -ItemType Directory -Force $srcDir | Out-Null
        Write-Host "Downloading $($Entry.url)"
        Invoke-WebRequest -Uri $Entry.url -OutFile $archive
    }
    $actual = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Entry.sha256.ToLowerInvariant()) {
        throw "SHA-256 mismatch for $($Entry.archive): expected $($Entry.sha256), got $actual. Delete '$archive' to download it again, or fix native/versions.json."
    }
    $dir = Join-Path $srcDir $Entry.folder
    if (-not (Test-Path $dir)) { Invoke-Native tar @('-xzf', $archive, '-C', $srcDir) }
    return $dir
}

$cmake = (Get-Command cmake -ErrorAction SilentlyContinue)?.Source
if (-not $cmake -and $IsWindows -and (Test-Path 'C:\Program Files\CMake\bin\cmake.exe')) { $cmake = 'C:\Program Files\CMake\bin\cmake.exe' }
if (-not $cmake) { throw 'cmake was not found on PATH. Install CMake 3.20 or later.' }

$qjsSrc = Get-Source $versions.'quickjs-ng'
$luaSrc = Get-Source $versions.lua

$osxArch = if ($Rid -eq 'osx-arm64') { @('-DCMAKE_OSX_ARCHITECTURES=arm64') } else { @() }
$compiler = if (-not $IsWindows -and (Get-Command clang -ErrorAction SilentlyContinue)) { @('-DCMAKE_C_COMPILER=clang') } else { @() }

# Single-config generators (Makefiles, Ninja) need the build type; the Visual Studio generator takes --config instead.
$buildType = $IsWindows ? @() : @("-DCMAKE_BUILD_TYPE=$Configuration")

function Build-Library {
    param([string] $Name, [string] $Source, [string[]] $Options, [string] $Target, [string] $FileName)
    $build = Join-Path $artifacts "_build/$Rid/$Name"
    if ($Force -and (Test-Path $build)) { Remove-Item -Recurse -Force $build }
    Invoke-Native $cmake (@('-S', $Source, '-B', $build) + $buildType + $compiler + $osxArch + $Options)
    Invoke-Native $cmake @('--build', $build, '--config', $Configuration, '--target', $Target)
    $built = Get-ChildItem $build -Recurse -File -Filter $FileName | Select-Object -First 1
    if (-not $built) { throw "Build of $Name finished but '$FileName' was not found under $build." }
    return $built.FullName
}

# QuickJS-NG: QJS_BUILD_LIBC=OFF leaves std/os out of the library. Its CMake target is named qjs, so the output is renamed.
$qjsBuilt = Build-Library 'quickjs-ng' $qjsSrc @('-DBUILD_SHARED_LIBS=ON', '-DQJS_BUILD_LIBC=OFF', '-DQJS_ENABLE_INSTALL=OFF') 'qjs' $names.qjsBuilt
$luaBuilt = Build-Library 'lua' (Join-Path $PSScriptRoot 'lua') @("-DLUA_SRC_DIR=$(Join-Path $luaSrc 'src')") 'lua54' $names.lua

New-Item -ItemType Directory -Force $outDir | Out-Null
Copy-Item $qjsBuilt (Join-Path $outDir $names.qjs) -Force
Copy-Item $luaBuilt (Join-Path $outDir $names.lua) -Force
if ($IsMacOS) {
    # The renamed dylib must not keep the install name of the upstream file name.
    Invoke-Native install_name_tool @('-id', "@rpath/$($names.qjs)", (Join-Path $outDir $names.qjs))
}
Set-Content $stamp $signature -NoNewline
Write-Host "Built $($names.qjs) and $($names.lua) in $outDir."
