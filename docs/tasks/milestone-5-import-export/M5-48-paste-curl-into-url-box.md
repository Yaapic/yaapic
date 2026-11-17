# M5-48: Paste a cURL command into the URL box

Status: blocked
Platform: any
Depends on: M5-18

## Goal

Let the user paste a `curl ...` command into the request URL field and have the whole request filled in, as other API clients do.

## Open question (product decision, answer before picking this card)

When the text pasted (or typed and committed) in the URL box of a request tab starts with `curl `, what should happen?

1. **Replace the current request** silently with the imported one (method, URL, params, headers, body, auth, settings), keeping the tab, its id and its place in the collection; the user can undo with the editor's normal edit history only if the request editor has one (it has not today), so data in the tab would be lost.
2. **Ask first**: show a small prompt "Replace this request with the pasted cURL command?" (Replace / Paste as text / Cancel), then do option 1 on Replace. Safest; one extra click.
3. **Never**: keep the URL box a plain text box; cURL import happens only through Import... (the import dialog of M5-51 has a "paste text" box).

Also: should the replacement apply to a tab that has unsaved edits, or only to an empty new request? Until answered, no other card depends on this one (the import dialog of M5-51 already accepts pasted cURL text).

## Acceptance criteria

- [ ] Rewritten after the answer; nothing to implement as is.

## Suggested commit message

`docs: record the decision on pasting cURL into the URL box`
