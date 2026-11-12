# P-04: Localize the view model and Core texts

Status: todo
Platform: any
Depends on: P-03

## Goal

Every user-visible string produced by `Yaapic.ViewModels` and `Yaapic.Core` (about 75: banner and status texts, palette commands,
validation and error messages, console system lines) comes from the catalogue, with an English and a French value.

## Design decisions

- Use `Strings` members, not literals. View model tests assert against the English values, plus a few French checks.
- An architecture test flags a new hard-coded sentence in these assemblies.

## Out of scope

The XAML views (P-05, P-06).

## Acceptance criteria

- [ ] No user-visible literal left in the view models and Core texts; all tests green.
- [ ] French wording reviewed by the maintainer.

## Suggested commit message

`feat: localize view model and core texts`
