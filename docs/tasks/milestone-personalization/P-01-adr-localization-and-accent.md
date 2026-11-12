# P-01: ADR: localization (FR/EN) and accent colour

Status: done
Platform: any
Depends on: none

## Goal

Write `docs/adr/0012-localization-and-accent.md` recording the decisions below, so the next cards do not reopen them.

## Decisions to record

- **Languages**: English (fallback) and French. Chosen in Settings; the default is the system language when it is French, else English.
  The choice is stored per user (P-02).
- **String source of truth**: one catalogue per language (`strings.en.json`, `strings.fr.json`) turned into a generated C# class by a
  build-time generator, the same pattern as the design tokens (ADR 0006). No `.resx`, no `ResourceManager`, no reflection (Native AOT).
  A test fails when a key or a `{placeholder}` exists in one language and not in the other.
- **Where strings are resolved**: view models and Core return text through `ILocalizer` (never hard-coded English); both front-ends
  use the same catalogue so the wording is identical.
- **Switching**: the language applies at the next start in the first version (the Settings dialog says so and offers a restart);
  live switching is a later card.
- **Not translated**: script API names, engine log lines, release notes, error text coming from the OS or libraries, docs.
- **Culture**: dates, numbers and sizes follow the chosen language.
- **Accent colour**: a preset palette (teal default, blue, purple, pink, red, orange, green, grey), plus "Windows accent" on WinUI and a
  custom colour. The chosen colour drives a derived set (fill, hover, pressed, subtle, text on accent) for both themes; the text colour
  on the accent is picked to keep a 4.5:1 contrast. Applied at runtime, no restart.
- **Storage**: `preferences.json` next to the update settings (per user, not in the workspace), source-generated JSON.

## Acceptance criteria

- [x] ADR written with context, options considered, AOT risks and the decision; indexed in `docs/adr/README.md`.

## Suggested commit message

`docs: ADR for localization and accent colour`
