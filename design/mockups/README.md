# Design mockups (reference sources)

These files are verbatim copies of artboards from the Claude Design canvas "Yaapic — Identité, design system &
maquettes" (`project/*.dc.html`, version of 2026-10-30). They are the visual reference for the WinUI fidelity cards
(M1-33 onward). The canvas remains the source of truth; refresh a file here when its artboard changes.

They are **reference sources, not buildable pages**: each file is a templated HTML component for the canvas runtime
(`support.js`, not copied). `{{x}}` holes are filled by the `renderVals()` script at the bottom of the file, and the
`TH` object in that script holds the dark and light colour values. Light variants are thin wrappers
(`<dc-import name="..." theme="light">`) around the dark source. Visible copy is in French; the app copy stays in English
(see `Voix.dc.html` for tone only).

The mockups cover much more than Milestone 1 (menu bar, command palette, console, runner, auth, scripts, tests,
timeline...). Only the parts that match M1 features are implemented by the M1 cards. Artboards for other products on
the canvas are not imported.

| File | Canvas artboard | Content used by M1 |
|---|---|---|
| `MainWindow.dc.html` | 02a · Main window, dark (Windows 11) | Title bar, rail, sidebar list and filter, request tabs, request header, URL bar, Send, editor section tabs, body editor, response header, status chip, Pretty/Raw segmented control, response body |
| `MainWindowLight.dc.html` | 02a · Main window, light | Light variant of the above |
| `ResponseStates.dc.html` | 03-04 · Editor and response states, dark | Params table, body mode bar, sending state (Cancel, progress line, skeleton), 4xx and 5xx chips, response headers list |
| `ResponseStatesLight.dc.html` | 03-04 · light | Light variant |
| `EnvVars.dc.html` | 05 · Environments and variables, dark | Environment list (dot, selection), header (name, meta, actions), variables table |
| `EnvVarsLight.dc.html` | 05 · light | Light variant |
| `States.dc.html` | 17 · Empty states, errors, notifications, dark | Empty response, empty list, error blocks (timeout, DNS), warning strip |
| `StatesLight.dc.html` | 17 · States, light | Light variant |
| `DSFondations.dc.html` | 10 · Principles and foundations | Type ramp, spacing, radii, elevation, motion, density |
| `DSCouleurs.dc.html` | 11 · Colours | Semantic colours and contrast (already in `design/tokens`) |
| `DSSyntaxeIcones.dc.html` | 12 · Syntax and icons | Syntax colours, icon rules |
| `DSComposants1.dc.html` | 13 · Components: input and actions | Button variants and states, text field states, URL bar, method picker, key/value table |
| `DSComposants2.dc.html` | 14 · Components: navigation and feedback | Request tabs, panel tabs, segmented control, list rows, chips and badges, InfoBar, progress and skeleton, ContentDialog, empty state |
| `DSTokens.dc.html` | 15 · Tokens, XAML mapping and rules | Token to WinUI resource mapping, responsive rules, accessibility, do/don't |
| `Voix.dc.html` | 04 · Tone of voice | Micro-copy tone for empty states and errors |
