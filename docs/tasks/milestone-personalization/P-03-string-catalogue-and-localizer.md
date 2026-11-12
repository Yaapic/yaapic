# P-03: String catalogue, generator and localizer

Status: todo
Platform: any
Depends on: P-01, P-02

## Goal

The mechanism, without translating screens yet.

## Design decisions

- `strings.en.json` and `strings.fr.json` in a new `Yaapic.Localization` project; keys are dotted (`status.vault.locked`), values may
  contain `{0}`-style placeholders. A build-time generator emits a static `Strings` class (one member per key, a method for keys with
  placeholders) and one table per language. No reflection.
- `ILocalizer` (Core): the current language and `Get(key)`. `Localizer` picks the table at start from `Preferences` and the system
  language, and sets the default thread culture and UI culture.
- A missing French value falls back to English; a missing English key is a build error.

## Acceptance criteria

- [ ] Generator wired into the build of both front-ends; the AOT publish stays clean.
- [ ] Tests: key and placeholder parity between the files, fallback, system-language resolution, generated code snapshot.

## Suggested commit message

`feat: add the localization catalogue and generator`
