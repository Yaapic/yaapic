# M4-69: WinUI: HTTP version in the Settings tab

Status: todo
Platform: windows
Depends on: M4-59, M4-61

## Goal

The request Settings tab lets the user choose the HTTP version.

## Design decisions

- Read first: `Views/RequestEditorView.xaml` (`SettingsPane`: the `ToggleSwitch` "Follow redirects" and the timeout `NumberBox`), M4-59.
- Add below the timeout: a `ComboBox` "HTTP version" bound to `HttpVersionChoices` (`DisplayMemberPath` `Label`; an item with `IsAvailable` false is disabled: set `IsEnabled` through an
  item container style bound to `IsAvailable`; WinUI `ComboBoxItem` does not take a binding from a plain `x:Bind` list, so use an `ItemContainerStyle` with a setter or a data template with
  opacity: pick what works and state it), selected value two-way with `HttpVersion` (index map in code-behind like the other enum boxes), and the `HttpVersionNote` text in the muted style
  under it when non-empty.
- Same spacing as the two existing settings; `x:Bind` only; tokens only.

## Out of scope

Showing the protocol (M4-66), a global default.

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards).
- [ ] Manual smoke on the published exe: choose HTTP/1.1, save, reopen: the choice persists and the file has `httpVersion: http1`; on a machine where HTTP/3 is unavailable the
  option is disabled; a request set to HTTP/2 against an HTTP/2 server shows `HTTP/2` as the protocol (use `https://www.google.com` or a local server and say which). Record anything not run.
- [ ] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the HTTP version choice to request settings`
