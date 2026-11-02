# M2-67: WinUI visual verification of the milestone 2 views

Status: todo
Platform: windows
Depends on: M2-18, M2-19, M2-37, M2-47, M2-48, M2-49, M2-58, M2-64

## Goal

Capture the new WinUI screens in dark and light from the AOT build, commit the screenshots and record how they differ
from the mockups, as M1-45 did for milestone 1.

## Design decisions

- Same method as M1-45: `tools/screenshots/Capture-WinUI.ps1` (extend its seeded workspace and stub server only as far
  as needed: a collection with a nested folder and requests, a globals file, two environments, a few history entries),
  1440 x 900, Native AOT publish output, PNG under `docs/screenshots/m2/` as `<screen>-<theme>.png`, each under 500 KB.

  | Screen | Compare with |
  |---|---|
  | `sidebar-tree` (tree expanded, one folder collapsed) | `MainWindow.dc.html` (sidebar) |
  | `tree-menu` (context menu open on a folder) | `DSComposants2.dc.html` (menus, list rows) |
  | `sidebar-history` | `MainWindow.dc.html` (rail, sidebar), `DSComposants2.dc.html` (list rows) |
  | `editor-methods` (method picker open) | `DSComposants1.dc.html` (method picker) |
  | `editor-body-formdata` | `DSComposants1.dc.html` (key/value table) |
  | `editor-auth` (Bearer selected) | `DSComposants1.dc.html` (text fields) |
  | `url-hints` (resolved and unresolved chips) | `DSComposants2.dc.html` (chips) |
  | `environments-globals` | `EnvVars.dc.html` |
  | `folder-settings` | `DSComposants2.dc.html` (dialog) |
  | `palette` (query typed, results shown) | `DSFondations.dc.html`, `DSComposants2.dc.html` (no dedicated artboard) |

  Each in `dark` and `light` (20 files).
- `docs/screenshots/m2/README.md` (English): how to regenerate, then per screen the two screenshots, the mockup artboard
  and a deviation list classified `fix now`, `M2 scope` or `later milestone`, comparing surface and text colours, accent
  use, code font, control heights, chips, borders and icons, exactly like `docs/screenshots/m1/README.md`.
- One-line token, style or margin mistakes are fixed here (re-capture after the fix); anything larger goes under
  "Follow-up" with a one-line card proposal, no card written.
- Keyboard focus visibility (Tab through the new controls, the palette) is checked and noted.

## Out of scope

New features, Avalonia, pixel-diff tooling.

## Acceptance criteria

- [ ] 20 screenshots committed under `docs/screenshots/m2/`, 1440 x 900, from the AOT build.
- [ ] `docs/screenshots/m2/README.md` lists every screen with deviations and classification, plus follow-up proposals.
- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] No Yaapic or stub-server process left running.

## Suggested commit message

`docs(winui): add M2 screenshots and mockup comparison`
