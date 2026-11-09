# M3-15: Native build pipeline for QuickJS-NG and Lua 5.4

Status: done
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

- [x] `pwsh native/Build-Natives.ps1` on the host produces both libraries in `artifacts/native/<host rid>/`; a second run
      does nothing (cache); a tampered archive hash fails the script.
- [x] A test in `Yaapic.Scripting.Tests` (`NativeLibraryAvailabilityTests`) loads each library with
      `NativeLibrary.TryLoad` from `AppContext.BaseDirectory` and asserts that the exported symbols `JS_NewRuntime` and
      `luaL_newstate` exist (`NativeLibrary.TryGetExport`).
- [x] `dotnet build Yaapic.sln -c Release` (and the cross-platform filter) builds from a clean checkout with the script
      invoked by the target; the publish folder of an App project contains the libraries and the notices file.
- [x] CI workflow updated (cannot be run locally: say so in Notes and check the YAML with `actionlint` if available).
- [x] Notes record the pinned tags, hashes, the CMake options actually used and any workaround.

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

## Notes

- Pinned sources (SHA-256 of the source archive, in `native/versions.json`):
  - QuickJS-NG `v0.18.0` (`https://github.com/quickjs-ng/quickjs/archive/refs/tags/v0.18.0.tar.gz`):
    `28bcd89aa3683fd2a946537e5ce3f11cc46b5cbf3383c7561aaef3df5ff9f1dd`.
  - Lua `5.4.8` (`https://www.lua.org/ftp/lua-5.4.8.tar.gz`):
    `4f18ddae154e793e46eeab727c59ef1c0c0c2b744e7b94219710d76f530629ae`. 5.4.8 was the latest 5.4 release known at
    implementation time; the lua.org download index could not be listed from the build host.
- CMake options: QuickJS-NG `-DBUILD_SHARED_LIBS=ON -DQJS_BUILD_LIBC=OFF -DQJS_ENABLE_INSTALL=OFF`, target `qjs` only
  (std/os are not compiled in). Lua uses `native/lua/CMakeLists.txt` (all `*.c` but `lua.c`/`luac.c`, `LUA_BUILD_AS_DLL` on
  Windows, `LUA_USE_LINUX`/`LUA_USE_MACOSX` elsewhere, no `LUA_COMPAT_5_3`). Windows uses the default Visual Studio
  generator with `--config Release` (no vcvars needed); Linux and macOS use clang with `CMAKE_BUILD_TYPE`.
- Workarounds: the QuickJS-NG CMake target is named `qjs`, so the built file is renamed to `quickjs-ng` on copy (and its
  macOS install name is rewritten with `install_name_tool`). A `.built` stamp (hash of `versions.json`, the script and the
  Lua CMake file) makes a second run a no-op. A cached archive with a wrong hash fails and is never replaced silently.
- The apps did not reference `Yaapic.Scripting` yet, so nothing flowed to their output: `Yaapic.Composition` now has a
  `ProjectReference` to it (already allowed by the architecture test), which carries the libraries and
  `THIRD-PARTY-NOTICES.md` to both app builds and publishes (verified with AOT publishes of `Yaapic.App.Avalonia` and
  `Yaapic.App.WinUI`).
- CI was changed (cache of `artifacts/native/_src`, `Build-Natives.ps1` step, upload of `artifacts/native/<rid>`) but could
  not be run here; `actionlint` is not installed. Only `win-x64` was built and tested locally; the Linux and macOS paths
  (clang, `libqjs.so`/`libqjs.dylib` names, `install_name_tool`) are unverified.
