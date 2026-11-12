# P-08: WinUI: apply the accent at runtime

Status: todo
Platform: windows
Depends on: P-07

## Goal

Apply the chosen accent to the running app, both themes, without restart.

## Design decisions

- Override the accent colour and brush resources (`SystemAccentColor*` and the `YaapicAccent*Brush` keys) in the theme dictionaries at
  start-up and when the choice changes. "Windows accent" reads the system accent (`UISettings`).
- Nothing hard-codes the teal outside the token file (checked by a grep in this card).

## Acceptance criteria

- [ ] Primary buttons, focus rings, selection, links and toggle switches follow the accent in dark and light.
- [ ] Screenshots with three accents in `docs/screenshots/accent`.

## Suggested commit message

`feat: apply the accent colour at runtime in WinUI`
