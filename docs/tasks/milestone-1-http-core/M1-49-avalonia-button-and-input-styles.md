# M1-49: Avalonia control styles: buttons and inputs

Status: done
Platform: unix
Depends on: M1-48

## Goal

Give Avalonia buttons, text boxes, combo boxes, check boxes, radio buttons, switches and focus visuals the Yaapic look
from `design/mockups/DSComposants1.dc.html` ("button states", "text fields", "toggle / checkbox / radio"). Equivalent
of M1-36 (read it first for the exact metrics and colours).

## Design decisions

- New file `src/Yaapic.App.Avalonia/Styles/Yaapic.Controls.axaml` (`Styles`, included in `App.axaml` after the
  typography file). It contains styles, templates and metrics only, never a colour literal: colours come from the
  generated keys and the aliases of M1-48. If a control still shows a Fluent colour, add the missing alias (M1-48
  mechanism) instead of a literal.
- **Metrics**: control height `YaapicControlHeight` (32, `MinHeight`), corner radius `YaapicRadiusControl`, `Button`
  padding `14,0`, `FontWeight` SemiBold; `TextBox`, `ComboBox` and `NumericUpDown` with `MinHeight` 32 and the
  sunken surface, 1 px `YaapicBorderControlBrush` border; focus = 1 px `YaapicFocusRingBrush` border plus a 2 px bottom
  border (mockup "Focus" field). Remove Fluent's focus adorner double ring and use one keyboard focus ring in
  `YaapicFocusRingBrush` on `:focus-visible` states (buttons, tabs, list rows, check boxes).
- **Button variants** as classes (mockup rows "Primaire", "Secondaire", "Discret", "Danger", "Icône"):
  `Button.accent` (Fluent's own class: restyle to `YaapicAccentFill*` with `YaapicAccentOnBrush` text, only one per
  view), default = secondary, `Button.subtle` (transparent, hover `YaapicOverlayHoverBrush`, pressed
  `YaapicOverlayPressedBrush`), `Button.danger` (`YaapicDangerBackgroundBrush` with `YaapicDangerBrush` text and
  `YaapicDangerBorderBrush` border; hover and pressed use the `YaapicDangerBackground*` brushes), `Button.icon` (based
  on subtle: 28 x 28, padding 0, secondary foreground, primary on hover; content is a 16 px `PathIcon` bound to a
  `Yi*` geometry of `Yaapic.Icons.Avalonia.axaml`). Disabled: `YaapicTextDisabledBrush` text, transparent background,
  subtle border. Use `^` template parts and `/template/` selectors rather than copying the whole control template,
  unless a state cannot be reached; if a template copy is needed, copy it once from the pinned Fluent theme and change
  only the brushes.
- **Selection controls**: `CheckBox` 14 px square, radius 3, accent when checked with the on-accent tick; `RadioButton`
  with a 10 px dot inside a 3 px accent ring when checked (the WinUI version had to give up on this, see
  `docs/screenshots/m1/README.md` F2: here do it in the template); `ToggleSwitch` with accent track when on.
- **Combo box**: closed state as `TextBox`; drop-down popup on `YaapicSurfaceOverlayBrush` with a 1 px
  `YaapicBorderOverlayBrush` border, radius 6, items 28 px high with hover `YaapicOverlayHoverBrush` and selected
  `YaapicSelectionBackgroundBrush`. The same popup style applies to `ContextMenu`, `MenuFlyout` and `ToolTip`
  (surface, border, radius, text style `bodySmall`).
- Replace the unicode `✕` and `+` glyph buttons (`MainWindow.axaml`, `KeyValueEditorView.axaml`) and the text
  buttons "Environments" and "Theme" with `Button.icon` + `PathIcon` (add a close and a plus geometry as resources
  `YaapicGlyphClose` = `M6 6l12 12M18 6 6 18` and `YaapicGlyphAdd` = `M12 5v14M5 12h14` in the controls file),
  each keeping an `AutomationProperties.Name` and a tooltip. Layout is otherwise unchanged (M1-51 reworks it).

## Out of scope

Lists, tabs, chips, progress, dialogs (M1-50), screen layouts (M1-51 onward), loading state inside buttons,
`DatePicker`, `Slider`.

## Acceptance criteria

- [x] No `#` colour literal in `src/Yaapic.App.Avalonia/Styles/` (`rg '="#|Color="#'`); snapshot tests green if the
      aliases changed.
- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; AOT publish 0 IL2xxx/IL3xxx warnings;
      published exe starts and exits cleanly.
- [x] Screenshots (M1-47 script, dark and light) inspected: Send in Ion `#1FC7D4` with dark text `#04252A`, text boxes
      on the sunken surface with a `border.control` border, 32 px controls, radio dots as in the mockup, the method
      combo drop-down on the overlay surface (capture it with `--open` or a manual run), keyboard focus ring visible
      after Tab. Deviations noted in the card, PNGs not committed.

## Suggested commit message

`feat(avalonia): add Yaapic button, input and selection control styles`

## Notes

- `Yaapic.Controls.axaml` added (buttons and variants, text inputs, check box, radio, toggle switch, combo box item,
  menu item and tooltip styles, single focus ring). The text box focus border and the `ControlCornerRadius` /
  `OverlayCornerRadius` keys are set in `App.axaml`. No generator change was needed: hover, selected and disabled
  states use the existing `Yaapic*` brushes through `/template/` selectors.
- The `Yi*` and `YaapicGlyph*` geometries are 24 x 24 strokes, so `Button.icon PathIcon` gets a template that strokes
  them (2 px, round caps) instead of filling them. Glyph resources are `StreamGeometry`.
- Replaced the "Environments", "Theme" text buttons and the glyph buttons (tab close, new tab, remove row) with
  `Button.icon`; Send is `Button.accent`. Added a `YaapicGlyphTheme` half-circle geometry for the theme button.
- Deviations: check box 14 px / radius 3 and radio 18 px with a 5 px accent ring (mockup values for the radio, card
  values for the check box). Combo drop-down popup and Tab focus ring were not captured: `Capture-Avalonia.ps1` has no
  key or focus options on Avalonia yet. Dark and light captures with Send checked (Ion `#1FC7D4` Send, sunken inputs
  with control border, 32 px controls, accent radio and check box).
