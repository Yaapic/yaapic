# U-04: Script snippets

Status: todo
Platform: any
Depends on: U-02, U-03

## Goal

A catalogue of ready-made scripts (set a variable from the response, status code test, JSON field assertion, bearer token refresh,
chain a request with `sendRequest`...) that the user inserts from the script editor, for both languages.

## Design decisions

- Snippets are data: name, language, phase (pre-request, post-response or both) and body, using `yaapic.*`. A snippet exists in
  JavaScript and in Lua, or in one of them only.
- Inserted from a "Snippets" button next to the editor (a flyout listing the snippets of the current language and phase), and as
  completion entries (U-03) triggered by a short prefix.
- Inserted at the caret; the editor stays the owner of the text, undo removes the whole insertion.
- Built-in only in this card; user-defined snippets are a later card.

## Out of scope

User snippets, sharing snippets, snippets of other kinds than scripts.

## Acceptance criteria

- [ ] Catalogue with at least ten snippets per language; a test runs each one against the real engine and expects no error.
- [ ] Snippets button and completion entries on both front-ends, filtered by language and phase.
- [ ] Insertion is a single undo step.

## Suggested commit message

`feat: add a script snippet catalogue to the editors`
