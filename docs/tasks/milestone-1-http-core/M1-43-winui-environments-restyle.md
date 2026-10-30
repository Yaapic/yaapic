# M1-43: WinUI environment management restyle

Status: todo
Platform: windows
Depends on: M1-38, M1-40

## Goal

Make `Views/EnvironmentsDialog.xaml` look like the environment screen of `design/mockups/EnvVars.dc.html` /
`EnvVarsLight.dc.html` (environment list with dots, a header with name and actions, the variables table), while
staying a `ContentDialog` in M1.

## Context

The mockup shows environments as a full page reached from the rail. M1 keeps the dialog (M1-27 decision, opened from
the rail and the environment pill since M1-38); only its layout and look change. Today it is 720 x 420 with a 200 px
plain list, a `TextBox` with header "Name", the table and a Save button at the bottom.

## Design decisions

- **Size**: override `ContentDialogMaxWidth` (1000) and `ContentDialogMaxHeight` (720) in the dialog's own
  `Resources`; content grid 960 x 560, no dialog title (the content has its own header), close button "Close"
  (secondary). Columns: list 240 | 1 px `YaapicBorderSubtleBrush` divider | detail `*`.
- **List column** (background `YaapicSurfaceSidebarBrush`):
  - header 40 px, padding `14,0,8,0`: "Environments" (`YaapicBodyStrongTextStyle`) + `YaapicIconButtonStyle` add
    button (`YaapicGlyphAdd`, `AddCommand`, tooltip "New environment").
  - `ListView` with `YaapicListRowStyle` but 32 px rows, padding `14,0`, spacing 10: an 8 px dot
    (`YaapicSuccessBrush` for the active environment, `YaapicTextTertiaryBrush` otherwise), the name (SemiBold when
    selected), then right-aligned: the dirty dot (6 px `YaapicAccentFillBrush`, `AutomationProperties.Name`
    "Unsaved changes") or the caption "active" (`YaapicCaptionTextStyle`) for the active environment. "Active" is
    `item == ViewModel.ActiveEnvironment`: add a static `x:Bind` function in `Presentation/` taking both values.
- **Detail column** (background `YaapicSurfaceBaseBrush`), visible when `EditedEnvironment` is not null:
  - header 56 px, padding `20,0`, 1 px subtle bottom border: 10 px dot (same rule as the list), the name as a
    borderless `TextBox` in title size (20/28 SemiBold, placeholder "Environment name"), a caption
    "{n} variables" (`YaapicBodySmallTextStyle` tertiary, n = `Variables.ActiveCount` from M1-40), then right
    aligned: "Delete" (`YaapicDangerButtonStyle`, keeps the M1-27 confirmation flyout) and "Save" (`AccentButtonStyle`,
    the only primary button of the view, existing `OnSave`).
  - the variables table (`KeyValueEditorView`, `KeyHeader="VARIABLE"`) with margin `20,12,20,16`.
  - when nothing is selected: centred "Select an environment, or add one." (secondary).
- The delete flyout uses the M1-37 flyout look; its button becomes `YaapicDangerButtonStyle`.

## Out of scope

Turning environments into a page, globals, secrets and vault, initial versus current values, variable types,
resolution preview, duplicate/export, environment colours, the "production" red square marker, warnings about
missing variables.

## Acceptance criteria

- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green.
- [ ] AOT publish 0 IL2xxx/IL3xxx warnings; published exe starts and exits cleanly.
- [ ] Manual check on the published exe: add, rename, edit variables, save, delete with the flyout; the active dot
      follows the environment pill.
- [ ] Screenshots dark and light of the dialog with the M1-34 fixture (`local` and `staging`) inspected against
      `EnvVars.dc.html` / `EnvVarsLight.dc.html`; differences listed in the card's Notes, not committed.

## Suggested commit message

`feat(winui): restyle environment management`
