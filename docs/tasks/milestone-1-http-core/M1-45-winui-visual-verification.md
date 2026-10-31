# M1-45: WinUI visual verification against the mockups

Status: done
Platform: windows
Depends on: M1-43, M1-44

## Goal

Capture every M1 WinUI screen in dark and light from the AOT build, commit the screenshots, and write down how each
one differs from the mockups, so remaining gaps become explicit follow-up work instead of surprises.

## Design decisions

- Screenshots with `tools/screenshots/Capture-WinUI.ps1` (M1-34), 1440 x 900, from the Native AOT publish output,
  committed under `docs/screenshots/m1/` as `<screen>-<theme>.png` (PNG, no other format):

  | Screen | Script options | Compare with |
  |---|---|---|
  | `empty` | `-NoTabs` | `States.dc.html` (empty), `DSComposants2.dc.html` (empty state) |
  | `editor-body` | `-Request create-user`, Body tab selected via `-Keys` if needed | `MainWindow.dc.html` (request half) |
  | `editor-params` | `-Request list-users` | `ResponseStates.dc.html` (Query Params) |
  | `response-201` | `-Request create-user -Send` | `MainWindow.dc.html` (response half) |
  | `response-404` | `-Request missing-item -Send` | `ResponseStates.dc.html` (4xx chip) |
  | `response-503` | `-Request server-error -Send` | `ResponseStates.dc.html` (5xx chip) |
  | `response-failed` | `-NoStub -Send` | `States.dc.html` (error blocks) |
  | `sending` | `-Request slow -Send -WaitMs 800` | `ResponseStates.dc.html` (sending) |
  | `environments` | `-Keys '^+e'` | `EnvVars.dc.html` |
  | `unsaved-dialog` | type in the name, then Ctrl+W through `-Keys` | `DSComposants2.dc.html` (dialog) |

  Each in `dark` and `light` (20 files). Keep each file under 500 KB (PNG, no scaling).
- `docs/screenshots/m1/README.md` (English): how to regenerate (publish command, script calls), then one section per
  screen: the two screenshots (relative links), the mockup file and artboard, and a **deviation list**: each item says
  what differs (element, expected value from the mockup source, observed), and classifies it as
  `fix now` (a token or style mistake in an M1 element), `M1 scope` (accepted difference, e.g. no breadcrumb because
  M1 has no collections), or `later milestone` (feature not in M1: rail entries, command palette, status bar...).
  Compare by reading the mockup sources (`TH` colour values, sizes in the inline styles) against the screenshots;
  check at least: surface colours, text colours, accent use, font family of code text, control heights, tab
  indicator, chip colours and icons, borders.
- `fix now` items that are a one-line token, style or margin change are fixed in this card (re-capture after the
  fix). Anything larger is listed in the README under "Follow-up" with a one-line proposal for a new card; do not
  write those cards here.
- Also verify keyboard focus visibility (Tab through title bar, rail, sidebar, tabs, URL, Send) and high contrast:
  start with Windows high contrast enabled if available on the machine and note whether text stays readable (no
  screenshot required).

## Out of scope

New features, Avalonia (its visual cards are not written yet), pixel-perfect comparison tooling, CI image diffs.

## Acceptance criteria

- [x] 20 screenshots committed under `docs/screenshots/m1/`, each a 1440 x 900 capture of the AOT build.
- [x] `docs/screenshots/m1/README.md` lists every screen with its deviations and classification, plus the
      follow-up proposals.
- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] No Yaapic or stub-server process left running.

## Suggested commit message

`docs(winui): add M1 screenshots and mockup comparison`
