# R-06: WinUI update banner, settings and About

Status: done
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
- The dialog ("Updates and about") opens from the Settings rail menu and from the palette command "Check for updates" (which also starts a
  check, so a manual check always has somewhere to report).
- Keyboard and screen-reader names on every control; no new styles unless a token is missing.

## Out of scope

Avalonia (R-07).

## Acceptance criteria

- [x] Screenshots of banner (available, downloading, ready), settings and About in `docs/screenshots/release/`.
- [x] Driven with a scripted service (`--update-demo <mode>` on both apps, `-UpdateDemo` in the capture scripts), no network, nothing written to disk.

## Suggested commit message

`feat: add the WinUI update banner and settings`
