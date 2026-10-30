# M1-35: WinUI typography and bundled code font

Status: done
Platform: windows
Depends on: M1-33, M1-34

## Goal

Ship JetBrains Mono with the WinUI app and apply the Yaapic type ramp, so code text is really monospace and the UI
uses 13/20 body text like the mockups instead of Fluent's 14 px.

## Context

`YaapicMonoFont` points to `ms-appx:///Assets/Fonts/JetBrainsMono.ttf#JetBrains Mono`, but the App project has no
`Assets/Fonts` folder: every "code" text (URL, headers, bodies, method labels) silently falls back to Segoe UI. The
mockups (`design/mockups/DSFondations.dc.html`, type ramp) use Segoe UI Variable Text for UI and JetBrains Mono for
every technical value (DSTokens rule: "URL, headers, JSON always monospace").

## Design decisions

- **Font file.** Download the JetBrains Mono 2.304 release from the official repository
  (`https://github.com/JetBrains/JetBrainsMono/releases/download/v2.304/JetBrainsMono-2.304.zip`, with `pwsh`
  `Invoke-WebRequest`), take `fonts/variable/JetBrainsMono[wght].ttf`, commit it as
  `src/Yaapic.App.WinUI/Assets/Fonts/JetBrainsMono.ttf` with the licence as `src/Yaapic.App.WinUI/Assets/Fonts/OFL.txt`.
  Add `<Content Include="Assets\Fonts\*.ttf" CopyToOutputDirectory="PreserveNewest" />` to the csproj. Verify the
  `ms-appx` URI resolves in the unpackaged AOT build (monospace digits, slashed zero). If the variable font does not
  honour weights 600/700, keep it (synthesised bold is acceptable for M1) and write that in the card's Notes.
- `docs/dependencies.md`: new section "Bundled assets" with a table row: JetBrains Mono, 2.304, SIL OFL 1.1,
  `Yaapic.App.WinUI`, source URL, justification (code font of the design system, `type.code`).
- **Typography dictionary** `src/Yaapic.App.WinUI/Themes/Yaapic.Typography.WinUI.xaml` (hand-written `Page`, merged
  in `App.xaml` after `Yaapic.Icons.WinUI.xaml`). Only generated keys for sizes, line heights and brushes (M1-33);
  font weights are written literally (XAML has no weight resource) and must match the token file. Styles
  (`TargetType="TextBlock"`, `LineStackingStrategy="BlockLineHeight"`):

  | Key | Size / line height | Weight | Font | Foreground |
  |---|---|---|---|---|
  | `YaapicTitleTextStyle` | `YaapicFontSizeTitle` / `YaapicLineHeightTitle` (20/28) | SemiBold | UI | primary |
  | `YaapicSubtitleTextStyle` | Subtitle (15/20) | SemiBold | UI | primary |
  | `YaapicBodyTextStyle` | Body (13/20) | Normal | UI | primary |
  | `YaapicBodyStrongTextStyle` | BodyStrong (13/20) | SemiBold | UI | primary |
  | `YaapicBodySmallTextStyle` | BodySm (12/16) | Normal | UI | secondary |
  | `YaapicCaptionTextStyle` | Caption (11/16) | Medium | UI | tertiary |
  | `YaapicCodeTextStyle` | Code (12/20) | Normal | `YaapicMonoFont` | primary |
  | `YaapicCodeSmallTextStyle` | CodeSm (11/16) | Medium | `YaapicMonoFont` | secondary |
  | `YaapicMethodTextStyle` | 10 / 16 | Bold | `YaapicMonoFont` | set by caller (method brush) |
  | `YaapicBadgeTextStyle` | 10 / 16 | SemiBold | `YaapicMonoFont` | tertiary |

  The 10 px method/badge size has no token: add `type.label.mono` (`10px` / `16px` / 700, mono) to the token file and
  regenerate (M1-33 snapshot command) instead of writing `10` by hand.
- **Fluent overrides** in the same dictionary: `ControlContentThemeFontSize` = `YaapicBodyFontSize` (13),
  `ContentControlFontSize` = 13, and `BodyTextBlockStyle` redefined `BasedOn` the Fluent one with 13/20, as listed in
  DSTokens ("BodyTextBlockStyle (13/20 surchargé)").
- Replace the hard-coded `FontSize="20"` of the request name in `Views/RequestEditorView.xaml` with
  `{StaticResource YaapicFontSizeTitle}`, and give the plain `TextBlock`s of the existing views the matching style
  (method labels -> `YaapicMethodTextStyle`, secondary hints -> `YaapicBodySmallTextStyle`). No layout change.

## Out of scope

Control templates and colours (M1-36, M1-37), layout changes (M1-38 onward), Segoe Fluent Icons replacement.

## Acceptance criteria

- [x] `rg 'FontSize="[0-9]' src/Yaapic.App.WinUI` finds nothing.
- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green.
- [x] AOT publish 0 IL2xxx/IL3xxx warnings; published exe starts and exits cleanly.
- [x] `tools/screenshots/Capture-WinUI.ps1` dark and light screenshots show the URL and body in JetBrains Mono and
      13 px UI text (inspected, not committed); result noted in the card.

## Suggested commit message

`feat(winui): bundle JetBrains Mono and add the Yaapic type ramp`

## Notes

- Variable font `JetBrainsMono[wght].ttf` shipped as `Assets/Fonts/JetBrainsMono.ttf`; the AOT build resolves the
  `ms-appx` URI (monospace glyphs, slashed zero visible in the dark capture). Weight 600/700 rendering was not checked
  separately; synthesised bold is acceptable for M1.
- Token `type.label.mono` added (10/16/700); the generator exposes it as `YaapicFontSizeLabelMono` and
  `YaapicLineHeightLabelMono`. XAML dictionaries regenerated with `YAAPIC_UPDATE_SNAPSHOTS=1`.
- Dark capture with `-Send` (Native AOT exe): URL, key/value fields, method labels and response body are monospace,
  UI text is 13 px; the successful response rendered without crashing. Light theme capture was taken the same way.
- Only a few existing views received a style (method labels, hints, title); the remaining plain `TextBlock`s are
  restyled by the layout cards that follow.
