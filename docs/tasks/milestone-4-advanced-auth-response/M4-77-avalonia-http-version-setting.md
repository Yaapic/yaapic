# M4-77: Avalonia: HTTP version in the Settings tab

Status: todo
Platform: unix
Depends on: M4-59, M4-61

## Goal

The Avalonia request Settings tab lets the user choose the HTTP version, matching M4-69.

## Design decisions

- Read first: M4-69, `Views/RequestEditorView.axaml` (the Settings pane with the redirects toggle and the timeout `NumericUpDown`), M4-59.
- Add a `ComboBox` "HTTP version" below the timeout, items from `HttpVersionChoices` with `IsEnabled` bound to `IsAvailable` through the item template container (Avalonia supports an
  `ItemContainerTheme`/`Styles` with a `ComboBoxItem` `IsEnabled` binding), selected value two-way with `HttpVersion`, and the `HttpVersionNote` muted text below.
- Same spacing as the other settings; compiled bindings; tokens only.

## Out of scope

Showing the protocol (M4-74).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly (kill it afterwards).
- [ ] Runtime smoke on macOS or Linux as in M4-69; if not possible, say so in the Notes and tick only the build and AOT items.
- [ ] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the HTTP version choice to request settings`
