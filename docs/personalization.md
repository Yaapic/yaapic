# Personalization: language, accent colour and layout

These are per-user settings (`preferences.json` next to `updates.json` in the per-user data directory, never in a workspace);
language and accent are chosen in **Settings** (menu, or the command palette: "Settings", "Change language", "Change accent colour").
Decisions and alternatives: [ADR 0012](adr/0012-localization-and-accent.md).

## Language

English (fallback) and French. The default is the system language when it is French, else English. A change applies at the next
start; the dialog offers "Restart now".

How it works:

- `src/Yaapic.Localization/strings.en.json` and `strings.fr.json` hold every user-visible text, as flat dotted keys
  (`settings.title`) with `{0}`-style placeholders.
- `Strings.g.cs` is **generated** from them (`Strings.SettingsTitle`, `Strings.CommonVersion(version)`) and committed. A test fails
  when it is stale or when the two files disagree (a key in one only, different placeholders, an untranslated key).
- `Localizer` holds the current language, set once at start-up before any window exists, together with the default culture
  (dates, numbers, sizes).
- View models and Core use `Strings` members. XAML uses `{x:Bind loc:Strings.X}` (WinUI) or `{x:Static loc:Strings.X}` (Avalonia).
  Architecture tests fail on a new English literal in the view models, Core and both front-ends' XAML.

Adding a string:

1. Add the key to **both** JSON files.
2. Regenerate `Strings.g.cs`:

   ```bash
   dotnet build tests/Yaapic.Localization.Tests
   YAAPIC_UPDATE_SNAPSHOTS=1 dotnet test tests/Yaapic.Localization.Tests --no-build
   ```

   (PowerShell: `$env:YAAPIC_UPDATE_SNAPSHOTS = '1'` first.)
3. Use the new member.

Adding a language: add a `strings.<code>.json`, a `UiLanguage` value and its branch in `Localizer.Pick`/`FromCulture`, extend the
generator to emit it, add the entry to `AppLanguage` and the language list of the settings view model.

Not translated: script API names, engine log lines, release notes, error text from the OS or libraries, the docs.

## Accent colour

Presets (teal by default, blue, purple, pink, red, orange, green, grey), "Windows accent" on WinUI, or any `#RRGGBB`.

`AccentPalette` (Core) turns one colour into the accent tokens of each theme: fill, hover, pressed, border, accent text, and the
colour of text on the fill. The text on the fill is a tinted near-black or white, whichever reads better (4.5:1); accent text is
nudged until it reaches 4.5:1 on the theme background. Teal returns exactly the values of `design/tokens/yaapic.tokens.json`.

At start-up and on every change, each front-end rewrites the accent entries of its dark and light theme dictionaries
(`AccentApplier`); the list of entries comes from `AccentResourceKeys`, generated from the token file by `TokenGenerator`, so a
new accent alias in the tokens is picked up after regenerating (`YAAPIC_UPDATE_SNAPSHOTS=1` on the Design.Tokens tests).
WinUI edits the existing brushes in place; Avalonia replaces the entries.

## Layout: pane widths

The widths of three resizable panes are remembered per user in the same `preferences.json` (`sidebarWidth`, `runnerListWidth`,
`loadTestListWidth`, device-independent units, plain numbers):

| Pane | Default | Min | Max |
|---|---|---|---|
| Main window sidebar (requests, collections, history) | 272 | 200 | 440 |
| Runner request list | 360 | 280 | 520 |
| Load test request list | 360 | 280 | 520 |

Drag the splitter on the pane edge, or focus it and press Left and Right (16 units a step on WinUI and for the sidebar, 10 on the
Avalonia list splitters); a double click resets the default. The width is saved when a drag ends (or after a key press or the reset),
never on every pixel. `PaneLimits` and `PaneWidths` (Core) clamp every value, so a missing, NaN, negative or huge value in the file
falls back to the default or to the nearest limit; a width saved on a big window is also capped at run time to leave the editor
(or results) room on a small one, and comes back when the window grows. `PaneLayoutSettings` serves the loaded widths and saves a
change by merging it into the stored preferences (language, accent and code generation target are kept).
The accessible names are "Resize the sidebar" and "Resize the request list".

## Screenshots

`Capture-WinUI.ps1` and `Capture-Avalonia.ps1` take `-Language english|french` and `-Accent <preset|#RRGGBB>`, which seed a
throw-away `preferences.json`. Examples are in `docs/screenshots/localization`, `accent` and `settings`.
