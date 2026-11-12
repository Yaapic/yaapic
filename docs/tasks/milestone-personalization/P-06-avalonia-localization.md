# P-06: Avalonia: localize the views

Status: todo
Platform: unix
Depends on: P-04, P-05

## Goal

Same as P-05 for the roughly 250 literals of the Avalonia `.axaml` files and code-behind.

## Design decisions

- `x:Static` or compiled bindings to the generated catalogue; no reflection-based markup extension.
- The same literal-scan test as P-05.

## Acceptance criteria

- [ ] Every view in French; screenshots next to the WinUI ones; no clipped text.

## Suggested commit message

`feat: localize the Avalonia views`
