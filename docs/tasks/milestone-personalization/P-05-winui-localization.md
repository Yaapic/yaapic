# P-05: WinUI: localize the views

Status: todo
Platform: windows
Depends on: P-04

## Goal

The roughly 250 literals of the WinUI XAML and code-behind (menus, dialogs, tooltips, automation names, placeholders) use the catalogue.

## Design decisions

- XAML binds to the generated catalogue through `x:Bind` on static members (set once at start); no `x:Uid` or `.resw`.
- Automation names are translated too.
- A test scans the XAML files and fails on a new `Content="..."` or `Text="..."` literal containing letters.

## Acceptance criteria

- [ ] Every view in French; screenshots of the main window, a dialog and the palette in `docs/screenshots/localization`.
- [ ] No clipped text at French lengths (buttons, status bar, tabs).

## Suggested commit message

`feat: localize the WinUI views`
