# M5-45: Core: Bruno export, request files

Status: todo
Platform: any
Depends on: M5-36

## Goal

`BruWriter` produces the text of a `.bru` file for one request, deterministically.

## Design decisions

- Read first: M5-31 (the format and the parsing rules: this card writes what that parser reads), M5-32 (the inverse mapping), `docs/file-format.md` ("File names").
- `src/Yaapic.Core/Export/Bruno/BruWriter.cs` (internal static): `string WriteRequest(HttpRequestDefinition request, int seq, string path, ImportReport report)`.
- **Layout**: blocks in this order, separated by one blank line, file ends with a single `\n`; indentation 2 spaces; omitted when empty: `meta`, the verb block, `params:query`, `headers`, `auth:<mode>`, the body block, `script:pre-request`, `script:post-response`.
  - `meta { name: <name> / type: http / seq: <seq> }`.
  - verb block (`get`, `post`, `put`, `patch`, `delete`, `head`, `options`): `url: <stored URL with the **enabled** params composed in>`, `body: <mode>` (`json`, `text`, `xml`, `formUrlEncoded`, `multipartForm`, `none`; Html and JavaScript raw bodies use `text`), `auth: <mode>` (`none`, `basic`, `bearer`, `digest`, `awsv4`, `apikey`, `oauth2`; `jwt` -> `none` with a `Warning`).
  - `params:query`: every param, `~` prefix when disabled, `key: value`.
  - `headers`: every header, `~` prefix when disabled.
  - `auth:<mode>` (inverse of M5-32): basic `username`, `password`; bearer `token`; digest `username`, `password`; awsv4 `accessKeyId`, `secretAccessKey`, `sessionToken`, `service`, `region`, `profileName:` (empty); apikey `key`, `value`, `placement` (`header`|`queryparams`);
    oauth2 `grant_type` (`client_credentials`|`authorization_code`|`password`), `callback_url`, `authorization_url`, `access_token_url`, `client_id`, `client_secret`, `scope`, `username`, `password`, `token_placement` (`header`|`query`), `token_header_prefix`, `token_query_key`, `credentials_placement` (`basic_auth_header`|`body`).
  - Body block: Json/Text/Xml -> `body:json`, `body:text`, `body:xml` with the text, **each line indented by 2 spaces** (empty lines stay empty); `UrlEncoded` -> `body:form-urlencoded` pairs; `FormData` -> `body:multipart-form` pairs, a file field written `key: @file(path)`;
    `Binary` -> **not exported**: body `none` and `Warning` `Strings.ExportBrunoBinaryBody(path)`.
  - Scripts: `script:pre-request` and `script:post-response` with the source indented by 2 spaces, preceded by the comment line `// Exported from Yaapic: this script uses the yaapic API, rewrite it for Bruno.`; one `Warning` per export `Strings.ExportBrunoScripts(count)`; a Lua block is skipped with `Warning` `Strings.ExportBrunoLuaScript(path)`.
- **Values**: a value containing a newline is written as a `'''` multi-line value (as in M5-31); text blocks cannot contain a line that is exactly `}` at column 0 after indentation (impossible: every line is indented by 2). A dictionary key containing `:` or starting with `~` is written with the characters kept (documented limit).
- Strings (EN and FR): `export.bruno.binaryBody` = "{0}: a binary body cannot be exported to Bruno and was left out."; `export.bruno.scripts` = "{0} scripts were exported unchanged: they use the yaapic API, not Bruno's."; `export.bruno.luaScript` = "{0}: a Lua script cannot be exported to Bruno and was left out.".

## Out of scope

The collection layout, environments and the zip (M5-46).

## Acceptance criteria

- [ ] Tests with exact expected `.bru` text for: a GET with params (one disabled) and headers; a POST with JSON body (multi-line, indented); urlencoded and multipart with a file; each auth mode; a script pair with the comment line; `Binary` dropped with the Warning; Jwt auth warning; a multi-line header value;
  the output re-read by `BruParser` (M5-31) gives the same blocks and pairs (a parser round-trip test; if M5-31 is not `done`, state it in the Notes and leave the test to M5-66).
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): write Bruno request files`
