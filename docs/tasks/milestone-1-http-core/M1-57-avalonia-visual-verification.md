# M1-57: Avalonia visual verification against the mockups

Status: todo
Platform: unix
Depends on: M1-55, M1-56

## Goal

Capture every Milestone 1 (and M2-so-far) Avalonia screen in dark and light from the AOT build, commit the
screenshots, compare them with the mockups and the WinUI captures, and write down the remaining deviations, so the
Avalonia app is declared faithful before more Avalonia UI is added. Equivalent of M1-45 (read it first for the method
and the deviation classes `fix now`, `M1 scope`, `later milestone`).

## Design decisions

- Screenshots with `tools/screenshots/Capture-Avalonia.ps1` (M1-47), 1440 x 900, from the Native AOT publish of the
  host OS, committed under `docs/screenshots/m1-avalonia/` as `<screen>-<theme>.png`. Same ten screens as M1-45
  (`empty`, `editor-body`, `editor-params`, `response-201`, `response-404`, `response-503`, `response-failed`,
  `sending`, `environments`, `unsaved-dialog`), each in `dark` and `light` (20 files, each under 500 KB), plus
  `method-picker-open` and `editor-body-form-data` (the all-methods and all-body-kinds views added by M2-21 and
  M2-22) if the script options can reach them; otherwise capture them by hand and say so.
- `docs/screenshots/m1-avalonia/README.md` (English): how to regenerate (publish and script calls), then one section
  per screen with the two screenshots, the mockup artboard, a link to the WinUI capture of the same screen in
  `docs/screenshots/m1/`, and a **deviation list** (element, expected value from the mockup source, observed, class).
  Check at least: surface and text colours, accent use, code font, control heights, URL bar, tab indicators, chip
  colours and icons, borders, focus ring, popup surfaces, dialog layout.
- Deviations that are a one-line token, style or margin fix are fixed in this card (then re-capture). Anything larger
  goes under "Follow-up" with a one-line proposal. Expected follow-ups to evaluate: an AvaloniaEdit-based code editor
  with syntax colouring and line numbers (the equivalent of M1-44, not part of this pass), custom drawn caption buttons,
  platform differences on macOS and Linux that cannot be checked on the development machine.
- Verify keyboard focus visibility (Tab through title bar, rail, sidebar, tabs, URL bar, Send) and note high-contrast
  behaviour if the host OS offers it. State which operating systems were actually checked; do not claim macOS or Linux
  if only Windows was run.
- Update `docs/tasks/README.md`: the sentence about the deferred Avalonia visual cards is replaced by a link to the
  README written here.

## Out of scope

New features, pixel-perfect diff tooling, CI image comparison, fixing large deviations.

## Acceptance criteria

- [ ] 20 or more PNGs present, each under 500 KB, all linked from the README; the README lists a class for every
      deviation and the operating systems checked.
- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green (only fixes of one line or so were made).
- [ ] The Avalonia and WinUI captures of the same screen are visually equivalent at the level the README states.

## Suggested commit message

`docs(avalonia): add visual verification screenshots and deviation list`
