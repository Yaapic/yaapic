# M4-81: WinUI visual verification of the milestone 4 views

Status: todo
Platform: windows
Depends on: M4-62, M4-63, M4-64, M4-65, M4-66, M4-67, M4-68, M4-69

## Goal

Capture the new WinUI screens in dark and light from the AOT build, commit the screenshots and record how they differ from the design, as M3-68 did for milestone 3.

## Design decisions

- Same method as M3-68: `tools/screenshots/Capture-WinUI.ps1` (extend the seeded workspace and the stub server only as far as needed: a token endpoint, a delayed response, a response that changes
  between sends; **no real secret or certificate**: generate throwaway ones at capture time and delete them), 1440 x 900, Native AOT publish output, PNG under `docs/screenshots/m4/` as
  `<screen>-<theme>.png`, each under 500 KB.

  | Screen | Compare with |
  |---|---|
  | `auth-oauth2` (Auth tab, client credentials, token valid) | `MainWindow.dc.html` (request tabs), `DSComposants2.dc.html` (fields) |
  | `auth-oauth2-code` (authorization code grant, redirect and hints) | `DSComposants2.dc.html` |
  | `auth-jwt` (JWT RS256 fields) | `DSSyntaxeIcones.dc.html` (monospace fields) |
  | `auth-aws` | `DSComposants2.dc.html` |
  | `response-timing` (waterfall, new connection) | `MainWindow.dc.html` (response tabs) |
  | `response-diff` (changed body, folds, headers section) | `DSComposants2.dc.html` (list rows) |
  | `code-generation` (Python selected) | `DSComposants2.dc.html` (dialog) |
  | `certificates` (list and editor, vault locked line) | `EnvVars.dc.html` (master-detail) |
  | `jwt-decoder` (expired token) | `DSComposants2.dc.html` (dialog) |
  | `settings-http-version` | `MainWindow.dc.html` (settings pane) |

  If a mockup does not exist for a screen, say so and compare with the closest component sheet.
- Fix the **small** visual defects found (alignment, spacing, a literal colour) in the same card; record bigger ones as follow-up cards in the Notes and in the index as `todo` rows only if the user
  asks. Commit the PNGs and a `docs/screenshots/m4/README.md` listing each screenshot with its differences from the design.

## Out of scope

Avalonia screenshots, redesigns.

## Acceptance criteria

- [ ] Twenty PNGs (ten screens, two themes) under `docs/screenshots/m4/`, each under 500 KB, captured from the Native AOT build; the capture script changes are committed.
- [ ] `docs/screenshots/m4/README.md` exists and lists the differences.
- [ ] No process left running (the published exe and the stub server are killed); 0 warnings, all tests green.

## Suggested commit message

`docs: add milestone 4 WinUI screenshots and visual review`
