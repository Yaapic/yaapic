# M5-06: Core: import helpers (methods, URLs, names, dynamic variables)

Status: todo
Platform: any
Depends on: M5-04

## Goal

The small pure functions every importer shares, so the format cards contain only the mapping of their own fields.

## Design decisions

- Read first: ADR 0016 ("Unsupported constructs"), `src/Yaapic.Core/Http/QueryStringSync.cs`, `src/Yaapic.Domain/Http/HttpMethodKindExtensions.cs`, `src/Yaapic.Core/Variables/DynamicVariableSource.cs`.
- `src/Yaapic.Core/Import/ImportHelpers.cs`, `public static class ImportHelpers`:
  - `bool TryParseMethod(string? text, out HttpMethodKind method)`: trim, ignore case, only the seven kinds; anything else false (the importer then reports `Skipped` with `Strings.ImportMethodUnsupported(text)`).
  - `string SafeName(string? name, string fallback)`: trim, replace `\r`, `\n`, `\t` with a space, collapse runs of spaces, cut at 120 characters; empty result returns `fallback`.
  - `string DescribeRequest(HttpMethodKind method, string url)`: `"<METHOD> <host><path>"` where the scheme (`scheme://`), the query and the fragment are removed from `url`, the method upper case, at most 80 characters
    (cut, no ellipsis); `{{baseUrl}}/users` gives `GET {{baseUrl}}/users`; an empty url gives just the method.
  - `(string Url, IReadOnlyList<KeyValueEntry> Params) SplitUrl(string url)`: `QueryStringSync.Parse`, `Url = Prefix + Fragment`, `Params` as parsed (all enabled).
  - `string SubstitutePathVariables(string url, IReadOnlyDictionary<string, string> values)`: in the **path part only** (after the authority, before `?`), every segment of the form `:name` (letters, digits, `_`, `-`)
    is replaced by `values[name]` when present and non-empty, otherwise by `{{name}}`. The `:port` of the authority is untouched (`http://localhost:3000/users/:id` -> `.../users/{{id}}`).
  - `RawContentType ContentTypeFromMime(string? mime)`: the text before any `;`, lower case; contains `json` -> Json; contains `xml` -> Xml; contains `html` -> Html; contains `javascript` -> JavaScript; else Text.
  - `bool IsManagedHeader(string name)`: true for (ignoring case) `Content-Length`, `Host`, `Connection`, `Accept-Encoding`, `Transfer-Encoding`, `Keep-Alive`, `TE`, `Upgrade`, and any name starting with `:`
    (HTTP/2 pseudo-headers). Used by the HAR and cURL importers, not by Postman or Bruno (those keep the user's headers).
  - `string TranslateDynamicVariables(string text, string path, ImportReport report)`: scans `{{ name }}` placeholders (use `VariableScanner.Scan` or the same rules: trimmed name, no braces inside); a name starting with `$`:
    `$randomUUID` is rewritten to `{{$guid}}`; `$guid`, `$timestamp`, `$isoTimestamp`, `$randomInt` stay; any other `$name` stays unchanged and `report.AddOnce(Warning, path, Strings.ImportDynamicVariableUnsupported(name))`.
    Null or empty text is returned as is.
- Strings (EN and FR): `import.methodUnsupported` = "The HTTP method {0} is not supported, the request was skipped."; `import.dynamicVariableUnsupported` = "The dynamic variable {{{0}}} does not exist in Yaapic and was left as written." (mind the brace escaping of the
  catalogue format: check how an existing key prints literal braces).

## Out of scope

Format specific parsing, auth, scripts.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests/Import/ImportHelpersTests`: every method name in mixed case, `TRACE` and `CONNECT` and empty rejected; `SafeName` cases (newline, long name, empty); `DescribeRequest` for
  `https://api.example.com/users?x=1#top`, `{{baseUrl}}/users`, empty, a 200 character path; `SplitUrl` with query, without, with fragment, with `{{var}}` in the query; `SubstitutePathVariables` with a value, without, with a port, with `:id` in the query (untouched);
  `ContentTypeFromMime` for `application/json; charset=utf-8`, `application/vnd.api+json`, `text/xml`, `text/html`, `application/javascript`, `text/plain`, null; `IsManagedHeader` for each name and `:authority`;
  `TranslateDynamicVariables` for `$randomUUID`, `$guid`, `$randomFirstName` twice (one warning), text without variables, `{{normal}}`.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the shared import helpers`
