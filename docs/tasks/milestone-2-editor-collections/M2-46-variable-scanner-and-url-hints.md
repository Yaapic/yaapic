# M2-46: Variable scanner and URL variable hints

Status: todo
Platform: any
Depends on: M2-44

## Goal

The URL bar can show which `{{variables}}` the URL uses and whether each resolves.

## Design decisions

- `Yaapic.Core.Variables.VariableScanner.Scan(string? text)` returns `IReadOnlyList<string>` of the **distinct trimmed
  names** in order of first occurrence, using exactly the token rules of `VariableResolver` (`{{`, a non-empty trimmed
  name without braces, `}}`; anything else is plain text). Add a test that `Scan` and `VariableResolver.Resolve` agree on
  a table of edge cases (`{{ a }}`, `{{}}`, `{{a`, `{{{a}}}`, `{{a}}{{b}}`, repeated names).
- `sealed record VariableHint(string Name, bool IsResolved, string? Value, VariableScopeKind? Scope)` in
  `Yaapic.ViewModels.Requests`.
- `RequestEditorViewModel.UrlVariables` (`IReadOnlyList<VariableHint>`, observable, plus `bool HasUrlVariables`): for each
  scanned name, `scopes.GetScopes(Id).TryResolve(...)` (M2-40); a dynamic variable has `Value` null (it changes per
  send) and `IsResolved` true. Recomputed when `Url` changes, when the tab becomes active, and on
  `IVariableScopeProvider.ScopesChanged` **only while the tab is active** (subscribe when `IsActive` becomes true,
  unsubscribe when it becomes false, so closed tabs never leak).
- Values are shown as stored (no masking in M2; the secret vault is M3).

## Out of scope

Autocomplete, highlighting inside the text box, hints for headers and body, views (M2-49, M2-52).

## Acceptance criteria

- [ ] Tests: scanner table; hints for an env variable, a collection variable, a globals variable, an unknown name and a
      dynamic variable; they update after typing, after `RaiseScopesChanged` while active, and not while inactive; an
      empty or variable-free URL gives `HasUrlVariables` false.
- [ ] 0 warnings.

## Suggested commit message

`feat: show which variables the URL uses and whether they resolve`
