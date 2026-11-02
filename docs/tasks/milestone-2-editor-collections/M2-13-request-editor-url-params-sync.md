# M2-13: Request editor ViewModel: URL and query params two-way sync

Status: done
Platform: any
Depends on: M2-09, M2-11

## Goal

Editing the URL updates the params table and editing the params table updates the URL, as in the mockups and every
API client.

## Design decisions

- `Url` stays the observable property the views bind to, but it now shows the **full URL**: the stored base URL plus the
  query of the **enabled, non-empty-key** params (`QueryStringSync.Compose`, M2-09). `ToDefinition().Url` is the URL
  **without** that query (prefix + fragment), and `QueryParams` is the table, so the resolver and files are unchanged
  (ADR 0008).
- Typing in `Url` (not triggered by a table edit): `QueryStringSync.Parse(Url)` and update the table. Rule: walk the
  existing rows; the i-th **enabled** row takes the i-th parsed entry (key and value); enabled rows left over when the
  parsed list is exhausted are removed; disabled rows stay where they are; parsed entries left over are appended
  after the last row (before the trailing placeholder). The URL text itself is never rewritten while the user types.
- Editing a row (key, value, enabled, removal) recomposes `Url` from the base prefix, the enabled params and the fragment.
  Use a private guard flag so the two directions never loop.
- On `FromDefinition`: the display `Url` is composed from the stored URL and the table. If the stored URL already holds
  an inline query (legacy), parse it and put those entries at the **front** of the table first; the baseline is taken
  after this, so a loaded request is not dirty.
- `ToDefinition` derives the stored URL by parsing `Url` and composing it with an empty param list, i.e. it drops the
  whole query (the enabled params are in the table).
- `CanSend` and the send path keep using `ToDefinition()`, so the request is sent once with its params appended by the
  resolver.

## Out of scope

Path variables (`:id`), variable autocomplete, URL validation messages.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests`: typing `https://x.test/a?b=1&c=2` fills two rows; removing `&c=2` removes the
      second row; editing a row value updates the URL; unchecking a row removes it from the URL but keeps the row;
      re-checking restores it; typing keeps disabled rows in place; fragment is kept; `{{baseUrl}}/a?id={{id}}` keeps
      the variables unescaped; a legacy stored URL with an inline query loads clean with the entries first in the table;
      `ToDefinition().Url` has no query; sending a request with params and an inline-typed query produces each pair
      exactly once (assert on the `FakeHttpTransport` request).
- [x] M1 editor tests still green; 0 warnings.

## Suggested commit message

`feat(viewmodels): sync the URL and the query params table`
