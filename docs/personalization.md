# Personalization: language and accent colour

Both are per-user settings (`preferences.json` next to `updates.json` in the per-user data directory, never in a workspace),
chosen in **Settings** (menu, or the command palette: "Settings", "Change language", "Change accent colour").
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

## Screenshots

`Capture-WinUI.ps1` and `Capture-Avalonia.ps1` take `-Language english|french` and `-Accent <preset|#RRGGBB>`, which seed a
throw-away `preferences.json`. Examples are in `docs/screenshots/localization`, `accent` and `settings`.
