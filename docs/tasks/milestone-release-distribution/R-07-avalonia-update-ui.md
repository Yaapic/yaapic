# R-07: Avalonia update banner, settings and About

Status: todo
Platform: unix
Depends on: R-05

## Goal

The same update UI as R-06 on Avalonia (macOS, Linux; also runs on Windows for checks).

## Design decisions

The About dialog does not exist yet (R-02 only put the version in the window title): this card creates it, showing the
version from `IAppInfo`.

Mirror R-06 control for control with the Avalonia styles of M1-48 to M1-50; same banner states and texts.

## Out of scope

WinUI (R-06).

## Acceptance criteria

- [ ] Screenshots through the Avalonia screenshot tool, saved next to the WinUI ones.
- [ ] Runtime check on the dev machine (Windows run of the Avalonia app) of banner transitions with the fake service.

## Suggested commit message

`feat: add the Avalonia update banner and settings`
