# M6-43: WinUI: tool window host and line chart

Status: done
Platform: windows
Depends on: M6-42

## Goal

The WinUI pieces the four M6 windows share: a non-modal secondary window that hosts a view, a small line chart control, and the new
`IDialogService.ConfirmAsync`.

## Design decisions

- Read first: `src/Yaapic.App.WinUI/MainWindow.xaml(.cs)` (title bar extension, Mica, `ApplyCaptionButtonColors`, theme handling,
  `OnClosing`), `src/Yaapic.App.WinUI/Services/` (the `IDialogService` implementation, `XamlRoot` use), `Presentation/BrushMap.cs`, the AOT
  notes of M4-68 (`ContentDialogMaxWidth`, `ListMap`), M6-26 (`ChartPoint`, `ConfirmAsync`). The app has no secondary window yet.
- `src/Yaapic.App.WinUI/Views/ToolWindow.cs` (code only, no XAML): `public sealed class ToolWindow : Window` built with a title and a
  `FrameworkElement` content; it copies the main window's look (extends content into the title bar with a 48 px title bar row showing the
  title, the same caption-button colours from the tokens, Mica backdrop, the main window's `RequestedTheme` and updates when it changes),
  default size 1100 x 720, minimum 800 x 560, centred on the main window. Static helper
  `ToolWindow.ShowOrActivate(ref ToolWindow? slot, string title, Func<FrameworkElement> content, MainWindow owner)`: one window per slot,
  activates it when open, clears the slot when it closes. `MainWindow` closes every open tool window when it closes (after its own close
  confirmation succeeds).
- Dialogs inside a tool window must use the tool window's `XamlRoot`: the WinUI dialog service gains an active-window override
  (`ToolWindow` sets it to itself on `Activated` with `WindowActivationState` other than `Deactivated`, and clears it when it is deactivated
  or closed); while set, every `ContentDialog` of the service uses that window's `XamlRoot` and every picker (`SaveFileAsync`,
  `PickFileAsync`, `PickFolderAsync`) is initialised with that window's handle (`WinRT.Interop.InitializeWithWindow`).
- `IDialogService.ConfirmAsync(title, message)` implemented with a `ContentDialog`: primary button `Strings.CommonContinue`
  ("Continue"), close button `Strings.CommonCancel` (add `common.continue` and `common.cancel` in EN and FR if missing); true only for
  the primary button.
- `src/Yaapic.App.WinUI/Views/LineChart.cs`: a `UserControl` with dependency properties `Points` (`IReadOnlyList<ChartPoint>`), `Title`,
  `MaxLabel`; draws a `Polyline` (accent stroke from the tokens, 1.5 px) over a border with two horizontal grid lines, scaling X and Y
  (0..1) to the actual size on `SizeChanged` and when `Points` changes; empty list draws nothing. No chart package.

## Out of scope

The four windows (M6-44 to M6-48) and their entry points (M6-49).

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts.
- [x] A temporary debug call (removed before the commit) opens a `ToolWindow` with a `LineChart` of sample points and a `ConfirmAsync`
  dialog inside it, in dark and light: the title bar looks like the main window's, the dialog appears over the tool window, closing the main
  window closes the tool window. Record anything not run in Notes.
- [x] Strings in EN and FR; 0 warnings, all tests green.

## Notes

- The temporary debug call opened a `ToolWindow` with a `LineChart` in dark and light (AOT binary): the title bar, Mica and the chart look right in both.
  Not run: the `ConfirmAsync` dialog over the tool window and closing the main window to close the tool window (the main window was not visible
  in the screenshots), and the French locale visual check.
- `ShowOrActivate` takes the slot by `ref`, which a `Closed` handler cannot capture: the window records that it closed and a closed window in the
  slot is replaced on the next call.

## Suggested commit message

`feat(winui): add the tool window host and the line chart`
