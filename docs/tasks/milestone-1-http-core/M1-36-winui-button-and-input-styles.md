# M1-36: WinUI control styles: buttons and text inputs

Status: todo
Platform: windows
Depends on: M1-35

## Goal

Give WinUI buttons, text boxes, combo boxes, check boxes, radio buttons and focus visuals the Yaapic look from
`design/mockups/DSComposants1.dc.html` (sections "button states", "text fields", "toggle / checkbox / radio").

## Design decisions

- New hand-written dictionary `src/Yaapic.App.WinUI/Themes/Yaapic.Controls.WinUI.xaml` (`Page`, merged in `App.xaml`
  last). It contains styles, templates and metrics only, never a literal colour: colours come from generated keys.
- **Colour keys go through the generator** (M1-33 mechanism): add these `winuiAliases` in
  `design/tokens/yaapic.tokens.json` and regenerate, so the keys are concrete brushes in `Default`/`Light` and absent in
  `HighContrast`:

  | Token | Aliases |
  |---|---|
  | `color.surface.raised` | `ButtonBackground` |
  | `color.surface.elevated` | `ButtonBackgroundPointerOver` |
  | `color.surface.base` | `ButtonBackgroundPressed` |
  | `color.border.default` | `ButtonBorderBrush`, `ButtonBorderBrushPressed` |
  | `color.border.control` | `ButtonBorderBrushPointerOver`, `TextControlBorderBrush`, `TextControlElevationBorderBrush`, `ComboBoxBorderBrush`, `CheckBoxCheckBackgroundStrokeUnchecked`, `RadioButtonOuterEllipseStroke` |
  | `color.text.primary` | `ButtonForeground`, `ButtonForegroundPointerOver`, `TextControlForeground`, `TextControlForegroundFocused` |
  | `color.text.secondary` | `ButtonForegroundPressed` |
  | `color.surface.sunken` | `TextControlBackground`, `TextControlBackgroundFocused`, `ComboBoxBackground` |
  | `color.surface.sidebar` | `TextControlBackgroundPointerOver`, `ComboBoxBackgroundPointerOver` |
  | `color.text.tertiary` | `TextControlBorderBrushPointerOver`, `ComboBoxBorderBrushPointerOver`, `TextControlPlaceholderForeground`, `TextControlPlaceholderForegroundFocused` |
  | `color.focus.ring` | `TextControlBorderBrushFocused`, `ComboBoxBorderBrushFocused`, `FocusVisualPrimaryBrush` |
  | `color.surface.app` | `FocusVisualSecondaryBrush` |
  | `color.text.disabled` | `ButtonForegroundDisabled`, `TextControlForegroundDisabled` |

  Accent, check box and radio fills already follow `AccentFillColor*` and `TextOnAccentFillColorPrimaryBrush` (M1-33).
  If a key has no visible effect, look up the exact key in the WinAppSDK `generic.xaml` (NuGet cache,
  `microsoft.windowsappsdk/<version>/.../Microsoft.UI/Themes/generic.xaml`) and use the key the template really uses.
- **Metrics** in `Yaapic.Controls.WinUI.xaml` (default, not theme): `ButtonPadding` = `14,0,14,0`; implicit
  `Button` style `BasedOn="{StaticResource DefaultButtonStyle}"` with `MinHeight="{StaticResource YaapicControlHeight}"`,
  `FontWeight="SemiBold"`, `CornerRadius="{StaticResource YaapicRadiusControl}"`; `TextControlThemeMinHeight` = 32;
  `TextControlBorderThemeThicknessFocused` = `1,1,1,2` (focus = accent border plus 2 px bottom, mockup "Focus" field);
  implicit `ComboBox` style with `MinHeight` 32 and `CornerRadius` control.
- **Button variants** (`x:Key` styles, mockup rows "Primaire", "Secondaire", "Discret", "Danger", "Icône"):
  - Primary: redefine `AccentButtonStyle` `BasedOn` the Fluent one with `FontWeight` SemiBold and `MinHeight` 32
    (colours come from the aliases). One primary button per view (DSTokens rule).
  - Secondary: the implicit default style above.
  - Tinted states get **new colour tokens** (with `light` and `highContrast` values, XAML keys
    `Yaapic{Name}Brush`), not `Opacity` tricks, so every theme dictionary stays generated:
    `color.overlay.hover` (dark `#FFFFFF0F`, light `#14181E0D`, HC `#00000000`),
    `color.overlay.pressed` (dark `#FFFFFF08`, light `#14181E08`, HC `#00000000`),
    `color.danger.border` (dark `#FF6B6B80`, light `#C42B2B80`, HC = HC value of `color.danger`),
    `color.danger.backgroundHover` (dark `#FF6B6B33`, light `#F8DADA`, HC = HC value of `color.danger.background`),
    `color.danger.backgroundPressed` (dark `#FF6B6B47`, light `#F3C9C9`, same HC).
  - `YaapicSubtleButtonStyle`: transparent background and border; hover `YaapicOverlayHoverBrush`, pressed
    `YaapicOverlayPressedBrush`; foreground primary, pressed secondary.
  - `YaapicDangerButtonStyle`: background `YaapicDangerBackgroundBrush`, foreground `YaapicDangerBrush`, border
    `YaapicDangerBorderBrush`; hover `YaapicDangerBackgroundHoverBrush` with border `YaapicDangerBrush`; pressed
    `YaapicDangerBackgroundPressedBrush`.
  - `YaapicIconButtonStyle` `BasedOn` subtle: 28 x 28, padding 0, foreground secondary (hover primary); content is a
    16 px `PathIcon` whose `Data` is a `Yi*` geometry string from `Yaapic.Icons.WinUI.xaml`.
  - Subtle and danger need their own `ControlTemplate`: copy the template of `DefaultButtonStyle` from the WinAppSDK
    `generic.xaml` once into the dictionary and change only the brushes used by the visual states. Keep
    `ContentPresenter`, `BackgroundSizing`, focus visual and `AutomationProperties` behaviour unchanged.
- **Disabled** buttons: text disabled, transparent background, subtle border (mockup "Désactivé").
- No change to view layouts in this card, except replacing the "Manage environments" button and the key/value remove
  button (`Segoe Fluent Icons` glyphs) with `YaapicIconButtonStyle` + `PathIcon` (`YiEnvironment` and a cross
  geometry added to `Yaapic.Controls.WinUI.xaml` as `<x:String x:Key="YaapicGlyphClose">M6 6l12 12M18 6 6 18</x:String>`),
  each keeping its `AutomationProperties.Name` and tooltip.

## Out of scope

Lists, tabs, chips, InfoBar, dialogs (M1-37), layout of screens (M1-38 onward), toggle switch and slider (not used in
M1), loading state inside buttons.

## Acceptance criteria

- [ ] `Yaapic.Controls.WinUI.xaml` contains no `#` colour literal (search `Color="#` and `="#` in `Themes/`).
- [ ] Snapshot tests green after regeneration; `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green.
- [ ] AOT publish 0 IL2xxx/IL3xxx warnings; published exe starts and exits cleanly.
- [ ] Screenshots (M1-34 script) dark and light inspected: Send button Ion `#1FC7D4` with dark text `#04252A`, text
      boxes on the sunken surface with a `border.control` border, 32 px controls, keyboard focus ring visible on Tab
      (check by pressing Tab before capture if needed). Note deviations in the card.

## Suggested commit message

`feat(winui): add Yaapic button and text input styles`
