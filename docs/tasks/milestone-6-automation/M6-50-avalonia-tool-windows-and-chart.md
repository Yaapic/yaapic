# M6-50: Avalonia: tool windows, line chart and confirmation dialog

Status: todo
Platform: unix
Depends on: M6-42, M6-43

## Goal

The Avalonia counterpart of M6-43: a helper that shows a non-modal tool window once, the line chart control and
`IDialogService.ConfirmAsync`.

## Design decisions

- Read first: M6-43 (behaviour to match), `src/Yaapic.App.Avalonia/Views/CertificatesWindow.axaml(.cs)` and `ImportWindow.axaml(.cs)` (window
  styling, owner, sizing), `src/Yaapic.App.Avalonia/MainWindow.axaml.cs` (how windows are opened and closed with the main window), the
  Avalonia dialog service (`rtk grep -rln "IDialogService" src/Yaapic.App.Avalonia`), `Presentation/BrushMap.cs`.
- `src/Yaapic.App.Avalonia/Views/ToolWindowHost.cs`: `static void ShowOrActivate(ref Window? slot, string title, Func<Control> content,
  Window owner)`: creates a `Window` (title, 1100 x 720, minimum 800 x 560, `WindowStartupLocation.CenterOwner`, the same chrome/theme
  resources as the existing windows), `Show(owner)` (non-modal), activates it when already open, clears the slot on `Closed`. The main
  window closes every tool window after its own close is confirmed.
- Dialogs and pickers opened from a tool window use that window as their parent: the Avalonia dialog service gains the same active-window
  override as M6-43 (set on `Activated`, cleared on `Deactivated`/`Closed`).
- `ConfirmAsync(title, message)`: a small modal window like `PromptWindow` with **Continue** and **Cancel** (`Strings.CommonContinue`,
  `Strings.CommonCancel`); true only for Continue.
- `src/Yaapic.App.Avalonia/Controls/LineChart.cs`: a `Control` overriding `Render` (`DrawingContext.DrawGeometry` of a polyline
  `StreamGeometry`, accent pen 1.5 px, two horizontal grid lines, title and max label as `FormattedText`), styled properties `Points`
  (`IReadOnlyList<ChartPoint>`), `Title`, `MaxLabel`; `AffectsRender` on all three.

## Out of scope

The four windows (M6-51 to M6-55), entry points (M6-56).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly (kill it afterwards).
- [ ] Runtime smoke on macOS or Linux through a temporary debug call (removed before the commit): a tool window with a chart and a
  confirmation over it, dark and light; if not possible, say so in Notes and tick only the build and AOT items.
- [ ] Strings exist in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add tool windows, the line chart and the confirmation dialog`
