# M6-67: Core: variable overrides from environment variables and a fixed-value redactor

Status: todo
Platform: any
Depends on: M6-65

## Goal

Implement the secrets default of ADR 0022: `YAAPIC_VAR_<name>` process environment variables override variables in the environment layer,
and their values are masked like vault values.

## Design decisions

- Read first: ADR 0022 ("Secrets"), M6-62, ADR 0010 (redaction rules), `src/Yaapic.Core/Secrets/SecretRedactor.cs`,
  `src/Yaapic.Core/Variables/VariableScopes.cs`, `src/Yaapic.Domain/Http/KeyValueEntry.cs`, `EnvironmentDefinition`.
- `src/Yaapic.Core/Variables/VariableOverrides.cs`, `public static class VariableOverrides`:
  - `public const string Prefix = "YAAPIC_VAR_";`
  - `public static IReadOnlyList<KeyValueEntry> FromEnvironment(IEnumerable<KeyValuePair<string, string?>> variables)`: keeps the entries
    whose key starts with `Prefix` (ordinal, case-sensitive) and has a non-empty rest; the rest is the variable name, verbatim. A null value
    becomes `""`. Each entry is `new KeyValueEntry(name, value, Enabled: true, IsSecret: true)`. Sorted by name (ordinal). Duplicate names
    cannot happen on one OS, but if they do the last one wins.
  - `public static EnvironmentDefinition? Apply(EnvironmentDefinition? environment, IReadOnlyList<KeyValueEntry> overrides)`:
    - no overrides: returns `environment` unchanged (same instance);
    - `environment` null: returns `new EnvironmentDefinition(Guid.Empty, "", overrides)`;
    - otherwise a copy whose rows keep their order; every row (enabled or not) whose key equals an override name (ordinal) takes the
      override value and becomes `Enabled: true, IsSecret: true`; override names with no row are appended in `overrides` order.
- `src/Yaapic.Core/Secrets/FixedSecretRedactor.cs`:
  `public sealed class FixedSecretRedactor(IEnumerable<string> values) : ISecretRedactor`. Keeps the distinct values of at least four
  characters (the vault rule, `ISecretVault.GetSecretValues`), longest first, computed once in the constructor; `Redact` replaces each with
  the mask `••••` (ordinal), exactly as `SecretRedactor` does. Extract the mask into one shared `internal const` if both classes need it
  (do not duplicate the literal).
- In `VariableOverrides` also: `public static IReadOnlyList<string> FindEmptySecrets(IEnumerable<KeyValueEntry> rows)`: names of enabled
  rows with `IsSecret` and an empty value, distinct, in row order. The CLI turns them into warnings (M6-70); Core holds no text. (The
  existing `SecretRows` helper is `internal` and tied to the vault, so it is not the place.)
- No user-visible text in Core.

## Out of scope

Reading `Environment.GetEnvironmentVariables()` (the CLI passes the dictionary in, M6-70), composition (M6-68).

## Acceptance criteria

- [ ] `tests/Yaapic.Core.Tests/Variables/VariableOverridesTests.cs`: prefix filter (wrong case `yaapic_var_x` ignored, bare `YAAPIC_VAR_`
  ignored), null value, sorting, secret flag; `Apply` with no overrides (same instance), with a null environment, replacing an enabled row,
  enabling a disabled row, appending a new name, keeping row order; `VariableScopes.TryResolve` on the result: an override beats a collection
  and a globals value of the same name, a data row still beats it.
- [ ] `tests/Yaapic.Core.Tests/Secrets/FixedSecretRedactorTests.cs`: masks a value inside text, ignores values shorter than four characters,
  masks the longer of two overlapping values whole, empty and null text unchanged, no values = identity.
- [ ] `FindEmptySecrets` tests in `VariableOverridesTests`: disabled, non-secret and filled rows are not reported; a duplicate name once.
- [ ] Architecture tests (including the localization rule) green, 0 warnings, XML docs, all tests green.
- [ ] Docs: none in this card (M6-74).

## Suggested commit message

`feat(core): add YAAPIC_VAR_ variable overrides and a fixed-value redactor`
