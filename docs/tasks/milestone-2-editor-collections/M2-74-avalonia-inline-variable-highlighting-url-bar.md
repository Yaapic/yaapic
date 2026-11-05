# M2-74: Avalonia inline variable highlighting and popup in the URL bar

Status: done
Platform: unix
Depends on: M2-71, M2-21, M1-57

## Goal

Same as M2-72 in `Yaapic.App.Avalonia`: `{{name}}` tokens coloured inside the URL text by state, with a hover popup
(value and scope, or "Unresolved variable" with a create form). This **supersedes M2-52** (the Avalonia chip strip, never
built); there is nothing to remove on this platform. Read M2-71 (model) and M2-72 (behaviour, texts, popup content,
keyboard access, acceptance scenarios) first: the behaviour is identical, only the control differs.

## Feasibility and approach (decided here, verify in the first hour)

Avalonia's `TextBox` cannot colour sub-ranges either. Options, in the order to try:

1. **Custom `TextPresenter` colouring** (best if possible): the `TextBox` template hosts a `TextPresenter` that builds its
   `TextLayout`. If Avalonia 12.1.4 lets a subclass override the layout creation (or exposes per-range text run
   properties), a `VariableTextPresenter` placed in a `ControlTheme` for a `VariableTextBox : TextBox` colours the
   ranges with no overlay and no scroll sync. Check the public/protected surface of `TextPresenter` first; if it is
   `internal` or sealed in practice, drop this option.
2. **Overlay (default)**: `VariableTextBox` (a `TextBox` subclass or a `UserControl` around one) whose own foreground is
   `Transparent` (keep `CaretBrush` and selection brushes visible) over a `TextBlock` with one `Run` per segment, same
   font/size/padding, `IsHitTestVisible` false. Mirror the inner `ScrollViewer` (`PART_ScrollViewer`) offset into the
   overlay (`RenderTransform`, clipped) on `ScrollChanged`. The `TextBox` background must be transparent in all
   pseudo-classes (`:pointerover`, `:focus`, `:disabled`); the surface comes from the host `Border`. Same risks and
   checks as M2-72 (a) to (e).
3. **LoomEditor.Avalonia single-line editor with decorations** (M2-70 package): robust and gives
   hit-testing (`GetPositionFromPoint`), but a full editor for a one-line bar (Enter handling, gutter off, height, theme),
   and it would make this card depend on M2-70 (which waits for M2-65 and the whole history/palette slice). **Only if (1)
   and (2) fail**; if used, add M2-70 to this card's dependencies and say so in Notes.

Hit test for the hover: with the overlay, use the `TextBox`'s text layout (`TextPresenter.TextLayout.HitTestTextPosition`
or the public char-index-to-rectangle helper available in 12.1.4) to get the rectangle of `token.Start` and `token.End`.
Record the spike outcome in Notes. The control must not draw colours that can drift from the text; if no option works,
stop and report.

## Design decisions

- **Control** `src/Yaapic.App.Avalonia/Controls/VariableTextBox.cs` (+ a `ControlTheme` in `Styles/`): styled properties
  `Text` (two-way), `Watermark` (placeholder), `Context` (`VariableContext?`, null = plain `TextBox`, no overlay work).
  No reflection; compiled bindings only (`AvaloniaUseCompiledBindingsByDefault` is on).
- **Rendering**: tokenise through `Context.Tokenize(Text)` on text change and on `Context.Changed`; segments cover the
  whole text exactly (`Run` lengths sum to `Text.Length`, braces included in the token run). Brushes from the generated
  resources of the current theme variant with `TryFindResource` + `ActualThemeVariant` (re-read on
  `ActualThemeVariantChanged`): resolved `YaapicSuccessBrush` on `YaapicSuccessBackgroundBrush`, unresolved
  `YaapicWarningBrush` on `YaapicWarningBackgroundBrush`, dynamic `YaapicInfoBrush`. Verify each key in
  `design/xaml/Yaapic.Colors.Avalonia.axaml` (both theme dictionaries) and contrast on the URL bar surface in dark and
  light; never hard-code hex.
- **Popup** `src/Yaapic.App.Avalonia/Views/VariablePopupView.axaml(.cs)` (`x:DataType="vm:VariablePopupViewModel"`) shown
  in an Avalonia `Popup` (`IsLightDismissEnabled` false, `Placement` bottom of the token rectangle, `PlacementTarget` the
  box) so it never steals focus while hovering. Timings, close rules, `Ctrl+.` keyboard access and content are exactly
  those of M2-72 (header in the token colour; resolved: value, scope badge, Edit/Save, Open; dynamic: message; unresolved:
  danger icon, "Unresolved variable", the sentence from `VariablePopupViewModel.UnresolvedMessage`, scope `ComboBox`
  with disabled entries and tooltips, value box, `Create`; `LastError` in `YaapicDangerBrush`). Use a `DispatcherTimer`
  for the hover delay.
- **Open actions**: wire `Context.OpenRequested` to the existing Avalonia environments window (M2-50; Globals tab for
  Globals) and, for `Collection`, to the folder settings window of M2-51 **if M2-51 is done**; if it is not, hide the
  Open link for the Collection scope and note it (do not block on M2-51).
- **`Views/RequestEditorView.axaml`**: replace the URL `TextBox` by `VariableTextBox` bound two-way to `Url`, with
  `Context="{Binding Variables}"`; keep placeholder, mono font, height, padding and the automation name of the current
  box (check `tools/screenshots/Capture-Avalonia.ps1` and any UI automation selector). M2-52's chip strip does not exist,
  so there is no strip to delete. The ViewModel cleanup (removing `UrlVariables`) was done by M2-72; if M2-74 runs first
  on a branch where it has not happened, do not touch it: M2-72 owns it.

## Out of scope

Header and param cells (M2-75), body (later, LoomEditor from M2-70), autocomplete, folder scope, WinUI.

## Acceptance criteria

- [x] `dotnet build Yaapic.CrossPlatform.slnf -c Release` (and `Yaapic.sln` on Windows) 0 warnings; all tests green;
      architecture tests green.
- [x] Resource keys used by the new AXAML exist in both theme dictionaries of `design/xaml/Yaapic.Colors.Avalonia.axaml`
      (listed in Notes); no hex colours in the new files.
- [x] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings (the existing Avalonia.Win32 ILC
      messages are not new).
- [x] **Exercise the PUBLISHED binary** (UI Automation, or `tools/screenshots/Capture-Avalonia.ps1` plus real input):
  - [x] `{{baseUrl}}/get?x={{nope}}` with environment `Local` active: first token resolved colour, second unresolved,
        inside the URL text; text not shifted or doubled; caret, selection and typing inside a token normal.
  - [x] Hover each token: the popup content of M2-72 (resolved details and Edit/Save; "Unresolved variable" + the exact
        sentence + create form). Create `nope` in Environment, then in Globals and Collection: token turns resolved live,
        files on disk updated.
  - [x] "No environment": `{{baseUrl}}` becomes unresolved without retyping; Environment choice disabled with its reason.
  - [x] Long URL scrolled horizontally keeps colours aligned.
  - [x] Dark and light (switch at run time) readable; `{{$guid}}` in the dynamic colour.
  - [x] `Ctrl+.` opens the popup, `Escape` closes it; hover never steals focus.
  - [x] Ctrl+Enter still sends; params sync with the URL (M2-13).
- [x] Screenshots through `Capture-Avalonia.ps1` (dark and light, `-Send`), not committed; differences from the WinUI
      look listed in Notes.
- [x] No Yaapic or stub-server process left running.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (the published binary, the stub server, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m2-editor-collections` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`feat(avalonia): colour variables inline in the URL bar with a hover popup`

## Notes

**Spike outcome (option 2, overlay, shipped).** Option 1 (custom `TextPresenter`) was dropped: the layout creation of
`TextPresenter` is not overridable in Avalonia 12.1.4 (only the read-only `TextLayout` is public). Option 3 was not
needed, so the card still depends on M2-71, M2-21 and M1-57 only. A transparent-text `TextBox` over a `TextBlock` of
`Run`s works with these details:

- `VariableTextBox` is a `UserControl` (`Controls/VariableTextBox.axaml(.cs)`) around the `bare` box: `Foreground`,
  `Background` and `BorderThickness` of the inner box are fixed (`BorderThickness` 0 keeps the content at the same offset
  in every pseudo-class, including the 2 px focus border); `CaretBrush` and `SelectionForegroundBrush`
  (`YaapicAccentOnBrush`, otherwise the selected text is invisible) stay explicit. No `ControlTheme` was needed.
- Metrics: the overlay follows the box `FontSize` (observable binding) and uses `YaapicMonoFont`; text and colours cannot
  drift because the overlay text is the box text character for character (`Run` lengths sum to `Text.Length`). The
  resolved/unresolved/dynamic highlight is the `Run.Background` (translucent), no metric change.
- Scrolling: `PART_ScrollViewer.ScrollChanged` drives a `TranslateTransform` of the overlay; the overlay sits in a
  horizontal `StackPanel` (unbounded width), otherwise the scrolled text was clipped to its slot and disappeared.
- Hit test: `TextPresenter.TextLayout.HitTestPoint` for the hover, `HitTestTextPosition` for the popup placement.
- Popup: a `Popup` (`IsLightDismissEnabled` false, `BottomEdgeAlignedLeft` on the box) so hovering never steals focus;
  closing is timer and focus driven; it stays open while the scope dropdown is open. `VariablePopupView` has a public
  parameterless constructor plus `Load` (a view with constructor arguments is not reachable by the Avalonia runtime
  loader and raises a build warning).
- `Ctrl+.` is matched on `Key.OemPeriod` like the WinUI `VK_OEM_PERIOD`; on layouts where `.` needs Shift (AZERTY) it is
  `Ctrl+Shift+;`, which does not match (same limitation as WinUI).
- **Open link:** the Collection scope has no editor on this platform yet (M2-51 is `todo`), so the popup hides `Open` for
  a Collection variable; Environment opens the environments window on the active environment, Globals opens it on
  Globals (`RequestEditorView.VariableScopeOpenRequested` to `MainWindow`).
- After a successful create the popup closes if the pointer is no longer over it (a new card is shown only when the focus
  was inside), which matches M2-72 ("closes if the pointer left").

**Resource keys used (all exist in both theme dictionaries of `design/xaml/Yaapic.Colors.Avalonia.axaml`):**
`YaapicSuccessBrush`, `YaapicSuccessBackgroundBrush`, `YaapicWarningBrush`, `YaapicWarningBackgroundBrush`,
`YaapicInfoBrush`, `YaapicInfoBackgroundBrush`, `YaapicDangerBrush`, `YaapicAccentOnBrush`, `YaapicTextPrimaryBrush`,
`YaapicTextSecondaryBrush`, `YaapicSurfaceElevatedBrush`, `YaapicSurfaceRaisedBrush`, `YaapicBorderOverlayBrush`,
`YaapicRadiusMd`, `YaapicRadiusSm`, `YaapicMonoFont`, `YaapicGlyphStatusClientError`. No hex colours in the new files.

**Verification (published Native AOT exe on Windows, real mouse and keyboard input through `mouse_event`, `SendKeys` and
`keybd_event` with screen captures, plus `Capture-Avalonia.ps1`):** `{{baseUrl}}/get?x={{nope}}` with `local`: resolved and
unresolved colours inside the text, caret, selection and typing in a token normal; hover popups (value + `Environment`
badge + Edit/Open; `Unresolved variable` + the exact sentence + scope list with the disabled entry and its reason);
create in Environment, Globals and Collection (`local.env.yaml`, `globals.yaml`, `folder.yaml` checked on disk, token
turned green live); active environment deleted: `{{baseUrl}}` unresolved without retyping and the Environment choice
disabled ("No environment is active"); long URL scrolled and selected: colours aligned; dark and light switched at run
time; `{{$guid}}` in the info colour with the dynamic message; `Ctrl+.` then `Escape` (typing continues in the box);
`Ctrl+Enter` sends and the variables resolve; Params stay in sync. The `Capture-Avalonia.ps1` run is `-Send` in dark and
light.
Not exercised: the environment pill menu (`MenuFlyout`) rendered as an empty sliver in this session, so "No environment"
was reached by deleting the active environment in the Environments window (same `ScopesChanged` path); IME composition
and screen readers.

**Differences from the WinUI look:** the highlight is `Run.Background` (rounded corners are not available), the popup uses
the Avalonia `subtle` button style for `Edit`/`Open` instead of hyperlinks, and `Open` is hidden for the Collection scope.
