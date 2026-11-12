# R-06: WinUI update banner, settings and About

Status: todo
Platform: windows
Depends on: R-05

## Goal

The update UI on Windows: a non-modal banner, a settings section, the About dialog.

## Design decisions

The About dialog does not exist yet (R-02 only put the version in the window title): this card creates it, showing the
version from `IAppInfo`.

- Banner under the title bar using the existing feedback/info-bar styles: "Version X is available" with Download, then a
  progress bar, then "Restart to update" and a dismiss button. Never modal, never steals focus.
- Settings section (channel, automatic check, version, "Check now") follows the settings page pattern of M3.
- Keyboard and screen-reader names on every control; no new styles unless a token is missing.

## Out of scope

Avalonia (R-07).

## Acceptance criteria

- [ ] Screenshots of banner (available, downloading, ready), settings and About in `docs/screenshots/release/`.
- [ ] Driven with the fake service in the screenshot tool, no network.

## Suggested commit message

`feat: add the WinUI update banner and settings`
