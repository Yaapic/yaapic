# U-03: Script editor autocompletion

Status: done (the `pm` API; the `yaapic` names join the catalogue with U-02)
Platform: any
Depends on: U-02

## Goal

Typing `yaapic.` (or `pm.`) in a script editor proposes the API members with a one-line description, and each member proposes its own
members (`yaapic.variables.` offers `get`, `set`, `has`, `unset`...). It works for JavaScript and Lua, in the request Scripts tab and in
the folder and collection scripts window, on WinUI and Avalonia.

## Design decisions

- The catalogue of members is data in `Yaapic.UI.Abstractions` (or Core), one list per language, built from the same names the engines
  expose; a test checks every catalogue entry exists on the real engine so the two cannot drift.
- Completion is driven through `LoomEditor` (`ICodeEditor`). Check the installed LoomEditor version for a completion API first; if it
  has none, raise it with the library (the user owns it) rather than building a popup in Yaapic.
- Lua uses `:` for methods where the API does; the catalogue stores the separator.
- `{{variable}}` completion (M2) is unchanged; the script editors do not offer it.

## Out of scope

Type inference on user variables, parameter hints, documentation popups beyond the one-line description, completion of the language's
standard library.

## Acceptance criteria

- [x] Catalogue for JavaScript and Lua with a test comparing it to the engines' real members (`ScriptApiCatalogueTests`).
- [x] Completion wired in both front-ends for both languages, opened by typing `.` and by Ctrl+Space (WinUI checked in the AOT build; Avalonia built and not driven by hand).
- [ ] Screenshots of the popup on both front-ends in `docs/screenshots/scripting`.

## Suggested commit message

`feat: autocomplete the yaapic API in the script editors`
