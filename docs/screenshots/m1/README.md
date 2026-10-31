# M1 WinUI screenshots and mockup comparison

Captures of every Milestone 1 WinUI screen, dark and light, taken from the Native AOT build at 1440 x 900 and
compared with the mockup sources in `design/mockups/` (see its README for the artboard list).

## Regenerate

```powershell
dotnet publish src/Yaapic.App.WinUI -c Release -r win-x64 -p:PublishAot=true
$s = 'tools/screenshots/Capture-WinUI.ps1'
foreach ($t in 'dark', 'light') {
  pwsh $s -Theme $t -NoTabs                                   -Out docs/screenshots/m1/empty-$t.png
  pwsh $s -Theme $t -Select BodyTab                           -Out docs/screenshots/m1/editor-body-$t.png
  pwsh $s -Theme $t -Request list-users                       -Out docs/screenshots/m1/editor-params-$t.png
  pwsh $s -Theme $t -Send -WaitMs 3000                        -Out docs/screenshots/m1/response-201-$t.png
  pwsh $s -Theme $t -Request missing-item -Send -WaitMs 3000  -Out docs/screenshots/m1/response-404-$t.png
  pwsh $s -Theme $t -Request server-error -Send -WaitMs 3000  -Out docs/screenshots/m1/response-503-$t.png
  pwsh $s -Theme $t -NoStub -Send -WaitMs 8000                -Out docs/screenshots/m1/response-failed-$t.png
  pwsh $s -Theme $t -Request slow -Send -WaitMs 800           -Out docs/screenshots/m1/sending-$t.png
  pwsh $s -Theme $t -Keys '^+e'                               -Out docs/screenshots/m1/environments-$t.png
  pwsh $s -Theme $t -FocusElement 'Request name' -Keys 'x^w'  -Out docs/screenshots/m1/unsaved-dialog-$t.png
}
```

Run each capture in its own `pwsh` process (as above): several captures in one PowerShell process leave the previous
stub server listening and the responses never arrive. `-Select` and `-FocusElement` were added to the script for this
card (UI Automation: invoke a button by AutomationId, focus a field by name). The failed connection takes about two
seconds to be refused on Windows, hence the long wait.

## Deviations that apply to every screen

| # | Deviation | Class |
|---|---|---|
| G1 | Dark title bar and rail use `surface.app` `#131518`; the mockup `chrome` is `#17191D` (light matches: `#EEF0F3`). The token file and the mockup disagree. | fix now (token), see Follow-up F1 |
| G2 | The mockup main area is `s1` (`#1A1D21` dark, `#FFFFFF` light); the request editor was drawn on `surface.app`. Fixed in this card: `RequestEditorView` root now uses `YaapicSurfaceBaseBrush`. All screenshots were taken after the fix. | fix now (done) |
| G3 | Light sidebar `#F3F4F6` (token) against `s0` `#F4F5F7` (mockup). One level of difference, not visible. | M1 scope (tokens are the source of truth) |
| G4 | No menu bar, breadcrumb, status bar, console, notification bell, user avatar, rail entries beyond Requests and Environments. | later milestone |
| G5 | No syntax colouring in the body editors (plain `TextBox`, see M1-44). | M1 scope |
| G6 | Matches the mockup: rail 48 px, sidebar 272 px, tab strip 36 px, URL bar height, Send in `acc` `#1FC7D4`, tab indicator `#1FC7D4`, selection row tinted with `sel`, request name and code text in JetBrains Mono, response `sk0` body surface. | none |

## Screens

### empty

[dark](empty-dark.png) · [light](empty-light.png) — compare with `States.dc.html` (empty response) and
`DSComposants2.dc.html` (empty state).

- `-NoTabs` still opens an untitled "New Request" tab (the app creates one when no request is open), so the
  fully empty editor state is not reachable from the script. Observed: tab, enabled Save with unsaved dot, disabled
  Send. Class: M1 scope (behaviour of M1-23); noted in F3.
- The empty response panel (icon, "The response will appear here.", `Ctrl` `Enter` keycaps) matches the mockup. No
  deviation.
- In the capture the environment pill shows a focus rectangle (startup focus). Class: M1 scope.

### editor-body

[dark](editor-body-dark.png) · [light](editor-body-light.png) — `MainWindow.dc.html` (request half),
`ResponseStates.dc.html` (body mode bar).

- Body mode radios (`none`/`raw`) render a ~20 px ring with a thick centre; the mockup uses a 10 px dot with a 3 px
  `acc` border. Class: fix now, but larger than a one-line change (control template), see F2.
- The mockup body bar starts with a bold "Body" label; ours has none. Class: M1 scope.
- Code text is 12 px JetBrains Mono on `sk0`, as in the mockup.

### editor-params

[dark](editor-params-dark.png) · [light](editor-params-light.png) — `ResponseStates.dc.html` (Query Params).

- Table header (`KEY`/`VALUE` caps in `t3`), 28 px rows, checkbox in `acc`: match.
- The Headers badge shows the count and Body shows a dot when a raw body exists.

### response-201

[dark](response-201-dark.png) · [light](response-201-light.png) — `MainWindow.dc.html` (response half).

- Status chip: `ok` text on `okbg`, check icon, mono text. Match in both themes.
- Duration and size in `t2`/`t3` mono. Match.
- Pretty/Raw segmented control and tabs match; the mockup also has a Beautify action and copy/save buttons. Class: later milestone.

### response-404

[dark](response-404-dark.png) · [light](response-404-light.png) — `ResponseStates.dc.html` (4xx chip).

- Warning chip uses `warn` on a translucent amber background with a triangle icon, as in the mockup.

### response-503

[dark](response-503-dark.png) · [light](response-503-light.png) — `ResponseStates.dc.html` (5xx chip).

- Error chip uses `err` on `errbg` with a circled cross, as in the mockup.

### response-failed

[dark](response-failed-dark.png) · [light](response-failed-light.png) — `States.dc.html` (error blocks).

- The block (32 px circle in `errbg`, title "No response", message in `t2`) follows the mockup error block
  structure (icon, title, message); a hint line and retry action were not compared. Class: M1 scope.
- The message text is the operating-system exception text, so it is localised (French on this machine). Class: M1
  scope; see F3.

### sending

[dark](sending-dark.png) · [light](sending-light.png) — `ResponseStates.dc.html` (sending).

- Cancel button (danger outline, spinner, `Esc` keycap), animated progress line in `acc` under the "Sending"
  header and three skeleton bars: match. The spinner glyph is partially clipped in the capture (animation frame).

### environments

[dark](environments-dark.png) · [light](environments-light.png) — `EnvVars.dc.html`.

- List with status dot, "active" label and a 960 x 560 dialog: structure matches.
- No environment is selected on open, so the right pane shows "Select an environment, or add one."; the mockup shows
  the active environment's variables. Class: M1 scope, proposed in F3.
- The mockup header (name, meta, actions) and the variables table are not visible here for that reason; they are
  covered by the M1-41 tests, not by this capture.
- The "+" add button shows a focus ring on open (initial focus). Class: M1 scope.

### unsaved-dialog

[dark](unsaved-dialog-dark.png) · [light](unsaved-dialog-light.png) — `DSComposants2.dc.html` (dialog).

- Title, body text in `t2`, footer on a darker band, primary Save in `acc` with a visible focus ring, secondary
  buttons: match. The dialog is 400 px wide; the mockup width was not checked.

## Keyboard focus and high contrast

- Focus: not walked end to end. The captures show a visible 2 px focus rectangle on the Params tab, the Save button
  of the dialog, the environment pill and the add button; the full Tab walk (title bar, rail, sidebar, tabs, URL,
  Send) stays for a manual pass, see F4.
- High contrast: not verified. The session ran on an unattended interactive desktop and switching the Windows
  theme would alter the user's settings, so this stays open for a manual pass (the tokens define a `highContrast`
  value for every surface).

## Follow-up (proposals, not written as cards)

- F1: dark title bar and rail colour. Decide whether the token `surface.app` or the mockup `chrome` is right, add a
  `surface.chrome` token if the mockup is, and use it for the title bar, rail and (later) status bar.
- F2: WinUI radio button template: 10 px dot, 3 px `acc` ring, 12 px label, matching `ResponseStates.dc.html`.
- F3: first-run polish: open the active environment selected in the environments dialog, an English-only message
  for connection failures (map `SendFailureKind` instead of showing the exception text), and a truly empty editor
  area when no request is open.
- F4: High contrast manual verification and a focus-ring pass on the body editor and key/value tables.
