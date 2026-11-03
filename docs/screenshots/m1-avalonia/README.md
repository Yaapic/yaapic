# M1 Avalonia screenshots and mockup comparison

Captures of every Milestone 1 Avalonia screen, dark and light, taken from the Native AOT build at 1440 x 900 and
compared with the mockup sources in `design/mockups/` and with the WinUI captures in [`../m1/`](../m1/README.md).

**Operating systems checked: Windows 11 only** (win-x64 AOT publish). macOS and Linux were not run; platform
differences there (fonts, window chrome, focus rendering) are unverified, see F4.

## Regenerate

```powershell
dotnet publish src/Yaapic.App.Avalonia -c Release -r win-x64 -p:PublishAot=true
$s = 'tools/screenshots/Capture-Avalonia.ps1'
foreach ($t in 'dark', 'light') {
  pwsh $s -Theme $t -NoTabs -Open empty                         -Out docs/screenshots/m1-avalonia/empty-$t.png
  pwsh $s -Theme $t -Select Body                                -Out docs/screenshots/m1-avalonia/editor-body-$t.png
  pwsh $s -Theme $t -Request list-users                         -Out docs/screenshots/m1-avalonia/editor-params-$t.png
  pwsh $s -Theme $t -Send -WaitMs 3000                          -Out docs/screenshots/m1-avalonia/response-201-$t.png
  pwsh $s -Theme $t -Request missing-item -Send -WaitMs 3000    -Out docs/screenshots/m1-avalonia/response-404-$t.png
  pwsh $s -Theme $t -Request server-error -Send -WaitMs 3000    -Out docs/screenshots/m1-avalonia/response-503-$t.png
  pwsh $s -Theme $t -NoStub -Send -WaitMs 8000                  -Out docs/screenshots/m1-avalonia/response-failed-$t.png
  pwsh $s -Theme $t -Request slow -Send -WaitMs 800             -Out docs/screenshots/m1-avalonia/sending-$t.png
  pwsh $s -Theme $t -Open environments                          -Out docs/screenshots/m1-avalonia/environments-$t.png
  pwsh $s -Theme $t -Open unsaved                               -Out docs/screenshots/m1-avalonia/unsaved-dialog-$t.png
}
```

Run each capture in its own `pwsh` process. On Windows the script's PowerShell host can stay alive after writing the
PNG (the stub-server runspace keeps it running); the PNG is complete by then, so stop that `pwsh` process, which also
frees the stub listener on port 5199, before the next capture. `sending` works because `--send` no longer waits for
the response: it waits at most `--wait-ms`, so the slow request (10 s) is still in flight when the window is captured.
The stub server's `/delay/N` handler uses `Thread.Sleep` (`Start-Sleep` dropped the connection inside the runspace).

Not captured: `method-picker-open` and `editor-body-form-data` (M2-21, M2-22). The capture options reach neither a
dropdown popup nor a body kind other than the saved one; they need a script option, see F5.

## Deviations that apply to every screen

| # | Deviation | Class |
|---|---|---|
| G1 | No custom caption buttons: minimise, maximise and close are not drawn (WinUI shows them). The capture renders the client area only. | later milestone, F3 |
| G2 | Surfaces follow the tokens: dark `surface.app` title bar, rail and sidebar (same token question as WinUI G1), editor on `s1`, response body on `sk0`. Visually equivalent to the WinUI captures. | none |
| G3 | Rail 48 px, sidebar 272 px, tab strip 36 px, Send in accent `#1FC7D4`, selected-row tint and accent indicator, method colours in the list and tab: match the mockup and WinUI. | none |
| G4 | Code (URL, tables, response body) is JetBrains Mono 12 px as in the mockup. Avalonia text looks lighter than WinUI at the same size (different renderer). | none |
| G5 | Rail icons are Avalonia path icons (window and sliders glyphs) instead of the WinUI library and globe glyphs. Same slot and size. | M1 scope |
| G6 | No menu bar, breadcrumb, status bar, console, bell, avatar or extra rail entries. | later milestone |
| G7 | No syntax colouring and no line numbers in the body and response viewers. | M1 scope, F2 |

## Screens

### empty

[dark](empty-dark.png) · [light](empty-light.png) · WinUI: [dark](../m1/empty-dark.png) · [light](../m1/empty-light.png) —
`States.dc.html` (empty state).

- `-Open empty` closes the open tab, so this is the true empty editor (icon, "No request open.", primary
  "New request" with a `Ctrl+T` keycap). The WinUI capture still shows the untitled tab. Class: none.

### editor-body

[dark](editor-body-dark.png) · [light](editor-body-light.png) · WinUI: [dark](../m1/editor-body-dark.png) ·
[light](../m1/editor-body-light.png) — `ResponseStates.dc.html` (body mode bar).

- Body bar with a bold "Body" label, a segmented control (`none`, `raw`, `x-www-form-urlencoded`, `form-data`,
  `binary`) and the content-type dropdown: matches the mockup and is closer than WinUI (radios, no label). Class: none.
- Code text 12 px mono on `sk0`, no colouring. Class: M1 scope, F2.

### editor-params

[dark](editor-params-dark.png) · [light](editor-params-light.png) · WinUI: [dark](../m1/editor-params-dark.png) ·
[light](../m1/editor-params-light.png) — `ResponseStates.dc.html` (Query Params).

- `KEY`/`VALUE` caps header in `t3`, 28 px rows, accent checkbox, Headers count badge and Body dot: match.
- The table is edge to edge, without the 1 px rounded border WinUI draws around it. Class: M1 scope, trivial.

### response-201

[dark](response-201-dark.png) · [light](response-201-light.png) · WinUI: [dark](../m1/response-201-dark.png) ·
[light](../m1/response-201-light.png) — `MainWindow.dc.html` (response half).

- Status chip (`ok` on `okbg`, check icon, mono), duration and size in `t2`/`t3` mono, Pretty/Raw segmented control and
  the Body/Headers tabs match. Class: none.
- No Beautify, copy or save buttons. Class: later milestone (M2-24).

### response-404

[dark](response-404-dark.png) · [light](response-404-light.png) · WinUI: [dark](../m1/response-404-dark.png) ·
[light](../m1/response-404-light.png) — `ResponseStates.dc.html` (4xx chip).

- Warning chip in `warn` on a translucent amber background with a triangle icon. Class: none.

### response-503

[dark](response-503-dark.png) · [light](response-503-light.png) · WinUI: [dark](../m1/response-503-dark.png) ·
[light](../m1/response-503-light.png) — `ResponseStates.dc.html` (5xx chip).

- Error chip in `err` on `errbg` with a circled cross. Class: none.

### response-failed

[dark](response-failed-dark.png) · [light](response-failed-light.png) · WinUI: [dark](../m1/response-failed-dark.png) ·
[light](../m1/response-failed-light.png) — `States.dc.html` (error blocks).

- 32 px `errbg` circle, "No response" title, message in `t2`: matches WinUI and the block structure of the mockup.
- The message is the localised operating-system exception text (French here), as on WinUI. Class: M1 scope, F1.
- The mockup hint line and retry action are missing, as on WinUI. Class: M1 scope.

### sending

[dark](sending-dark.png) · [light](sending-light.png) · WinUI: [dark](../m1/sending-dark.png) ·
[light](../m1/sending-light.png) — `ResponseStates.dc.html` (sending).

- "Sending" header, a progress line under it and three skeleton bars: match. The progress line is an animation frame
  (only its left end is visible).
- The Send button becomes a neutral outlined "Cancel"; the mockup and WinUI show a danger-red outline, a spinner and an
  `Esc` keycap. Class: fix now, but a button template change rather than a token, see F6.
- Save stays enabled while WinUI shows it disabled until the request is dirty. Class: M1 scope, F6.

### environments

[dark](environments-dark.png) · [light](environments-light.png) · WinUI: [dark](../m1/environments-dark.png) ·
[light](../m1/environments-light.png) — `EnvVars.dc.html`.

- 960 x 560 window: list with status dots and the "active" label, header with name, variable count, danger Delete and
  accent Save, variables table with a mono value column. The active environment is selected on open, so the right pane
  shows the variables as in the mockup; WinUI opens with nothing selected. Class: none (Avalonia is closer).

### unsaved-dialog

[dark](unsaved-dialog-dark.png) · [light](unsaved-dialog-light.png) · WinUI: [dark](../m1/unsaved-dialog-dark.png) ·
[light](../m1/unsaved-dialog-light.png) — `DSComposants2.dc.html` (dialog).

- 400 px dialog, title, body text in `t2`, footer on a darker band, primary Save in accent, secondary Don't save and
  Cancel: matches WinUI. The dialog is captured alone (topmost window). Class: none.

## Keyboard focus and high contrast

- Focus: the captures show the focus ring on the Params tab, the filter field and the primary buttons. The full Tab
  walk (title bar, rail, sidebar, tabs, URL bar, Send) was not performed because the capture tooling is not
  interactive. Class: M1 scope, F4.
- High contrast: not verified; switching the Windows theme would alter the user's settings.

## Follow-up (proposals, not written as cards)

- F1: map `SendFailureKind` to an English message instead of showing the exception text (shared with WinUI).
- F2: an AvaloniaEdit-based code editor with syntax colouring and line numbers (the equivalent of M1-44).
- F3: custom drawn caption buttons for the Avalonia main window.
- F4: manual Tab walk and high-contrast pass, and a screenshot pass on macOS and Linux, which cannot be done on the
  development machine.
- F5: a capture option that opens a dropdown (method picker) and sets the body kind, to capture `method-picker-open`
  and `editor-body-form-data`.
- F6: Cancel button template (danger outline, spinner, `Esc` keycap) and a disabled Save while the request is clean.
