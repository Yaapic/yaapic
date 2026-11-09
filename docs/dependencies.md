# Dependencies

Every third-party dependency needs a written justification here before it is added to `Directory.Packages.props`.

| Package | Version | Used by | Justification |
|---|---|---|---|
| xunit.v3 | 4.0.2 | Yaapic.Architecture.Tests | Test framework with native Microsoft.Testing.Platform support, required by `dotnet test --solution`. Test-only, never shipped. |
| VYaml | 1.5.0 | Yaapic.Infrastructure | YAML serialisation for the workspace file format (ADR 0005). MIT, source-generated `[YamlObject]` formatters, no reflection, Native AOT. Verified in M1-10, see below. |
| CommunityToolkit.Mvvm | 8.4.2 | Yaapic.ViewModels | `ObservableObject`, `[ObservableProperty]` and `[RelayCommand]` source generators for ViewModels. MIT, first-party .NET Foundation; generated code is reflection-free on the paths we use (`ObservableValidator` and `Messenger.RegisterAll` are forbidden). |
| Microsoft.WindowsAppSDK | 2.6.5 | Yaapic.App.WinUI | WinUI 3 runtime and XAML compiler; first-party, Native AOT supported. Target `net11.0-windows10.0.26100` (M2-68); verified with `LoomEditor.WinUI` 0.4.0 and `FluentIcons.WinUI` 2.2.344 (restore, build, AOT publish without IL2xxx/IL3xxx). Minimum OS stays Windows 10 2004 (`10.0.19041.0`). |
| LoomEditor.WinUI | 0.5.0 | Yaapic.App.WinUI | Code editor of the request and response bodies behind `ICodeEditor` (ADR 0002): syntax colours from the Yaapic tokens, line numbers and large text on WinUI, where no other mature editor exists. MIT. Restore, build and Native AOT publish with Windows App SDK 2.6.5 give 0 warnings and 0 IL2xxx/IL3xxx; the AOT exe renders, edits and recolours on theme change (M2-69). |
| LoomEditor.Avalonia | 0.5.0 | Yaapic.App.Avalonia | Code editor of the request and response bodies behind `ICodeEditor` (ADR 0002), the Avalonia twin of LoomEditor.WinUI: syntax colours from the Yaapic tokens, line numbers, folding and large text, with no TextMate registry or grammar files to bundle. MIT. Requires `Avalonia >= 12.1.5` (the Avalonia packages moved from 12.1.4 to 12.1.5 for it). Restore, build and Native AOT publish give 0 new IL2xxx/IL3xxx (only the two existing `Avalonia.Win32` ILC messages); the publish ships `onig.dll` (native regex engine of the grammars). The AOT exe renders, edits, folds and recolours on theme change (M2-70). |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.7463 | Yaapic.App.WinUI | Build-time tooling required by Windows App SDK projects. Not shipped. |
| QuickJS-NG | v0.18.0 | Yaapic.Scripting | JavaScript engine (ADR 0001, ADR 0009). MIT. Native library built from source by `native/Build-Natives.ps1`, shipped next to the exe, loaded with `LibraryImport`. |
| Lua | 5.4.8 | Yaapic.Scripting | Lua engine (ADR 0001, ADR 0009). MIT. Native library built from source by `native/Build-Natives.ps1`, shipped next to the exe, loaded with `LibraryImport`. |

Milestone 2 (editor, collections, environments, history, command palette) added the two code editor packages above (`LoomEditor.WinUI`, `LoomEditor.Avalonia`).

## Native libraries (M3-15)

`native/versions.json` pins, for QuickJS-NG and Lua, the source URL, the tag, the SHA-256 of the source archive and the
licence file. `native/Build-Natives.ps1` (PowerShell 7, Windows, Linux and macOS) downloads the archives into
`artifacts/native/_src`, fails on a hash mismatch, builds them with CMake (MSVC on Windows, clang elsewhere) and writes
`quickjs-ng` and `lua54` to `artifacts/native/<rid>/` (`win-x64`, `linux-x64`, `osx-arm64`). A second run does nothing.
`Yaapic.Scripting` runs the script when that folder is missing and copies the libraries and
`native/THIRD-PARTY-NOTICES.md` next to every app build and publish. No binary is committed.

`native/lua/yaapic_lua.c` (M3-24) is compiled into the `lua54` library. A Lua error is a `longjmp`, which must never cross a managed frame under Native AOT, so the bindings call only the non-raising `yl_*` shim (`yl_new`, `yl_close`, `yl_open_libs`, `yl_set_host`, `yl_set_limits`, `yl_last_abort`, `yl_run`, `yl_alloc`, `yl_free`, `yl_now_ms`), never the Lua API itself. The shim owns the memory-limited allocator, the restricted library set, the deadline and cancel hook and the traceback handler. `Build-Natives.ps1` rebuilds when the shim changes.

To update a version: change `tag`, `url`, `archive`, `folder` and `sha256` in `native/versions.json` (compute the hash
with `Get-FileHash`), update `native/THIRD-PARTY-NOTICES.md` and the rows above, then run
`pwsh native/Build-Natives.ps1 -Force`. Delete `artifacts/native/<rid>` to make the next MSBuild build rebuild it.

## Secret vault (ADR 0010)

The vault adds **no package**: `System.Security.Cryptography` (AES-GCM, PBKDF2, random) and `System.Text.Json` are part of
the framework. The only exception is `System.Security.Cryptography.ProtectedData` (DPAPI), which M3-48 adds for Windows.

## Architecture tests: NetArchTest

NetArchTest was not adopted. The dependency rules are project-level, so `Yaapic.Architecture.Tests` checks the
`ProjectReference` items of each csproj directly (an unused reference is not emitted into the assembly, so a type-level
check would miss it). No extra package and no reflection-based tooling are needed. NetArchTest compatibility with
Native AOT was not evaluated; revisit if type-level rules (namespaces, naming) are needed.

## VYaml: Native AOT check (M1-10)

VYaml 1.5.0 (latest stable on nuget.org at the time). A scratch console project (not committed) referenced
`Yaapic.Infrastructure` and round-tripped a `[YamlObject]` type holding a string, a nested `[YamlObject]`, a `List<T>`
of objects and a multi-line string. `dotnet publish -c Release -r win-x64 -p:PublishAot=true` produced **0 IL2xxx/IL3xxx
warnings**, and the published binary ran and the round trip matched.

Multi-line strings are emitted as **literal block scalars** (`|`), so scripts and bodies stay readable in diffs.
Property names are emitted in camelCase by default (`openRequests`), matching the file format.

## Bundled assets

| Asset | Version | Licence | Used by | Source | Justification |
|---|---|---|---|---|---|
| JetBrains Mono (variable) | 2.304 | SIL OFL 1.1 (`Assets/Fonts/OFL.txt`) | Yaapic.App.WinUI, Yaapic.App.Avalonia | <https://github.com/JetBrains/JetBrainsMono/releases/tag/v2.304> | Code font of the design system (`type.code`): URLs, headers and bodies are always monospace. |

## History: LoomEditor.WinUI (M1-44, M2-68, M2-69)

Rejected by M1-44, unblocked by M2-68 (Windows App SDK 2.6.5, 26100 target), adopted by M2-69 (`LoomEditor.WinUI` 0.4.0,
see the table above). `LoomEditor.Avalonia` 0.5.0 followed on Avalonia in M2-70 (AvaloniaEdit + TextMate was planned and abandoned). The text below is the historical M1-44 finding.

`LoomEditor.WinUI` `0.2.0-alpha.5` (MIT, WinUI 3 code editor ported from Monaco, TextMate grammars, line numbers,
folding, `Theme` from a VS Code theme JSON, `IsReadOnly`, `ResetText`, `Model.ContentChanged`) was evaluated and
**rejected for now**: it does not resolve for `net11.0-windows10.0.19041.0` with the current Windows App SDK.

- The package ships its assemblies only under `lib/net11.0-windows10.0.26100`; at the project's `...19041.0`
  target NuGet restores it with no compile reference (`The type or namespace name 'LoomEditor' could not be found`).
- At target `...26100.0` the restore fails with `NU1605`: the package depends on `Microsoft.WindowsAppSDK >= 2.5.1`,
  the project uses 1.8.260921001.

Adopting it therefore means moving the whole WinUI app to Windows App SDK 2.x and a 26100 target, which is a platform
decision outside the card. The AOT and runtime checks were not run for that reason. The views use `CodeEditorView`
(ADR 0002) over a borderless `TextBox`, so switching later only changes that one control.

## System.Text.Json (history store, M2-55)

`FileHistoryStore` writes `.yaapic/history.jsonl` with source-generated `System.Text.Json` (`HistoryJsonContext`, no
reflection). `System.Text.Json` is part of the framework: no new package.
