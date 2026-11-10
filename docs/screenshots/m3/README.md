# M3 WinUI screenshots and mockup comparison

Captures of the Milestone 3 WinUI views (script editor, folder scripts, console, tests tab, script error, secret variables,
secret vault), dark and light, taken from the Native AOT build at 1440 x 900 and compared with the mockup sources in
`design/mockups/`. Method as in `docs/screenshots/m2/README.md`. Every value on screen is fake (`sk_test_FAKE_0000`,
`rt_test_FAKE_1111`, the passphrase `fake-passphrase-123`); no real secret is involved.

The `avalonia-console-*` and `winui-console-*` files in this folder are the earlier runtime checks of the console cards, kept as is.

## Regenerate

```powershell
dotnet publish src/Yaapic.App.WinUI -c Release -r win-x64 -p:PublishAot=true
pwsh tools/screenshots/Capture-M3.ps1                      # all 18 files; -Only <screen> for one screen
```

`Capture-M3.ps1` runs `Capture-WinUI.ps1` once per file (one `pwsh` process each, see `tools/screenshots/README.md`). New in
this card:

- **Throw-away vault.** The app takes `--data-dir <dir>`: the per-user data directory the vault file lives under (the default
  is `%APPDATA%`). `Capture-WinUI.ps1` passes a temp directory and deletes it afterwards, so a capture never creates or touches
  anything under `%APPDATA%\Yaapic`.
- **Vault helper step.** `secret-variables` creates a passphrase vault through the real setup dialog (`$vault` in
  `Capture-M3.ps1`: click the Passphrase radio, type the fake passphrase twice, Enter), then opens the environments dialog and
  types the two fake tokens. No vault file is prepared beforehand, so the run is repeatable.
- `Capture-WinUI.ps1` gained `-Crop x,y,w,h` (the `status-bar` files are the bottom 1440 x 68 px, the only files that are not
  1440 x 900) and the `wheel:x,y,ticks` step (scroll inside a dialog).
- The fixture workspace gained the `Lua scripted` request (Lua pre and post scripts), `Broken` (a pre-request script that throws),
  two more tests in `Scripted` (3 passed, 2 failed), and the `apiToken` / `refreshToken` secret variables plus `region` in the
  `local` environment. No menu is part of these shots, so none uses `-Screen` (dialogs are in-window and `PrintWindow` gets them).

## Deviations that apply to every screen

| # | Deviation | Class |
|---|---|---|
| G1 | The hint line next to the language selector is cut off (`Variables, tests and requests: pm.* (JavaScript a...`) and stays the JavaScript text when Lua is selected; the mockup has no such line. | M3 scope, tiny (F2) |
| G2 | Window frame, title bar and rail colours: same question as M1 F1. | see M1 F1 |

## Screens

### scripts-pre-request

[dark](scripts-pre-request-dark.png) · [light](scripts-pre-request-light.png) — `MainWindow.dc.html` (request tabs, code editor).

- `Scripts` tab with a `2` count badge (the mockup tab strip also has `Scripts 2`); `Pre-request` / `Post-response` segmented
  control, `JavaScript` combo box, mono editor with line numbers and a current-line tint.
- JavaScript colours: punctuation and the `pm` object in the text colour, property calls in amber, strings green, keywords in the
  syntax violet/red of the code tokens, same in both themes. Not compared token by token with `DSSyntaxeIcones`.
- A 3-line script leaves a large empty editor area; the Scripts tab shows the focus rectangle because the capture invokes it. G1 applies.

### scripts-post-lua

[dark](scripts-post-lua-dark.png) · [light](scripts-post-lua-light.png) — `DSSyntaxeIcones.dc.html` (syntax colours).

- Lua: `-- comment` in the muted comment colour, keywords (`local`, `function`, `end`, `for`, `in`, `do`) in the keyword colour, strings
  green, called functions amber, indent guides. The same token set as JavaScript, so the two languages read alike; the mockup has no Lua sample.
- `Post-response` shows the focus rectangle (invoked by the capture). G1: the hint still names JavaScript in the Lua view.

### folder-scripts

[dark](folder-scripts-dark.png) · [light](folder-scripts-light.png) — `DSComposants2.dc.html` (dialog).

- The collection settings dialog scrolled to its `Scripts` section: title, explanatory line, bordered editor block with the phase
  toggle, language combo and mono editor, footer band with a primary `Save` and `Close`. 640 px wide against 400 px in the mockup
  (it hosts tables and an editor), as in M2 folder-settings. The title still says `Settings` (M2 F2).
- The Scripts section sits below name, description and variables, so it is hidden until the dialog is scrolled (the capture scrolls
  it). Class: M3 scope (F3).

### console

[dark](console-dark.png) · [light](console-light.png) — `MainWindow.dc.html` (console).

- Panel open after a send of `Scripted`: header (title, `All / Log / Warn / Error`, filter box, `Clear`, `Copy`, close), then rows
  with time, level glyph, message and a right-aligned source chip: the request line (`GET ... 200 · 20 ms · 47 B`), the phase marker,
  three passed tests (green check), two failed tests (red glyph on the error tint) and the summary line. The row anatomy is the
  mockup's (tertiary time, glyph, message, source).
- The card asks for one error row; the Tests tab shot needs 3 passed and 2 failed, so this capture shows two failed-test rows.
- The mockup console is 300 px high; ours measures about 375 px. The mockup puts a coloured method label (`POST`, `GET`, `DEL`)
  on network rows and short source chips (`tests`, `script:2`, `resolver`, `network`); ours uses an arrow glyph and a repeated
  `post-response · Scripted` chip that is long. Class: M3 scope (F4).
- Long messages are ellipsised in the middle (`... with key 'X-M...`); a tooltip or wrap was not checked. Class: later milestone.

### tests-tab

[dark](tests-tab-dark.png) · [light](tests-tab-light.png) — `MainWindow.dc.html` (response tabs), `DSComposants2.dc.html` (list rows, chips).

- Response tabs `Body`, `Headers 4`, `Tests 3/5` (the `3/5` in the failure colour), the `200 OK` status chip in the success tint,
  `3 of 5 passed` with an `All / Failed` segmented control, rows with a check, name, duration and a source chip. The mockup tab
  shows the total only (`Tests 6`); ours shows passed/total, which is more informative.
- Only two of the five rows fit: the editor keeps the upper half. Class: M3 scope (F5).
- Captured 1.5 s after the send, the Tests tab was clipped to `Tests 3/`; after 4 s it is complete (the tab seems measured before
  its badge text is set). The capture waits 4 s. Class: M3 scope (F6).

### script-error

[dark](script-error-dark.png) · [light](script-error-light.png) — `States.dc.html` (error blocks).

- `Broken` aborts in its pre-request script: the response area shows the error icon on a danger-tinted disc, the title
  `No response` and `Pre-request script failed — pre-request · Broken: apiToken is not set`; the Console toggle in the status bar
  counts `1`. Same structure as the error blocks of `States` (icon disc, title, secondary text).
- No action button and no detail line with script, line and column: those are only in the console. Class: M3 scope (F7).

### secret-variables

[dark](secret-variables-dark.png) · [light](secret-variables-light.png) — `EnvVars.dc.html`.

- Environments dialog for `local` with the vault unlocked: `apiToken` revealed (`sk_test_FAKE_0000`, reveal toggle on),
  `refreshToken` masked (a fixed run of dots, which does not leak the length), `baseUrl` and `region` plain. Every row has the
  lock toggle (accent fill on the two secret rows) and the delete cross; the status bar reads `Vault unlocked`.
- The mockup pairs the table with a vault card (`3 secrets chiffrés dans <vault>`, lock now, auto-lock delay) and a warning card;
  ours has neither. Class: later milestone.
- The mockup draws a secret row's icon and dots in the tertiary colour; ours fills the lock toggle with the accent. Class: M3 scope, tiny (F8).
- Typing a value into an empty secret cell keeps only the first character: the cell becomes the read-only masked field after one
  keystroke and the rest is lost. The capture works around it (one character, Reveal, the rest). Class: M3 scope, usability defect (F1).

### vault-setup

[dark](vault-setup-dark.png) · [light](vault-setup-light.png) — `DSComposants2.dc.html` (dialog).

- `Set up the secret vault`: explanation line, `Protect the vault with` radio group (Windows account (DPAPI); Passphrase with the hint
  `You will type it each time you open the workspace`), two masked fields (reveal eye on the focused one), primary `Create vault`
  and `Cancel`. 400 px wide with the overlay surface and border of the mockup dialog. The shot has `Passphrase` selected and
  filled with the fake passphrase.
- The footer band behind the buttons that the other dialogs have (compare `folder-scripts`) is missing here, and the first
  radio row is taller than needed. Class: M3 scope, tiny (F8).

### status-bar

[dark](status-bar-dark.png) · [light](status-bar-light.png) — `MainWindow.dc.html` (footer). Crop 1440 x 68.

- Right side: `No vault` with a lock glyph and `Console` with a red `2` badge once the console has errors; the Console toggle
  shows the focus rectangle (invoked by the capture). The left side is empty, the mockup footer has more items (active
  environment, timing). Class: later milestone. The footer height was not measured against the mockup.

## Keyboard focus

Checked in the published exe with injected Tab presses and from the captures:

| Area | Result |
|---|---|
| Request editor tab strip | 2 px focus rectangle, visible in both themes |
| Scripts tab, Tab order after the tab strip | `Settings` tab, then (phase toggle) → language combo (ring plus accent bar) → editor (full-width ring) |
| Phase toggle, Console toggle | visible ring (`scripts-post-lua`, `status-bar`) |
| Vault dialog: radio group, passphrase fields | visible ring on the radio and the focused field (`vault-setup`) |
| Environments dialog: `+` button | visible ring (first stop) |

Not checked: Tab through the console header (filter box, `Clear`, `Copy`, close), the reveal and lock toggles of the variable
tables, and the unlock dialog. No screen reader was run.

## Accessibility gap list for the script editor (LoomEditor, carried over from M2-69)

- UI Automation exposes a focusable element named `Code editor` and a container named `Script` that cannot take focus, so
  assistive tools meet two elements for one control. Class: M3 scope (name it once, e.g. `Script editor`).
- Not verified: that the peer exposes its text (`TextPattern` / `ValuePattern`), caret, selection, line numbers and the current
  line, so a screen reader may announce an empty editor. Needs a manual pass with Narrator.
- Syntax colours are the only signal for tokens; contrast of the comment colour and of the current-line tint was not measured.
- The folder-scripts dialog nests the editor in a scrolling dialog: Tab order from the dialog fields into the editor was not checked.

## Runtime notes

- Scripts, tests, console, the error state and the passphrase vault (create, unlocked status, set values, reveal) all worked in
  the published AOT exe; each capture left no process, stub server or temp directory behind.
- No file was created under `%APPDATA%\Yaapic`: the vault of every run lives in the throw-away data directory.

## Follow-up (proposals, not written as cards)

- F1: a secret value cell keeps focus while typing (mask on focus loss, or after Reveal is switched off).
- F2: show the hint of the selected language, without truncation.
- F3: open the folder settings dialog on its Scripts section, or give the scripts their own tab.
- F4: console rows: short source chips (`pre-request`, `tests`, `network`), a coloured method label on network rows, 300 px height.
- F5: let the response panel grow when the Tests tab is active, or remember a dragged splitter position.
- F6: re-measure the response tab strip when the Tests badge text changes.
- F7: script error state: an `Open console` action and a detail line with script, line and column.
- F8: tiny styling: secret row colours, vault dialog footer band, radio row height.
