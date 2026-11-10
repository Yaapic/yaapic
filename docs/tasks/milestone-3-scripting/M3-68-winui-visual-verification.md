# M3-68: WinUI visual verification of the milestone 3 views

Status: done
Platform: windows
Depends on: M3-51, M3-52, M3-53, M3-54, M3-55, M3-56

## Goal

Capture the new WinUI screens in dark and light from the AOT build, commit the screenshots and record how they differ from the
mockups, as M2-67 did for milestone 2.

## Design decisions

- Same method as M2-67: `tools/screenshots/Capture-WinUI.ps1` (extend its seeded workspace and stub server only as far as needed:
  the `scripted` and `chatty` requests, a collection with scripts, an environment with a secret variable, an unlocked passphrase
  vault prepared by a documented helper step so the capture is repeatable; **no real secret**, use obviously fake values),
  1440 x 900, Native AOT publish output, PNG under `docs/screenshots/m3/` as `<screen>-<theme>.png`, each under 500 KB.

  | Screen | Compare with |
  |---|---|
  | `scripts-pre-request` (Scripts tab, JavaScript, code visible) | `MainWindow.dc.html` (request tabs, code editor) |
  | `scripts-post-lua` (post-response phase, Lua) | `DSSyntaxeIcones.dc.html` (syntax colours) |
  | `folder-scripts` (collection settings, Scripts section) | `DSComposants2.dc.html` (dialog) |
  | `console` (panel open after a send: header, logs, network, tests, one error row) | `MainWindow.dc.html` (console) |
  | `tests-tab` (Tests tab: 3 passed, 2 failed) | `MainWindow.dc.html` (response tabs), `DSComposants2.dc.html` (list rows, chips) |
  | `script-error` (pre-request abort state) | `States.dc.html` (error blocks) |
  | `secret-variables` (environment dialog, one row revealed, one masked) | `EnvVars.dc.html` |
  | `vault-setup` (setup dialog) | `DSComposants2.dc.html` (dialog) |
  | `status-bar` (crop of the bottom: Console toggle with counts, vault item) | `MainWindow.dc.html` (footer) |

  Each in `dark` and `light` (18 files).
- `docs/screenshots/m3/README.md` (English): how to regenerate (including the vault helper step), then per screen the two
  screenshots, the mockup artboard and a deviation list classified `fix now`, `M3 scope` or `later milestone`, comparing surface and
  text colours, accent use, code font, control heights, chips, borders, icons, the syntax colours of JavaScript and Lua, exactly like
  `docs/screenshots/m2/README.md`.
- One-line token, style or margin mistakes are fixed here (re-capture after the fix); anything larger goes under "Follow-up" with a
  one-line card proposal, no card written.
- Keyboard focus visibility (Tab through the Scripts tab, console controls, vault dialog) is checked and noted. Include the
  accessibility gap list for the script editor (LoomEditor UI Automation) carried over from M2-69.

## Out of scope

New features, Avalonia screenshots (the Avalonia cards carry their own runtime checks), pixel-diff tooling.

## Acceptance criteria

- [x] 18 screenshots committed under `docs/screenshots/m3/`, 1440 x 900, from the AOT build, no real secret visible.
- [x] `docs/screenshots/m3/README.md` lists every screen with deviations, classification and follow-up proposals.
- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] No Yaapic or stub-server process left running.

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

`docs(winui): add M3 screenshots and mockup comparison`
