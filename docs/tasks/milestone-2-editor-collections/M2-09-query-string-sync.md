# M2-09: Core: query string parsing and composition

Status: todo
Platform: any
Depends on: M2-01

## Goal

Pure helpers to split a URL into prefix, query entries and fragment, and to put them back together, for the URL and
params sync of M2-13.

## Design decisions

- `public static class QueryStringSync` in `Yaapic.Core.Http`, with `public sealed record UrlParts(string Prefix,
  IReadOnlyList<KeyValueEntry> Params, string Fragment)` (`Prefix` is everything before the first `?` or `#`,
  `Fragment` is from `#` included, or `""`).
- `UrlParts Parse(string url)`: the query is the text between the first `?` and the first `#` (a `?` after a `#` belongs
  to the fragment). Split on `&`, skip empty segments, split each at its first `=` (no `=` means an empty value).
  Keys and values are percent-decoded with `Uri.UnescapeDataString` (`+` stays `+`; an invalid sequence leaves the
  segment as typed). Every entry is enabled.
- `string Compose(UrlParts parts)` and `string Compose(string prefix, IReadOnlyList<KeyValueEntry> enabledParams,
  string fragment)`: `prefix`, then `?` plus `key=value` pairs joined by `&` when there is at least one param, then
  the fragment. Keys and values are escaped with `Uri.EscapeDataString`, except that every `{{name}}` token (a
  `{{`, then characters other than braces, then `}}`) is copied unescaped so variables stay readable. A param with an
  empty value is written `key=`; an entry with an empty key is skipped.
- No `Uri` parsing of the prefix: it may contain `{{baseUrl}}` or be incomplete.

## Out of scope

The ViewModel sync logic (M2-13); the resolver keeps its own query appending (unchanged).

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: parse with no query, with query and fragment, `?` inside a fragment, empty
      segments, key without `=`, percent-decoding, `+` kept; compose with zero/one/many params, with fragment,
      escaping of space `&` `=` unicode, `{{var}}` kept unescaped in key and value; `Compose(Parse(x))` equals `x` for
      canonical URLs such as `{{baseUrl}}/a?b=1&c=%20d#top`.
- [ ] 0 warnings.

## Suggested commit message

`feat(core): add query string parse and compose helpers`
