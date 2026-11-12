# P-03: String catalogue, generator and localizer

Status: done
Platform: any
Depends on: P-01, P-02

## Goal

The mechanism, without translating screens yet.

## Design decisions

- `strings.en.json` and `strings.fr.json` in a new `Yaapic.Localization` project; keys are dotted (`status.vault.locked`), values may
  contain `{0}`-style placeholders. A build-time generator emits a static `Strings` class (one member per key, a method for keys with
  placeholders) and one table per language. No reflection.
- `Localizer` (static, in `Yaapic.Localization`): holds the current language, set once at start-up, with the default culture; `Strings` members read it. Generated `Strings.g.cs` is committed and guarded by a snapshot test (`YAAPIC_UPDATE_SNAPSHOTS=1` regenerates), like the design tokens.
  language, and sets the default thread culture and UI culture.
- A missing French value falls back to English; a missing English key is a build error.

## Acceptance criteria

- [x] Generator wired into the build of both front-ends; the AOT publish stays clean.
- [x] Tests: key and placeholder parity between the files, fallback, system-language resolution, generated code snapshot.

## Suggested commit message

`feat: add the localization catalogue and generator`
