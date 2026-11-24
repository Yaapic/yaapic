# M5-68: WinUI visual verification of the milestone 5 views

Status: done
Platform: windows
Depends on: M5-56, M5-57, M5-58, M5-59

## Goal

Capture the new WinUI screens in dark and light from the Native AOT build, commit the screenshots and record how they differ from the design, as M4-81 did for milestone 4.

## Design decisions

- Same method as M4-81: `tools/screenshots/Capture-WinUI.ps1` (extend the seeded workspace and the scripted flow only as far as needed; **no real secret**: sample files are generated at capture time under a temporary directory and deleted afterwards), 1440 x 900, Native AOT publish output, PNG under `docs/screenshots/m5/` as `<screen>-<theme>.png`, each under 500 KB.

  | Screen | Compare with |
  |---|---|
  | `import-empty` (dialog just opened) | `DSComposants2.dc.html` (dialog) |
  | `import-postman-report` (a Postman import with warnings and a script review) | `DSComposants2.dc.html` (list rows) |
  | `import-har` (static resources option visible, report with aggregated notes) | `DSComposants2.dc.html` |
  | `import-error` (unrecognized text, format `ComboBox` open) | `DSComposants2.dc.html` (fields, error line) |
  | `export-collection` (Postman format, saved line and report) | `DSComposants2.dc.html` |
  | `export-environments` (environment list and globals box) | `EnvVars.dc.html` (master-detail) |
  | `sidebar-menu` (the `+` flyout with Import) | `MainWindow.dc.html` (sidebar) |
  | `folder-menu` (context menu with Export) | `MainWindow.dc.html` |

  If a mockup does not exist for a screen, say so and compare with the closest component sheet.
- Fix the **small** visual defects found (alignment, spacing, a literal colour, a dialog clipped by `ContentDialogMaxWidth`, a list that crashes under AOT) in the same card; record bigger ones as follow-up notes. Commit the PNGs and a `docs/screenshots/m5/README.md` listing each screenshot with its differences from the design.

## Out of scope

Avalonia screenshots, redesigns.

## Acceptance criteria

- [x] Sixteen PNGs (eight screens, two themes) under `docs/screenshots/m5/`, each under 500 KB, captured from the Native AOT build; the capture script changes are committed.
- [x] `docs/screenshots/m5/README.md` exists and lists the differences.
- [x] No process left running (the published exe is killed); 0 warnings, all tests green.

## Suggested commit message

`docs: add milestone 5 WinUI screenshots and visual review`
