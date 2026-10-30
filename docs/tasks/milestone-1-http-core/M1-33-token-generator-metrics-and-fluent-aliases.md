# M1-33: Token generator: metrics, colour resources and Fluent aliases

Status: done
Platform: any
Depends on: M0-04

## Goal

Make the generated dictionaries carry everything the WinUI restyle needs, so hand-written styles never contain a
literal colour or size: spacing, radii, type ramp and layout sizes, a `Color` resource per colour token, and WinUI
Fluent resource keys aliased to Yaapic colours so stock controls pick up the Yaapic palette.

## Context

Today the WinUI app merges `XamlControlsResources` plus the generated `Yaapic.Colors.WinUI.xaml`. Only
`SystemAccentColor` is overridden, so Fluent controls keep Fluent greys and the Fluent accent ramp
(`AccentFillColorDefaultBrush` is derived from `SystemAccentColorLight2` in dark theme, not from Ion `#1FC7D4`).
`design/mockups/DSTokens.dc.html` gives the intended mapping (for example `color.text.primary` ->
`TextFillColorPrimaryBrush`, `color.accent.fill` -> `AccentFillColorDefaultBrush` overridden).

## Design decisions

- Files: `design/tokens/yaapic.tokens.json`, `src/Yaapic.Design.Tokens/TokenGenerator.cs`,
  `tests/Yaapic.Design.Tokens.Tests/TokenGeneratorTests.cs`, regenerated `design/xaml/Yaapic.Colors.WinUI.xaml` and
  `design/xaml/Yaapic.Colors.Avalonia.axaml`. ADR 0006 still holds: generated files are never edited by hand.
- **Fluent aliases.** A colour token may list WinUI resource keys in a new optional array
  `$extensions["dev.yaapic.xaml"].winuiAliases`. For each alias the WinUI generator emits one more
  `<SolidColorBrush x:Key="{alias}" Color="..."/>` with the token's colour, right after the token's own brush, in the
  `Default` and `Light` theme dictionaries only. The `HighContrast` dictionary gets no alias, so the OS system colours
  stay in charge there (DSTokens rule). Avalonia ignores `winuiAliases`. Initial aliases (later cards add
  control-specific keys the same way):

  | Token | WinUI aliases |
  |---|---|
  | `color.surface.app` | `SolidBackgroundFillColorBaseBrush` |
  | `color.surface.base` | `LayerFillColorDefaultBrush` |
  | `color.surface.raised` | `ControlFillColorDefaultBrush`, `CardBackgroundFillColorDefaultBrush` |
  | `color.surface.sunken` | `ControlFillColorInputActiveBrush` |
  | `color.border.subtle` | `DividerStrokeColorDefaultBrush`, `CardStrokeColorDefaultBrush` |
  | `color.border.default` | `ControlStrokeColorDefaultBrush`, `SurfaceStrokeColorDefaultBrush` |
  | `color.border.control` | `ControlStrongStrokeColorDefaultBrush` |
  | `color.text.primary` / `secondary` / `tertiary` / `disabled` | `TextFillColorPrimaryBrush` / `TextFillColorSecondaryBrush` / `TextFillColorTertiaryBrush` / `TextFillColorDisabledBrush` |
  | `color.accent.fill` / `fillHover` / `fillPressed` | `AccentFillColorDefaultBrush` / `AccentFillColorSecondaryBrush` / `AccentFillColorTertiaryBrush` |
  | `color.accent.on` | `TextOnAccentFillColorPrimaryBrush` |
  | `color.accent.text` | `AccentTextFillColorPrimaryBrush` |
  | `color.focus.ring` | `FocusStrokeColorOuterBrush` |
  | `color.info` / `success` / `warning` / `danger` | `SystemFillColorAttentionBrush` / `SystemFillColorSuccessBrush` / `SystemFillColorCautionBrush` / `SystemFillColorCriticalBrush` |
  | `color.info.background` / `success.background` / `warning.background` / `danger.background` | `SystemFillColorAttentionBackgroundBrush` / `SystemFillColorSuccessBackgroundBrush` / `SystemFillColorCautionBackgroundBrush` / `SystemFillColorCriticalBackgroundBrush` |

- **Colour resources.** For every colour token both generators also emit `<Color x:Key="{Key}Color">` next to the
  brush in every theme dictionary that has the brush, where `{Key}` is the brush key without the `Brush` suffix
  (`YaapicDangerBrush` -> `YaapicDangerColor`). Hand-written styles use them for tinted brushes
  (`<SolidColorBrush Color="{ThemeResource YaapicDangerColor}" Opacity="0.3"/>`).
- **Metrics** (both generators, after the existing metrics, `x:Double` unless stated, values from the token file):
  - `space.*` -> `YaapicSpace50` ... `YaapicSpace1200` (one per token, key suffix = token name).
  - `radius.xs|sm|md|lg|xl|full` -> `CornerRadius` `YaapicRadiusXs` ... `YaapicRadiusXl`, `YaapicRadiusFull`. WinUI also gets
    `YaapicRadiusControl` with the `windows` override (4), like `ControlCornerRadius`.
  - `type.*` -> `YaapicFontSize{Name}` and `YaapicLineHeight{Name}` with `{Name}` in PascalCase from the token path
    (`type.title.lg` -> `TitleLg`, `type.body.strong` -> `BodyStrong`, `type.code.sm` -> `CodeSm`). Keep the existing
    `YaapicBodyFontSize` and `YaapicCodeFontSize` keys (already used by the app).
  - `size.control.compact|spacious` -> `YaapicControlHeightCompact`, `YaapicControlHeightSpacious`;
    `size.row.compact|spacious` -> `YaapicRowHeightCompact`, `YaapicRowHeightSpacious` (existing `YaapicControlHeight`
    and `YaapicRowHeight` unchanged); `size.titlebar.windows` -> `YaapicTitleBarHeight` (WinUI only);
    `size.rail` -> `YaapicRailWidth`; `size.sidebar.default|min|max` -> `YaapicSidebarWidth`, `YaapicSidebarMinWidth`,
    `YaapicSidebarMaxWidth`; `size.statusbar` -> `YaapicStatusBarHeight`; `size.tabbar` -> `YaapicTabBarHeight`.
  - `border.width.default|focus` -> `Thickness` `YaapicBorderThickness` (1) and `YaapicFocusBorderThickness` (2).
- **Snapshot refresh.** Add an opt-in test path: when the environment variable `YAAPIC_UPDATE_SNAPSHOTS=1` is set, the
  two reference tests write the generator output to the reference files (LF line endings) instead of asserting, then
  pass. Without the variable they assert as today. Document the command in `design/README.md` (one English
  paragraph, "Regenerating the XAML dictionaries"). Later cards use it after adding aliases.
- The generator stays `JsonDocument`-only (no reflection, AOT safe). Keep generated output deterministic (file order).

## Out of scope

Hand-written control styles (M1-35 onward), motion and elevation resources for WinUI, any change to the App projects,
translating the existing French text of `design/README.md` (only append the English paragraph).

## Acceptance criteria

- [x] New tests in `TokenGeneratorTests`: a token with `winuiAliases` emits the alias brush in `Default` and `Light`
      but not in `HighContrast`; Avalonia output has no alias; `YaapicDangerColor` exists for each theme;
      `YaapicSpace300` is `12`; `YaapicRadiusControl` is `4` on WinUI; `YaapicFontSizeTitle` is `20` and
      `YaapicLineHeightTitle` is `28`; `YaapicSidebarWidth` is `272`.
- [x] Reference files regenerated with `YAAPIC_UPDATE_SNAPSHOTS=1` and the snapshot tests green without it.
- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; `dotnet test --solution Yaapic.sln -c Release` green.
- [x] On Windows: WinUI AOT publish 0 IL2xxx/IL3xxx warnings and the published exe starts and exits cleanly (the
      dictionary is merged by the app, so a bad key would crash at startup).

## Suggested commit message

`feat(design): generate metrics, colour resources and Fluent aliases`
