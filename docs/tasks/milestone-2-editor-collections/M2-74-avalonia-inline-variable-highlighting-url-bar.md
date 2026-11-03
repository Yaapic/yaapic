# M2-74: Avalonia inline variable highlighting and popup in the URL bar

Status: todo
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
3. **AvaloniaEdit single-line `TextEditor` + `DocumentColorizingTransformer`** (M2-70 package): robust and gives
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

Header and param cells (M2-75), body (later, AvaloniaEdit from M2-70), autocomplete, folder scope, WinUI.

## Acceptance criteria

- [ ] `dotnet build Yaapic.CrossPlatform.slnf -c Release` (and `Yaapic.sln` on Windows) 0 warnings; all tests green;
      architecture tests green.
- [ ] Resource keys used by the new AXAML exist in both theme dictionaries of `design/xaml/Yaapic.Colors.Avalonia.axaml`
      (listed in Notes); no hex colours in the new files.
- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings (the existing Avalonia.Win32 ILC
      messages are not new).
- [ ] **Exercise the PUBLISHED binary** (UI Automation, or `tools/screenshots/Capture-Avalonia.ps1` plus real input):
  - [ ] `{{baseUrl}}/get?x={{nope}}` with environment `Local` active: first token resolved colour, second unresolved,
        inside the URL text; text not shifted or doubled; caret, selection and typing inside a token normal.
  - [ ] Hover each token: the popup content of M2-72 (resolved details and Edit/Save; "Unresolved variable" + the exact
        sentence + create form). Create `nope` in Environment, then in Globals and Collection: token turns resolved live,
        files on disk updated.
  - [ ] "No environment": `{{baseUrl}}` becomes unresolved without retyping; Environment choice disabled with its reason.
  - [ ] Long URL scrolled horizontally keeps colours aligned.
  - [ ] Dark and light (switch at run time) readable; `{{$guid}}` in the dynamic colour.
  - [ ] `Ctrl+.` opens the popup, `Escape` closes it; hover never steals focus.
  - [ ] Ctrl+Enter still sends; params sync with the URL (M2-13).
- [ ] Screenshots through `Capture-Avalonia.ps1` (dark and light, `-Send`), not committed; differences from the WinUI
      look listed in Notes.
- [ ] No Yaapic or stub-server process left running.

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
