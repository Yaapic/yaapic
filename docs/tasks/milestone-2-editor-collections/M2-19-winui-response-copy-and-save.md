# M2-19: WinUI response copy and save buttons

Status: todo
Platform: windows
Depends on: M2-14, M2-15

## Goal

The WinUI response panel exposes Copy and Save for the body.

## Design decisions

- In `Views/ResponsePanelView.xaml`, to the right of the Pretty/Raw segmented control (M1-42), add two icon buttons
  with tooltips "Copy response body" and "Save response to file", bound to `CopyBodyCommand` and `SaveBodyCommand`
  (the buttons disable themselves through `CanExecute`). Use the `copy`-like glyph and `export` icon from
  `design/xaml/Yaapic.Icons.WinUI.xaml` when present, else Segoe Fluent glyphs.
- After a successful copy, show nothing extra (no toast in M2); after a failed save nothing changes.

## Out of scope

Copying headers, saving with the request, a notification system.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke on the published exe: send `https://httpbin.org/json`, Copy then paste elsewhere gives the pretty
      body; Save writes `response.json` with the raw bytes; both buttons are disabled before the first send and for a
      binary response (`https://httpbin.org/image/png`).
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): copy and save the response body`
