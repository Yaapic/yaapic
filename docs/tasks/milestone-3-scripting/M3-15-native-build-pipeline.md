# M3-15: Native build pipeline for QuickJS-NG and Lua 5.4

Status: todo
Platform: any
Depends on: M3-14

## Goal

Reproducible native libraries for the engines, built from pinned upstream sources on `win-x64`, `linux-x64` and `osx-arm64`
(the CI runners), copied next to every app build and publish. No binary is committed.

## Design decisions

- Read first: ADR 0001, ADR 0009 (native binaries paragraph), `.github/workflows/ci.yml`, `docs/dependencies.md`,
  `tools/screenshots/README.md` (script conventions, PowerShell 7 is the repo's scripting shell).
- `native/` at the repository root:
  - `native/versions.json`: for each library the upstream repository or URL, the **tag**, the **SHA-256** of the source
    archive, and the licence file name. Pick the latest stable tags at implementation time: QuickJS-NG from
    `https://github.com/quickjs-ng/quickjs` (a release tag), Lua from `https://www.lua.org/ftp/lua-5.4.<latest>.tar.gz`.
    Record the chosen tags and hashes in Notes.
  - `native/Build-Natives.ps1` (PowerShell 7, runs on all three OSes): parameters `-Rid` (default: the host RID),
    `-Configuration Release`, `-Force`. Steps: download the archives into `artifacts/native/_src` (skip when present and
    the hash matches; **fail on a hash mismatch**), extract, configure and build with CMake (`cmake` and a C compiler are
    already needed for Native AOT: MSVC on Windows, clang on Linux and macOS), output shared libraries to
    `artifacts/native/<rid>/`: `quickjs-ng`, `lua54` with the platform names (`quickjs-ng.dll`/`libquickjs-ng.so`/
    `libquickjs-ng.dylib`, `lua54.dll`/`liblua54.so`/`liblua54.dylib`). QuickJS-NG builds with its own `CMakeLists.txt`
    (`-DBUILD_SHARED_LIBS=ON`, `-DBUILD_QJS_LIBC=OFF` or the closest option so that `std`/`os` are not compiled in; check
    the option names in the tag you pin); Lua has no CMake file, so add `native/lua/CMakeLists.txt` that compiles `onelua`-free
    sources (all `*.c` except `lua.c` and `luac.c`) with `LUA_BUILD_AS_DLL` on Windows and exports the API; use the default
    `LUA_COMPAT_5_3` off.
  - `native/THIRD-PARTY-NOTICES.md`: both MIT licence texts and versions (copied into the publish output by M3-16/M3-24).
- `src/Yaapic.Scripting/Yaapic.Scripting.csproj`: an MSBuild target `EnsureNativeScriptingLibraries` (BeforeBuild, condition
  `Exists('$(MSBuildThisFileDirectory)..\..\native\Build-Natives.ps1')`) that, when `artifacts/native/<rid>/` is missing,
  runs the script through `pwsh` for the current RID (`$(NETCoreSdkRuntimeIdentifier)` when no `RuntimeIdentifier` is
  set) with an error message that names the command if it fails; and `Content`/`None` items with
  `CopyToOutputDirectory=PreserveNewest` + `CopyToPublishDirectory` for the library files, flowing to the app projects
  (verify by publishing `Yaapic.App.Avalonia` and checking the folder). `artifacts/` is git-ignored (add it if not).
- CI (`.github/workflows/ci.yml`): before `Build`, a step `pwsh native/Build-Natives.ps1` on every matrix OS; cache
  `artifacts/native/_src` keyed by `native/versions.json`; upload `artifacts/native/<rid>` as a workflow artifact.
- `docs/dependencies.md`: QuickJS-NG and Lua rows (version, MIT, "built from source by `native/Build-Natives.ps1`, shipped
  next to the exe, loaded with `LibraryImport`"), and a "Native libraries" section describing how to update a version.
- No P/Invoke code in this card: it ends with the libraries on disk and a tiny test that proves they can be loaded.

## Out of scope

Bindings (M3-16, M3-24), static linking, arm64 Windows/Linux, signing and notarisation (milestone 10).

## Acceptance criteria

- [ ] `pwsh native/Build-Natives.ps1` on the host produces both libraries in `artifacts/native/<host rid>/`; a second run
      does nothing (cache); a tampered archive hash fails the script.
- [ ] A test in `Yaapic.Scripting.Tests` (`NativeLibraryAvailabilityTests`) loads each library with
      `NativeLibrary.TryLoad` from `AppContext.BaseDirectory` and asserts that the exported symbols `JS_NewRuntime` and
      `luaL_newstate` exist (`NativeLibrary.TryGetExport`).
- [ ] `dotnet build Yaapic.sln -c Release` (and the cross-platform filter) builds from a clean checkout with the script
      invoked by the target; the publish folder of an App project contains the libraries and the notices file.
- [ ] CI workflow updated (cannot be run locally: say so in Notes and check the YAML with `actionlint` if available).
- [ ] Notes record the pinned tags, hashes, the CMake options actually used and any workaround.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (published exes, stub servers, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m3-scripting` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`build: build QuickJS-NG and Lua natives from pinned sources`
