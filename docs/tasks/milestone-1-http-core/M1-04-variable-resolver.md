# M1-04: Variable resolver

Status: done
Platform: any
Depends on: M1-03

## Goal

Replace `{{name}}` placeholders in a string from a pluggable variable source. Creates `tests/Yaapic.Core.Tests`.

## Design decisions

- Namespace `Yaapic.Core.Variables`, folder `src/Yaapic.Core/Variables/`.
- `interface IVariableSource { bool TryGet(string name, out string value); }`.
- `sealed class EnvironmentVariableSource(EnvironmentDefinition? environment) : IVariableSource`: ignores disabled entries
  and entries with an empty key; when a key appears several times the **last** enabled entry wins; case-sensitive; a null
  environment resolves nothing.
- `sealed class CompositeVariableSource(params IVariableSource[] sources) : IVariableSource`: asks sources in order, the
  first hit wins.
- `sealed record ResolveResult(string Text, IReadOnlyList<string> Unresolved)`.
- `static class VariableResolver { public static ResolveResult Resolve(string? input, IVariableSource source); }`.
- Syntax: `{{` + name + `}}`. The name is trimmed, so `{{ baseUrl }}` equals `{{baseUrl}}`. Hand-written scanner, no
  `Regex`. A name is any text without `{` or `}` that is non-empty after trimming. `{{}}`, `{{ }}` and an unterminated
  `{{` are left literally.
- Resolution is **single pass**: a substituted value is never scanned again (no recursion, no loops).
- An unresolved placeholder stays in the output exactly as typed (spaces included) and its trimmed name is added to
  `Unresolved` (distinct, in order of first occurrence).
- Null or empty input returns `ResolveResult("", [])`.
- `tests/Yaapic.Core.Tests` mirrors `Yaapic.Architecture.Tests.csproj`, references `Yaapic.Core` and `Yaapic.Domain`, and
  is added to `Yaapic.sln`.

## Out of scope

Dynamic variables (M1-05), an escape syntax, nested `{{a{{b}}}}`, scripts, secrets.

## Acceptance criteria

- [x] Tests: single and multiple placeholders; trimmed names; unresolved kept as typed and reported once; a value
      containing `{{other}}` is not re-expanded; `{{}}`, `{{ }}` and `{{abc` untouched; last enabled entry wins; disabled
      entry ignored; null environment; composite order.
- [x] 0 warnings.

## Suggested commit message

`feat(core): add variable resolver`
