# M2-72: WinUI inline variable highlighting and popup in the URL bar

Status: todo
Platform: windows
Depends on: M2-71, M2-49, M2-16

## Goal

In the WinUI URL bar each `{{name}}` token is coloured **inside the URL text** by state, and hovering it opens a popup
(value and scope, or "Unresolved variable" with a create form). This **replaces the chip strip shipped in M2-49**: the
strip, its mapping functions and the now unused `UrlVariables` API are deleted in this card. M2-49 stays `done` (history);
its behaviour is superseded here.

## Feasibility and approach (decided here, verify in the first hour)

A WinUI `TextBox` cannot colour sub-ranges of its text. Options evaluated:

1. **Overlay (chosen): a `TextBox` with transparent text over a coloured `TextBlock`.** The `TextBox` keeps all editing
   behaviour (caret, selection, IME, undo, clipboard, accessibility); a `TextBlock` with one `Run` per segment sits
   behind it with identical font, size and padding. Cheap, AOT-safe, no new dependency, reusable for header and param
   cells (M2-73). Risks to prove: (a) the `TextBox` background must be transparent in **every** visual state (override
   `TextControlBackground*` in the control's resources; the surface comes from the host `Border`); (b) identical text
   metrics (same `FontFamily` `YaapicMonoFont`, `FontSize`, `Padding`, no letter spacing, `TextWrapping` none);
   (c) horizontal scrolling: when the URL is wider than the box the inner `ScrollViewer` (template part
   `ContentElement`, found with `VisualTreeHelper` after `Loaded`) scrolls; mirror its `HorizontalOffset` into a
   `TranslateTransform` of the overlay on `ViewChanged` and clip the overlay to the content area; (d) caret and selection
   remain visible with transparent foreground (`CaretBrush`/`SelectionHighlightColor*` resources: check the names in the
   installed Windows App SDK, fall back to resource overrides); (e) AOT.
2. **`RichEditBox`**: real per-range `CharacterFormat`, but multi-line by default, rich paste, formatting pollutes the
   undo stack, different template from the M1-36 input styles. **Fallback only** if (1) cannot meet (a) to (d): set
   `AcceptsReturn` false, `ClipboardCopyFormat` plain text, disable formatting accelerators, colour with
   `ITextRange.CharacterFormat.ForegroundColor` after each change without moving the selection.
3. **LoomEditor in single-line mode (M2-69)**: not chosen. It needs the Windows App SDK 2.x migration (M2-68), is a
   full code editor (gutter, minimap-class features) for a 32 px bar, and its inline-decoration API is unverified. It is
   the right tool for **bodies** (a later card: `{{variable}}` decorations in `CodeEditorView`, using the same
   `VariableContext`). Do not wait for it: this card ships before M2-68/M2-69 and does not depend on them.

Record the outcome of the spike (what worked, what needed a workaround, the fallback if used) in Notes. If (1) fails and
(2) also fails, stop and report; do not ship a version where the text and the colours can drift apart.

## Design decisions

- **New control** `src/Yaapic.App.WinUI/Views/VariableTextBox.xaml(.cs)` (a `UserControl`; M2-73 reuses it): a `Grid`
  with the overlay `TextBlock` (`IsHitTestVisible` false, `AutomationProperties.AccessibilityView` Raw so Narrator does
  not read the text twice) and the `TextBox Input` (`Foreground` transparent, `Background` transparent,
  `AutomationProperties.Name` forwarded). Dependency properties: `Text` (two-way, `UpdateSourceTrigger`
  `PropertyChanged` semantics as the current URL box), `PlaceholderText`, `Context` (`VariableContext?`, null = plain
  text box, no overlay work), `InputName` (automation name). Register the DPs as the concrete types; if a DP of the
  class type fails only under Native AOT, type it `object` and cast (note it).
- **Rendering**: on `Text` change and `Context.Changed`: `tokens = Context.Tokenize(Text)`; rebuild the overlay `Inlines`
  (plain `Run` for text between tokens, one `Run` per token; text length must equal `Text.Length` exactly, so never
  trim or replace characters, and keep the double-brace characters inside the token run). Colours through brushes
  resolved from the resource keys below with `ThemeResource` semantics: re-read them on `ActualThemeChanged`.
  - Resolved: `YaapicSuccessBrush` text on a rounded `YaapicSuccessBackgroundBrush` highlight (use an `InlineUIContainer`
    only if metrics stay identical; otherwise colour the text only, no background; note which). Unresolved:
    `YaapicWarningBrush` (`YaapicWarningBackgroundBrush`). Dynamic (`$guid`, ...): `YaapicInfoBrush`. All keys exist in
    `design/xaml/Yaapic.Colors.WinUI.xaml`; verify each in the file and check contrast on the URL bar surface in dark and
    light (Warning on light must stay readable; if not, take the nearest token and note it). Never hard-code hex.
  - Coalesce: rebuild only when `Text` or the token list changed (compare), no allocation when there is no `{{`.
- **Hover popup** `src/Yaapic.App.WinUI/Views/VariablePopupView.xaml(.cs)` (a `UserControl` bound with `x:Bind` and
  `x:DataType="vm:VariablePopupViewModel"`), shown in a `Microsoft.UI.Xaml.Controls.Primitives.Popup` (not a `Flyout`:
  a flyout steals focus from the URL box mid-typing) with `XamlRoot` set, `IsLightDismissEnabled` false, placed under
  the token.
  - Hit test: on `PointerMoved` over the control map the pointer to a token with
    `Input.GetRectFromCharacterIndex(token.Start, false)` and `GetRectFromCharacterIndex(token.End, true)` (relative to
    the box, so scrolling is already accounted for). Open after about 350 ms on a token (a `DispatcherTimer`), close
    about 250 ms after the pointer leaves **both** the token and the popup, on `Escape`, on text edit that removes the
    token, on tab change and when the box loses focus to something outside the popup. One popup at a time; moving to
    another token swaps its content.
  - Keyboard: with the caret inside a token, `Ctrl+.` opens the popup and moves focus into it; `Escape` returns focus to
    the URL box with the caret unchanged.
  - Content (all from `VariablePopupViewModel`, strings are the VM's constants): header row with the name in the token
    colour; **resolved**: value in `YaapicMonoFont`, a scope badge (`ScopeLabel`), `Edit` link (reveals the value
    `TextBox` and a `Save` button) and `Open` link; **dynamic**: message only; **unresolved**: a
    `YaapicDangerBrush` icon + title "Unresolved variable", the message text, then the form: scope `ComboBox`
    (`ScopeChoices`, disabled entries greyed with `DisabledReason` as tooltip), a value `TextBox`, and a `Create` button
    (`YaapicAccent` style) that runs `SubmitCommand`; `LastError` in `YaapicDangerBrush`. After a successful create the
    token turns green live (through `ScopesChanged`) and the popup shows the resolved state, or closes if the pointer
    left. Typing in the popup must not move the URL box caret. Use the existing control styles of M1-36/M1-37 and
    `design/xaml` brushes only (`YaapicSurfaceRaisedBrush`/border tokens: take the names from the dialog styles already in
    the app).
  - Wire `Context.OpenRequested`: `Environment` and `Globals` open the existing environments dialog (globals tab for
    Globals; see how the title bar / rail opens it in `MainWindow`/shell code-behind), `Collection` opens the folder
    settings dialog of the request's collection (M2-48). If an entry point does not exist as a method, add a small one in
    the shell code-behind; no new dialog.
- **Remove the chip strip** from `Views/RequestEditorView.xaml` (the `ItemsRepeater` and the `Grid.Row="1"` it
  occupies; collapse the now empty row definition) and replace the URL `TextBox` by
  `<local:VariableTextBox Context="{x:Bind ViewModel.Variables, Mode=OneWay}" Text="{x:Bind ViewModel.Url, Mode=TwoWay}" .../>`
  keeping the placeholder, font, height, padding and `AutomationProperties.Name="URL"` of the old box (UI Automation
  tests and screenshot scripts look for the name `URL`; check `tools/screenshots/Capture-WinUI.ps1`). Delete
  `ListMap.Hints`, `TextMap.HintText`, `TextMap.HintTooltip`, `BrushMap.HintForeground`, `BrushMap.HintBackground`.
  In `Yaapic.ViewModels`: delete `RequestEditorViewModel.UrlVariables`, `HasUrlVariables`, `RefreshUrlVariables` and its
  duplicated subscription (the `VariableContext` of M2-71 owns activation and `Changed` now), and migrate
  `tests/Yaapic.ViewModels.Tests/RequestEditorUrlVariablesTests.cs` to the `Variables.Tokenize` API (keep the cases, they
  still describe the behaviour). Keep `VariableHint` (used by tokens). The chip styles of M1-37 stay (used elsewhere).
- The URL bar behaviour of M2-13/M2-16 (two-way query sync, Ctrl+Enter send, method picker) is unchanged; re-test it.

## Out of scope

Header and param cells (M2-73), body (later, LoomEditor), autocomplete of `{{`, folder scope, Avalonia (M2-74, M2-75),
changing token colours or adding new brushes to the generator.

## Acceptance criteria

- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; architecture tests green.
- [ ] Every resource key used by the new XAML exists in `design/xaml/Yaapic.Colors.WinUI.xaml` (or the app's own
      resources); list the keys in Notes. No hex colours in the new files.
- [ ] `dotnet publish src/Yaapic.App.WinUI -c Release -r win-x64 -p:PublishAot=true`: 0 IL2xxx/IL3xxx warnings.
- [ ] **Exercise the PUBLISHED exe** (launch it through `tools/screenshots/Capture-WinUI.ps1` with the seeded workspace,
      environment `Local` active, `baseUrl` = `http://127.0.0.1:5199`; UI Automation and real mouse input):
  - [ ] URL `{{baseUrl}}/get?x={{nope}}`: `{{baseUrl}}` is drawn in the resolved colour and `{{nope}}` in the unresolved
        colour **inside the text**; the text is not doubled or shifted; caret, selection and typing in the middle of a
        token behave normally; no chip strip remains and no empty gap under the URL bar.
  - [ ] Hover `{{baseUrl}}`: popup shows the name, `http://127.0.0.1:5199`, the scope `Environment`; Edit changes the value,
        Save recolours without retyping, Open opens the environments dialog.
  - [ ] Hover `{{nope}}`: popup shows "Unresolved variable", the exact sentence "This variable is not defined in the
        active collection, environment or globals.", the scope list with disabled entries explained; create it in the
        active environment with value `1`: the token turns green live, the environment file on disk contains it; repeat
        for Globals and Collection (request inside a collection) and check the files.
  - [ ] Switch to "No environment": `{{baseUrl}}` turns unresolved without retyping; the Environment scope choice is
        disabled with its reason.
  - [ ] A URL longer than the box (scroll horizontally): colours stay aligned with the text while scrolling and typing.
  - [ ] Dark and light theme (switch at run time): colours follow the theme and stay readable; `{{$guid}}` is shown in the
        dynamic colour with the dynamic message.
  - [ ] `Ctrl+.` inside a token opens the popup, `Escape` closes it and returns the caret; popup never steals focus
        while merely hovering.
  - [ ] Params tab still syncs with the URL (M2-13), Ctrl+Enter still sends, the sent request resolves the variables.
- [ ] Screenshots via `Capture-WinUI.ps1` (dark and light, resolved + unresolved + popup open if the script can capture
      popups; otherwise describe them in Notes), not committed.
- [ ] The chip-strip code is gone (grep for `UrlVariables`, `HasUrlVariables`, `ListMap.Hints`, `HintTooltip` returns
      nothing in `src` and `tests`).
- [ ] No Yaapic or stub-server process left running.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (the published exe, the stub server, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m2-editor-collections` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`feat(winui): colour variables inline in the URL bar with a hover popup`

## Notes
