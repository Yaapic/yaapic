# M1-56: Avalonia environment management and prompt restyle

Status: done
Platform: unix
Depends on: M1-51, M1-53

## Goal

Make `Views/EnvironmentsWindow.axaml` look like the environment screen of `design/mockups/EnvVars.dc.html` /
`EnvVarsLight.dc.html` (environment list with dots, a header with name and actions, the variables table), staying a
separate window as in M1-31, and bring `Views/PromptWindow.axaml` in line with the dialog style. Equivalent of M1-43
(read it first for the texts and the active-dot rules).

## Context

The window already has the 240 px list column, the detail column and the `IsActive` / `IsEdited` flags on
`EnvironmentItemViewModel` (they come from the shared ViewModel, M1-43). Missing: list row styling, dots, a title-sized
name field, danger and accent buttons, the "{n} variables" caption wording and the table look.

## Design decisions

- **Window**: 960 x 560, `MinWidth` 560, `MinHeight` 320, centred on the owner, background `YaapicSurfaceBaseBrush`,
  system title "Environments". Columns: list 240 | 1 px `YaapicBorderSubtleBrush` divider | detail `*`.
- **List column** (background `YaapicSurfaceSidebarBrush`): header 40 px, padding `14,0,8,0`: "Environments" (`bodyStrong`)
  and a `Button.icon` add button (plus glyph, `AddCommand`, tooltip "New environment") instead of the "Add" text button.
  `ListBox` with `Classes="roomy"` rows (32 px, padding `14,0`, spacing 10): an 8 px `Ellipse` dot (`YaapicSuccessBrush`
  for the active environment, `YaapicTextTertiaryBrush` otherwise), the name (SemiBold when `IsEdited`), then right
  aligned the 6 px accent dirty dot or the `caption` "active". Use bindings on the existing flags and, if a brush
  choice is needed, a converter in `Presentation/` (a new `EnvironmentMap` mirroring the WinUI one, with XML docs).
- **Detail column** (`YaapicSurfaceBaseBrush`), visible when `EditedEnvironment` is not null: header 56 px, padding
  `20,0`, 1 px subtle bottom border: 10 px dot (same rule), the name as a borderless `TextBox` in title size (20/28
  SemiBold, placeholder "Environment name"), a caption "{n} variables" (`bodySmall` tertiary, n =
  `Variables.ActiveCount`), then right aligned "Delete" (`Button.danger`, keeps the confirmation flyout, whose button
  is also `Button.danger`) and "Save" (`Button.accent`, the only primary button, existing `OnSave`). The variables
  table is `KeyValueEditorView` with `KeyHeader="VARIABLE"` and margin `20,12,20,16`. Nothing selected: centred "Select
  an environment, or add one." (secondary).
- **PromptWindow** (M2-20): apply `Window.dialog` (M1-50): title-size label, the `TextBox` below, a raised command area
  with right-aligned equal-width "OK" (`Button.accent`, default) and "Cancel" buttons. No logic change.
- Make `--open prompt` (M1-47) available in the screenshot options if it is cheap (open the prompt with a sample label);
  otherwise capture it manually and say so in the Notes.

## Out of scope

Turning environments into a page, globals (M2-50), secrets, duplicate and export, environment colours, resolution
preview, the "production" marker.

## Acceptance criteria

- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual check on the published exe: add, rename, edit variables, save, delete with the flyout; the active dot
      follows the title-bar pill; the prompt returns the typed text and cancels cleanly.
- [x] Screenshots (`-Open environments` with the fixture `local` and `staging`, dark and light; the prompt) inspected
      against `EnvVars.dc.html` / `EnvVarsLight.dc.html` and `DSComposants2.dc.html` (dialog). Deviations listed in the
      card, PNGs not committed.

## Suggested commit message

`feat(avalonia): restyle the environments window and the text prompt`
