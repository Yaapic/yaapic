# M1-48: Avalonia typography, fonts and Fluent resource aliases

Status: done
Platform: unix
Depends on: M1-33, M1-47

## Goal

Apply the Yaapic type ramp and fonts to the Avalonia app, and make the Fluent theme read the Yaapic colours, so the
next cards only have to write styles. Equivalent of M1-35 plus the alias part of M1-33 and M1-36 (read them first).

## Context

`App.axaml` only loads `FluentTheme` and the generated colours and icons. UI text uses the Avalonia default font at
14 px; code text uses `FontFamily="monospace"` in the sidebar and `YaapicMonoFont` elsewhere; no style exists for any
text role. The token generator writes `winuiAliases` only: the Avalonia output has no Fluent aliases, so Fluent
controls keep their own greys and the system accent colour. JetBrains Mono is already linked from the WinUI asset
(`Yaapic.App.Avalonia.csproj`) and `YaapicMonoFont` is overridden in `App.axaml`.

## Design decisions

- **Generator**: add an optional `avaloniaAliases` array next to `winuiAliases` in `design/tokens/yaapic.tokens.json`
  and teach `TokenGenerator` to emit, for the Avalonia platform, the alias brushes in the `Dark` and `Light` theme
  dictionaries (same rule as WinUI: concrete value, absent from high contrast). Regenerate with the M1-33 snapshot
  command and keep the snapshot tests green. Aliases to add (Fluent key names; check each in the pinned
  `Avalonia.Themes.Fluent` sources and use the key the control template really reads): `SystemAccentColor` and its
  `Light1..3` / `Dark1..3` variants from `color.accent.fill*`, `TextControlBackground*`, `TextControlBorderBrush*`,
  `TextControlForeground*`, `TextControlPlaceholderForeground`, `ButtonBackground*`, `ButtonForeground*`,
  `ButtonBorderBrush*`, `ComboBoxBackground*`, `ComboBoxBorderBrush*`, `CheckBoxCheckBackground*`,
  `RadioButtonOuterEllipse*`, `ListBoxItemBackground*`, `TabItemHeader*`, `MenuFlyoutPresenterBackground`,
  `ToolTipBackground`, `ProgressBar*`, following the token mapping that M1-36, M1-37 and M1-39 list for WinUI. The
  colour tokens those cards added already exist; add only aliases, plus a token if a control needs a colour that has none.
- **Fonts**: `YaapicUiFont` (Segoe UI Variable Text, then `Inter`, `Segoe UI`, `SF Pro Text`, `system-ui`) as a
  `FontFamily` resource in `App.axaml`, applied to `Window` and `TextBlock` through a global style, with `FontSize` =
  `YaapicFontSizeBody` (13). `YaapicMonoFont` keeps its override. Check in the AOT build that the bundled JetBrains
  Mono renders (monospace digits, slashed zero); record weight 600/700 behaviour in the Notes. In
  `docs/dependencies.md` add `Yaapic.App.Avalonia` to the JetBrains Mono row.
- **Typography styles** in `src/Yaapic.App.Avalonia/Styles/Yaapic.Typography.axaml` (`Styles`, included in `App.axaml`
  after `FluentTheme`): classes `TextBlock.title`, `.subtitle`, `.body`, `.bodyStrong`, `.bodySmall`, `.caption`,
  `.code`, `.codeSmall`, `.method`, `.badge`, with the sizes, line heights, weights, fonts and foregrounds of the table
  in M1-35 (sizes and line heights from the generated `YaapicFontSize*` and `YaapicLineHeight*` keys, `.method` and
  `.badge` from `YaapicFontSizeLabelMono`). `SelectableTextBlock` gets the same classes.
- Replace the hard-coded `FontSize`, `FontFamily="monospace"` and `FontWeight` in the existing views with the classes
  (method labels -> `method`, hints -> `bodySmall`, request name -> `bodyStrong`). No layout change.

## Out of scope

Control templates and colours beyond what the aliases change (M1-49, M1-50), layout changes (M1-51 onward).

## Acceptance criteria

- [x] `rg 'FontSize="[0-9]|FontFamily="monospace"' src/Yaapic.App.Avalonia` finds nothing.
- [x] Snapshot tests green after regeneration; `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green.
- [x] AOT publish 0 IL2xxx/IL3xxx warnings; published exe starts and exits cleanly.
- [x] Screenshots (M1-47 script) dark and light inspected: 13 px UI text, URL, header and body text in JetBrains Mono,
      accent `#1FC7D4` (not the system accent) wherever the accent is used. Not committed; result noted in the card.

## Suggested commit message

`feat(avalonia): add the Yaapic type ramp, fonts and Fluent colour aliases`

## Notes

- `avaloniaAliases` added to 23 colour tokens; the generator also emits `SystemAccentColor` and `SystemAccentColorLight1..3` /
  `Dark1..3` (hover colour for the lighter shades, pressed colour for the darker ones: the palette has no more).
  Key names are Fluent's own (same family as WinUI); `TabItemHeader*Unselected/Selected` and `ListBoxItem*` follow the
  Avalonia templates. `ControlContentThemeFontSize` (13) and `ContentControlThemeFontFamily` are overridden in `App.axaml`.
- Typography classes use `:is(TextBlock)`, so `SelectableTextBlock` gets them too. TextBoxes keep their explicit
  `YaapicMonoFont` / `YaapicFontSizeCode*` attributes (classes only target text blocks); the request-name TextBox keeps
  `FontWeight="SemiBold"`, to be replaced when M1-49 styles inputs.
- AOT capture (win-x64, dark and light, `-Send`): 13 px UI text, URL, key/value placeholders, body and headers in JetBrains Mono
  (slashed zero visible), method labels 10 px bold mono, accent `#1FC7D4` on the checkbox and tab underline, status chip.
  Remaining Fluent blue: the tab close button outline in light theme (templates, M1-49/M1-50). Weight 600/700 of the variable
  font was not checked separately. No "before" capture was taken (the previous state was not rebuilt).
- Published exe starts, renders and exits cleanly; ILC prints only third-party `Avalonia.Win32` `UnmanagedCallersOnly` notes, no IL2xxx/IL3xxx.

