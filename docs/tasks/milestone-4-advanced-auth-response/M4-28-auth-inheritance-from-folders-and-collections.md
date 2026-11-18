# M4-28: Authentication inherited from folders and collections

Status: blocked
Platform: any
Depends on: M4-27

## Open question for the product owner

Should a request be able to **inherit** its authentication from its folder or collection (Postman's "Inherit auth from parent"), so a
collection can define one OAuth 2.0 configuration used by every request?

Options:

1. **Yes, folder and collection level** (recommended): `AuthKind.Inherit` on a request (the default for new requests inside a collection), an
   `auth` block in the folder YAML file (`_folder.yaml` or whatever M2-01 named it; same shape as the request `auth` block), resolution walks
   request, then its folders upward, then the collection; the first non-`Inherit` wins; `None` stops the walk. Needs: Domain (`Inherit` kind,
   folder auth), YAML for requests and folders, the chain provider (like `IScriptChainProvider`), a resolution card, the auth editor "Inherit"
   entry and a folder settings tab on both front-ends. Roughly 8 more cards.
2. **Collection level only**: the same with one level, about 5 cards.
3. **No**: auth stays per request; users repeat it or use `{{variables}}` (already possible for every field).

Why it blocks: it changes the file format of existing workspaces (`AuthKind.Inherit` as the default for requests in a collection changes
what an old file means) and adds UI on both front-ends. The answer decides the scope; no other M4 card depends on it.

## Goal

(To be written once the question is answered; the cards will be M4-82 onward.)

## Out of scope

Everything until the answer.

## Suggested commit message

(none yet)
