# R-07: Avalonia update banner, settings and About

Status: done
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

- [x] Screenshots through the Avalonia screenshot tool, saved next to the WinUI ones.
- [x] Runtime check on the dev machine (Windows run of the Avalonia AOT build): banner available, downloading (42 %) and ready, and
      the dialog (up to date, failed check) with the scripted service. Not run on macOS or Linux.

## Suggested commit message

`feat: add the Avalonia update banner and settings`
