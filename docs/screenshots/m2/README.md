# M2 WinUI screenshots and mockup comparison

Captures of the Milestone 2 WinUI views (collection tree, history, editor, variables, palette), dark and light, taken
from the Native AOT build at 1440 x 900 and compared with the mockup sources in `design/mockups/`. Method as in
`docs/screenshots/m1/README.md`. The page also records what was exercised **at runtime** in the published exe (not only
photographed): see "Runtime verification".

## Regenerate

```powershell
dotnet publish src/Yaapic.App.WinUI -c Release -r win-x64 -p:PublishAot=true
$s = 'tools/screenshots/Capture-WinUI.ps1'
foreach ($t in 'dark', 'light') {
  pwsh $s -Theme $t -Steps 'invoke:Users API'                                              -Out docs/screenshots/m2/sidebar-tree-$t.png
  pwsh $s -Theme $t -Screen -Steps 'invoke:Users API|select:Users API|keys:+{F10}'         -Out docs/screenshots/m2/tree-menu-$t.png
  pwsh $s -Theme $t -Steps 'invoke:History'                                                -Out docs/screenshots/m2/sidebar-history-$t.png
  pwsh $s -Theme $t -Screen -Steps 'invoke:Method: POST'                                   -Out docs/screenshots/m2/editor-methods-$t.png
  pwsh $s -Theme $t -Request upload-avatar -Steps 'invoke:BodyTab|invoke:BodyFormData'     -Out docs/screenshots/m2/editor-body-formdata-$t.png
  pwsh $s -Theme $t -Request secure-call -Steps 'invoke:AuthTab'                           -Out docs/screenshots/m2/editor-auth-$t.png
  pwsh $s -Theme $t -Screen -Request secure-call -Steps 'click:595,143|wait:800'           -Out docs/screenshots/m2/url-hints-$t.png
  pwsh $s -Theme $t -Steps 'keys:^+e|wait:500|invoke:Globals'                              -Out docs/screenshots/m2/environments-globals-$t.png
  pwsh $s -Theme $t -Steps 'select:Users API|keys:+{F10}|keys:{DOWN}{DOWN}{DOWN}{ENTER}|wait:800' -Out docs/screenshots/m2/folder-settings-$t.png
  pwsh $s -Theme $t -Steps 'keys:^p|wait:300|type:user'                                    -Out docs/screenshots/m2/palette-$t.png
}
```

One capture per `pwsh` process. The script gained, for this card: `-Steps` (`|`-separated: `invoke:`, `toggle:`, `select:`,
`focus:` by AutomationId or name, `keys:`, `type:`, `click:`/`rclick:`/`hover:` at window pixels, `wait:`), `-Screen`
(copies the screen instead of `PrintWindow`, needed for menu and combo-box popups, which are separate windows), history
entries generated with timestamps relative to now, and an explicit exit (the blocked stub-server runspace used to keep
the host alive for minutes). The fixture workspace now holds a `Users API` collection with a nested `Admin` folder and
a collection variable, `globals.yaml`, and two requests with a form-data body and a bearer token.

## Deviations that apply to every screen

| # | Deviation | Class |
|---|---|---|
| G1 | Same dark title bar / rail colour question as M1 (G1 there). | see M1 F1 |
| G2 | **Fixed in this card:** in the light theme, method labels and status colours (tree, tabs, picker, history, response chip) were pale because `BrushMap` looked brushes up in `Application.Resources`, which only sees the dark application theme. It now searches the theme dictionary of the window theme (`BrushMap.IsLight`, set by the main window), as `VariableTextBox` already did; all light captures were taken after the fix (`GET` is now `#0F7A5C`, `POST` `#8F5B00`, ...). Rows already on screen keep their brush when the theme is switched at runtime until they are bound again. | fix now (done), see F1 |
| G3 | `OPTIONS` is pink and bold; the mockup picker draws it in the neutral text colour with regular weight. Token `method.options`. | M2 scope (tokens are the source of truth) |
| G4 | No menu bar, breadcrumb, status bar, console. | later milestone |

## Screens

### sidebar-tree

[dark](sidebar-tree-dark.png) · [light](sidebar-tree-light.png) — `MainWindow.dc.html` (sidebar).

- Folder `Users API` expanded, nested `Admin` collapsed; folders first then requests, sorted by name; 32 px rows, method
  label column, selection tint plus the 3 px accent bar. Matches the sidebar of the mockup.
- Method labels are bold mono in the sidebar; light-theme colours: see G2.

### tree-menu

[dark](tree-menu-dark.png) · [light](tree-menu-light.png) — `DSComposants2.dc.html` (menus, list rows).

- Menu on a folder: New request, New folder, Rename, Settings, Delete, on `surface.overlay` with a 1 px overlay border
  and 8 px radius, 4 px padding, ~31 px rows. Matches the picker/menu surface of the mockup. The first item shows the
  keyboard focus rectangle (opened with Shift+F10). `Delete` is not tinted in the danger colour (the mockup tints
  destructive items). Class: M2 scope, tiny, listed in F2.

### sidebar-history

[dark](sidebar-history-dark.png) · [light](sidebar-history-light.png) — `MainWindow.dc.html` (rail, sidebar), `DSComposants2.dc.html` (list rows).

- Day headers (`Today`, `Yesterday`, ISO date), method column, URL line and a status/time/duration line with the status
  in its semantic colour. Rail shows the History entry selected (accent bar and tint).
- **Fixed in this card:** the `503 Service Unavailable` row was clipped (`33 ms` cut to `33 ı`) because the status text
  allowed 150 px in a 272 px sidebar. It is now limited to 96 px with an ellipsis (`503 Service Una…`). Re-captured.

### editor-methods

[dark](editor-methods-dark.png) · [light](editor-methods-light.png) — `DSComposants1.dc.html` (method picker).

- Seven methods, bold mono, colour per method, `surface.overlay` panel with 8 px radius. The panel is wider than the
  mockup (97 px against 140 px in the mockup, here the content width) and does not tint the current method row
  (mockup: accent at 10 %); here the focus rectangle sits on the first item instead. Class: M2 scope (listed in F2).
- `OPTIONS` colour: G3. Light colours: G2.

### editor-body-formdata

[dark](editor-body-formdata-dark.png) · [light](editor-body-formdata-light.png) — `DSComposants1.dc.html` (key/value table).

- Header row in caps (`KEY`, `TYPE`, `VALUE`) on the header surface, 30 px rows, mono key/value text, disabled row
  dimmed, text/file kind selector, `Browse` button on file rows, empty trailing row. Matches the table.
- **Fixed in this card:** the kind selector was 72 px wide and showed `Tex` instead of `Text`. The column is now 96 px and
  the selector 88 px. Re-captured.
- The mockup table has a description column and bulk-edit actions. Class: later milestone.
- Radio ring: same as M1 F2 (20 px ring with a thick centre; the light theme dot is near black).

### editor-auth

[dark](editor-auth-dark.png) · [light](editor-auth-light.png) — `DSComposants1.dc.html` (text fields).

- `Bearer Token` selected: type combo box and a masked token field (32 px, 4 px radius, 1 px border). Matches the field
  states; the auth tab shows an accent dot when an auth type is set.

### url-hints

[dark](url-hints-dark.png) · [light](url-hints-light.png) — `DSComposants2.dc.html` (chips).

- The chip strip of M2-49 was replaced by inline highlighting in M2-72: `{{baseUrl}}` resolved (success colour, tinted
  background) and `{{userId}}` unresolved (warning colour). The popup for the unresolved variable shows the title with a
  triangle icon, explanation, scope selector, value field and `Create`. The mockup draws resolved variables as indigo
  chips (`#959BFF` on 16 % indigo) and has an autocomplete list; ours uses the success/warning hues from M1-37. Class:
  M2 scope for colours (the card decisions say success/warning), autocomplete is a later milestone.
- The popup opened on click here; with a synthetic cursor move alone (no click) it did not open in the capture session,
  which may be a limit of injected input rather than of the app (see "Runtime verification").

### environments-globals

[dark](environments-globals-dark.png) · [light](environments-globals-light.png) — `EnvVars.dc.html`.

- `Globals` pinned above the environment list, then `local` (active, green dot) and `staging` (grey dot); the right pane
  has the title, the precedence hint and the variable table with checkbox, mono key and value. Matches the structure.
- Description column and environment colour/lock actions of the mockup: later milestone.

### folder-settings

[dark](folder-settings-dark.png) · [light](folder-settings-light.png) — `DSComposants2.dc.html` (dialog).

- Title `Settings`, name field with clear button (focused), multi-line description, collection variables table, footer on
  the darker band with a primary `Save` and a `Close`. Dialog is 640 px wide against 400 px in the mockup, as it hosts
  a table. The title says `Settings` where the mockup pattern names the object; class: M2 scope, F2.

### palette

[dark](palette-dark.png) · [light](palette-light.png) — `DSFondations.dc.html`, `DSComposants2.dc.html` (no dedicated artboard).

- 560 px card on a dimmed scrim, query field with clear button, results with matched characters in the accent colour,
  subtitle line (method, URL, collection path) in secondary text, category chip (`Request`, `Tab`, `Environment`,
  `Command`) and the selected row tinted with the accent bar. Tokens match the dialog/overlay surfaces.
- Rows without a subtitle (`Use environment: staging`) are shorter than rows with one, so the list height jumps while
  typing. Class: M2 scope, tiny.

## Runtime verification

Run in the published AOT exe with UI Automation and injected keys/mouse (Windows 11, unlocked session):

| Check | Result |
|---|---|
| `Ctrl+K` opens the palette, typing filters, Esc closes | works |
| `Ctrl+P` opens the palette (new in M2-76) | works |
| Rail button `Command palette` (new in M2-77) opens the palette | works |
| Palette `Enter` on `Show history` switches the sidebar to History | works |
| Palette results: requests of nested folders (`Delete user`, path `Users API / Admin`), tabs, environments, commands | works |
| History: entries grouped by day, status colours, clicking an entry opens the request as a tab with the unsaved dot | works |
| Collections: expand/collapse, nested folder, Shift+F10 context menu, folder Settings dialog | works |
| Variables: Globals in the environments dialog, collection variables in folder settings, inline highlight and popup for an unresolved variable in the URL | works (popup opened on click) |
| Form-data editor, bearer auth editor, method picker | works |
| Keyboard focus: tabs, `+` button, history items, tree rows, menus, dialog buttons, palette query field show a visible 2 px focus rectangle | works |

Not verified: hover-only opening of the variable popup (injected cursor moves did not trigger it), the palette after
`Enter` on a request or environment, drag and drop, rename and delete flows, high contrast. After the palette closes
with `Enter`, keyboard focus lands on the environment pill (visible focus ring) instead of returning to the editor
(F3).

## Follow-up (proposals, not written as cards)

- F1: refresh the brushes of bound rows (tree, history, response chip) when the theme changes at runtime, and review the
  light method colours for contrast once more (`POST` `#8F5B00` and `GET` `#0F7A5C` are readable, check `HEAD` and `OPTIONS`).
- F2: menu and picker polish: current method row tint, destructive menu items in the danger colour, object name in
  dialog titles (`Folder settings` instead of `Settings`).
- F3: restore keyboard focus to the previously focused element when the command palette closes.
- F4: check the variable popup opens on real mouse hover (manual pass) and on keyboard.
