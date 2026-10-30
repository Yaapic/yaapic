# M1-46: WinUI resizable request / response splitter

Status: todo
Platform: windows
Depends on: M1-42

## Goal

Let the user drag the boundary between the request editor and the response panel, as in Postman, so a long body or
response never pushes the other half off screen. The current layout stacks the two halves with fixed proportions
(see the screenshots in M1-34/M1-35: the response body is cut at the bottom of the window).

## Design decisions

- **Hand-written splitter, no new dependency.** WinUI 3 has no `GridSplitter`; use a `Thumb` in a 5 px row between the
  two halves (the visible line is 1 px `YaapicBorderSubtleBrush`, the hit area stays 5 px, cursor
  `InputSystemCursorShape.SizeNorthSouth` through a small `ProtectedCursor` subclass or `UIElement` helper that is
  AOT safe). No reflection, no `CommunityToolkit.WinUI` package.
- The request area is a `RowDefinition` with `MinHeight` 160 and the response area `MinHeight` 120; dragging adjusts
  the request row height (`DragDelta` -> `Height = new GridLength(...)`), the response row is `*`.
- Keyboard: the splitter is focusable (`AutomationProperties.Name` "Resize request and response panels"), Up / Down move
  it by 16 px, Home resets to the default ratio; focus ring from M1-36.
- Double click resets to the default (60 % request / 40 % response).
- Hover and drag show `YaapicAccentBrush` on the 1 px line (2 px while dragging).
- The split position is per window and kept in memory only; persisting layout is a later milestone. The ViewModels
  are untouched: this is presentation state in `MainWindow` / `RequestEditorView` code-behind.
- When the response panel is Empty the splitter stays, so the layout does not jump when a response arrives.

## Out of scope

Horizontal (side by side) layout toggle, resizable sidebar, persisted layout, collapsing the response panel.

## Acceptance criteria

- [ ] Dragging the splitter resizes both halves between their minimum heights; the window never shows a half cut off
      at the default 1440 x 900 size with a long JSON response.
- [ ] Keyboard Up / Down / Home work; double click resets.
- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; no new package.
- [ ] AOT publish 0 IL2xxx/IL3xxx warnings; published exe starts, sends a request with `-Send` and exits cleanly.
- [ ] Screenshots (`tools/screenshots/Capture-WinUI.ps1 -Send`, dark and light) show the response body fully visible;
      result noted in the card.

## Suggested commit message

`feat(winui): add a resizable request and response splitter`
