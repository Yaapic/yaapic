# M2-68: WinUI: migrate to Windows App SDK 2.x

Status: todo
Platform: windows
Depends on: M2-67

## Goal

Move `Yaapic.App.WinUI` from Windows App SDK 1.8 to 2.x (target `net11.0-windows10.0.26100`), which is the prerequisite
of the LoomEditor code editor (M2-69). No visible change: the app looks and behaves exactly as before.

## Design decisions

- **Verified beforehand** (by the user's session, not part of this repository): a throwaway project with
  `Microsoft.WindowsAppSDK` `2.6.5` and `LoomEditor.WinUI` `0.4.0`, TFM `net11.0-windows10.0.26100`, `PublishAot`,
  restores, builds with 0 warnings and publishes with no IL2xxx/IL3xxx. This card applies the same versions to the
  real app and fixes what the real app breaks. If `2.6.5` is no longer the newest stable 2.x when the card runs, still
  use `2.6.5` (verified) and note the newer version as a follow-up in Notes.
- **Why now**: M1-44 rejected LoomEditor because it needs Windows App SDK 2.5.1 or later and a `26100` target (see
  `docs/dependencies.md`, "Rejected: LoomEditor.WinUI").
- `src/Yaapic.App.WinUI/Yaapic.App.WinUI.csproj`: `TargetFramework` becomes `net11.0-windows10.0.26100`;
  `TargetPlatformMinVersion` and `SupportedOSPlatformVersion` stay `10.0.19041.0` so Windows 10 2004 and later keep
  working (check that the 2.x runtime still supports that minimum; if it does not, raise the minimum to what the 2.x
  documentation states and record the user-visible consequence in Notes and in `docs/dependencies.md`).
  Keep every other property (`WindowsPackageType` `None`, `WindowsAppSDKSelfContained`, `UseOptimizedPublish`,
  `AllowUnsafeBlocks`, `EnableMsixTooling`) unless the 2.x build forces a change; each change gets a one-line XML
  comment saying why.
- `Directory.Packages.props`: `Microsoft.WindowsAppSDK` `2.6.5`. Check whether `Microsoft.Windows.SDK.BuildTools`
  (`10.0.26100.7463`) still matches what 2.6.5 requires; align it if restore reports `NU1605`/`NU1608`.
  `FluentIcons.WinUI` `2.2.344`: check that it still restores, builds and publishes with 2.x; if it does not, move to
  the newest version that does, and say so in `docs/dependencies.md`.
- **Breaking changes**: read the Windows App SDK 2.x release notes and migration guide before touching code, then build.
  Fix API or XAML breaks in `Yaapic.App.WinUI` only (no ViewModel, Core or Domain change). Typical suspects: bootstrap
  and self-contained deployment properties, `Window`/`AppWindow` title bar APIs used by the shell (M1-38),
  `ContentDialog` `XamlRoot`, file picker initialisation (M2-15), clipboard, the `design/xaml` resources compiled as
  `Page` items, the `PrintWindow`-based screenshot script. Do not rewrite anything that still works.
- **Resource keys**: verify that every key referenced by the app still resolves against
  `design/xaml/Yaapic.Colors.WinUI.xaml` and the Fluent theme resources of 2.x (renamed or removed keys fail only at
  run time, as a XAML parse exception or a silently missing brush). Search the XAML for every
  `ThemeResource`/`StaticResource` key, list the ones defined by WinUI itself (for example `TextControlBackground`,
  `ButtonBackground...`, `TreeViewItem...`) and confirm each still exists in 2.x (read the `themeresources.xaml` of the
  package in the NuGet cache). Any change goes in the app's own resources, never in `design/xaml/*` (generated).
- **CI** (`.github/workflows/ci.yml`): the Windows job must build, test, publish with Native AOT and smoke test with the
  2.x SDK. Check that the runner image has the Windows 11 SDK `10.0.26100` (add an install step if not) and that
  `global.json` is still honoured by `actions/setup-dotnet`. Do not change the Linux and macOS jobs.
- **Screenshot scripts and docs**: `tools/screenshots/Capture-WinUI.ps1` and the docs that hard-code the publish folder
  `net11.0-windows10.0.19041.0` move to `...26100.0`; search the repository (excluding `bin`/`obj`) for `19041.0`
  and update every script or doc that points at the old publish folder.
- **Smoke test of the real app** (not only "starts and exits"): publish with Native AOT, launch the exe with a seeded
  workspace through `Capture-WinUI.ps1`, then by hand or UI Automation: expand and collapse the sidebar tree, open the
  history list, open a request, switch every request tab (Params, Headers, Body, Auth, Settings), change the method,
  pick each body kind, send a request, switch Pretty/Raw, open the environments dialog and the folder settings dialog,
  open the command palette, and **hover and click every button and control once**, looking for a crash, a XAML parse
  error, an invisible control or a wrong brush. Dark and light theme both. Note anything odd in Notes.
- **Docs**: `docs/dependencies.md`: update the `Microsoft.WindowsAppSDK` row (version `2.6.5`, the 26100 target, the
  verification above) and reword the "Rejected: LoomEditor.WinUI" section opening to say that the blocker is removed by
  this card and that M2-69 adopts it. `docs/adr/0002-code-editor.md` is updated by M2-69, not here. Contributor docs
  (`README.md`): update the prerequisites (Windows 11 SDK 26100) if they list the SDK.

## Out of scope

LoomEditor (M2-69), Avalonia, new features or restyling, any change to ViewModels, Core or Domain.

## Acceptance criteria

- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; `dotnet test --solution Yaapic.sln -c Release` all green;
      architecture tests green.
- [ ] `dotnet publish src/Yaapic.App.WinUI -c Release -r win-x64 -p:PublishAot=true` 0 IL2xxx/IL3xxx warnings
      (`win-arm64` builds too if the SDK allows it, otherwise noted).
- [ ] Smoke test of the published exe as described above, dark and light, with no crash and no XAML/resource error.
- [ ] Every WinUI-defined resource key used by the XAML is confirmed present in 2.x (list in Notes).
- [ ] CI workflow and screenshot tooling reference the 26100 target; nothing in the repository still points at
      `19041.0` publish folders.
- [ ] `docs/dependencies.md` updated.
- [ ] No Yaapic or stub-server process left running.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (the published exe, the stub server, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Commit on `feat/m2-editor-collections` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`build(winui): move to Windows App SDK 2.6.5 and the 26100 target`

## Notes
