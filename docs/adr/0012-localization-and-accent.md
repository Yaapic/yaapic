# ADR 0012: Localization (FR/EN) and accent colour

Status: Accepted

See also: [ADR 0006](0006-shared-design-tokens.md), [ADR 0011](0011-distribution-and-updates.md). The cards P-02 to P-13 read this ADR first.

## Context

Yaapic is English only and uses one fixed accent (teal) defined in the shared design tokens. Users want the app in French and
want to pick the accent colour. Both front-ends (WinUI 3 and Avalonia) must behave the same, and everything must stay Native AOT
clean (no reflection).

## Options considered

### Localization

| Option | Verdict |
|---|---|
| Generated catalogue from one JSON file per language | Same pattern as the design tokens: one source of truth, no runtime reflection, one test guards drift between languages. Chosen. |
| `.resx` and `ResourceManager` | Reflection and satellite assemblies; awkward with AOT and with two UI stacks. Rejected. |
| WinUI `.resw` with `x:Uid` and Avalonia `.resx` | Two different systems, two catalogues that drift, view-model texts left out. Rejected. |

### Accent colour

| Option | Verdict |
|---|---|
| Presets plus a custom colour, tokens derived at runtime | One input gives the whole set; a contrast rule keeps text readable. Chosen. |
| Presets only, each hand-tuned | Predictable but no custom colour. Kept as the look of the presets (their derived values are checked in tests). |
| Follow the system accent only | Not available on every platform and not a user choice. Offered as one option on WinUI. |

## Decision

- **Languages**: English (fallback) and French. The default is the system language when it is French, else English. The choice is
  stored per user.
- **Catalogue**: `strings.en.json` and `strings.fr.json` in a `Yaapic.Localization` project; a build-time generator emits a static
  `Strings` class (a member per key, a method for keys with `{0}` placeholders) and one table per language. A missing French value
  falls back to English; a missing English key fails the build. A test fails when a key or a placeholder exists in one language
  and not in the other.
- **Who resolves strings**: view models and Core return text through `ILocalizer`, never hard-coded English; the views use the same
  generated class, so both front-ends show the same wording. No `x:Uid`, no `.resw`, no `.resx`.
- **Switching**: the language applies at the next start. The settings UI says so and offers a restart. Switching without a restart is
  a later card.
- **Not translated**: the script API names, the engine log lines, release notes, error text that comes from the OS or a library, the docs.
- **Culture**: dates, numbers and sizes follow the chosen language (default thread culture and UI culture set at start).
- **Accent**: presets (teal default, blue, purple, pink, red, orange, green, grey), "Windows accent" on WinUI, and a custom `#RRGGBB`.
  A pure function turns the colour into the accent tokens of both themes (fill, hover, pressed, subtle background, accent text, text on
  accent); the text on the accent is black or white, whichever contrasts more, and the accent text is nudged to 4.5:1 on the theme
  background. The teal preset must reproduce today's token file. The result replaces the accent resources of the running app, with no restart.
- **Storage**: `preferences.json` next to `updates.json` in the per-user data directory (not in the workspace), source-generated JSON,
  atomic write; unreadable or unknown values fall back to the defaults.

## AOT risks

None expected: the generator runs at build time, the catalogue is plain static data, the colour maths is arithmetic, and the JSON uses
a source-generated context. The accent is applied by setting resource entries, which both XAML stacks support without reflection.
