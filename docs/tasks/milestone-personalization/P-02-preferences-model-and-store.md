# P-02: Preferences model and store

Status: done
Platform: any
Depends on: P-01

## Goal

Per-user appearance preferences, loaded before the first window opens.

## Design decisions

- `Preferences` record in Core: `Language` (`AppLanguage { System, English, French }`) and `Accent` (a preset id, `System`, or a custom
  `#RRGGBB`). Defaults: System, teal.
- `IPreferencesStore` port (Core) and `FilePreferencesStore` (Infrastructure), same pattern and folder as `FileUpdateSettingsStore`:
  source-generated JSON, atomic write; unreadable or unknown values fall back to the defaults and never throw at start-up.
- An in-memory double in `Yaapic.TestDoubles`.

## Acceptance criteria

- [x] Tests: round trip, missing file, corrupt file, unknown preset id.
- [x] A custom colour that is not `#RRGGBB` falls back to the default accent.

## Suggested commit message

`feat: add the per-user preferences store`
