# M5-25: Core: HAR import, entries to requests

Status: done
Platform: any
Depends on: M5-05, M5-06

## Goal

`HarImporter` turns the `log.entries` of a HAR file into requests in one folder. Filtering and de-duplication come in M5-26.

## Design decisions

- Read first: ADR 0017 (HAR, request naming), M5-06 (`IsManagedHeader`, `DescribeRequest`, `ContentTypeFromMime`), M5-13 (body shapes).
- `src/Yaapic.Core/Import/Har/HarImporter.cs` (`public sealed class HarImporter : IImporter`, `Id` `har`, `DisplayName` `HAR`) and `HarEntries.cs` (internal static). **Sniff**: `Certain` when the first 8192 characters contain both `"log"` and `"entries"`; `Possible` when they contain `"entries"` and `"request"`; else `None`.
  Input parsed with `DataNodeJson.Parse`; the root must have `log.entries` as an array (an empty array is allowed), otherwise `ImportException(Strings.ImportHarNotAHar)`.
- Collection folder: `FolderDefinition(NewId, SafeName(input.Name, "HAR import"), "", [], empty scripts)`; all requests are direct children, in entry order.
- **Per entry** (`entry.request`):
  - `method` through `TryParseMethod` (unsupported: `Skipped` entry with path `entries[<index>]`, continue); `url` required and non-empty, else `Skipped` `Strings.ImportHarNoUrl(index)`.
  - `Url`/params: `SplitUrl(url)` (the `queryString` array is ignored: it repeats the URL query). `Name = DescribeRequest(method, url)`.
  - **Headers**: `request.headers` array of `{name, value}`; drop every header for which `IsManagedHeader(name)` is true; keep the others in order, enabled. `Cookie` is kept as a header. `request.cookies` is ignored.
  - **Body** from `request.postData` (absent: none), `mimeType` lower case without parameters:
    - `application/x-www-form-urlencoded`: `UrlEncoded` with the `params` array (`name`, `value`; URL-decoded already in HAR) or, when `params` is absent or empty and `text` is present, the fields parsed from `text` (`&` and `=` split, URL-decoded).
    - `multipart/form-data`: `FormData` with the `params`: an element with `fileName` -> `File` field with `Value = ""` and `Warning` `Strings.ImportHarFileField(path, name, fileName)` (the file content is not in the HAR); others `Text` with `value`. The `Content-Type` header (it carries the boundary) is dropped from the headers.
    - any other `mimeType`: `Raw` with `ContentTypeFromMime(mimeType)` and `Text = postData.text` (absent: none). If the headers hold no `Content-Type` and `mimeType` is not empty, the importer adds a `Content-Type` header with the `mimeType` only when the raw kind cannot express it (`mimeType` not in the list of M5-22: `application/json`, `application/xml`, `text/xml`, `text/html`, `text/plain`, `application/javascript`, `text/javascript`).
    - `postData.text` with `encoding: base64` (response-style) is not expected; if `request.postData.comment` or `encoding` says base64, `Warning` and the body is dropped.
  - `Settings`: defaults; `httpVersion` of the entry is ignored. `Auth`: none (credentials travel in headers). Scripts: none.
- The report gets one `Info` per import `Strings.ImportHarResponsesIgnored(count)` ("Responses, timings and cookies of {0} entries were not imported.") when any entry had a `response`.
- Strings (EN and FR): `import.har.notAHar` = "This file is not a HAR file (no \"log.entries\")."; `import.har.noUrl` = "Entry {0} has no URL and was skipped."; `import.har.fileField` = "{0}: the file \"{2}\" of the field \"{1}\" is not in the HAR; choose it again before sending."; `import.har.responsesIgnored`.

## Out of scope

Filtering and duplicates (M5-26), importing responses.

## Acceptance criteria

- [x] Tests with inline HAR JSON: sniff; two entries with GET and POST JSON (exact requests, header filtering of `Host`, `Content-Length`, `:authority`, `Accept-Encoding`, kept `Cookie` and `User-Agent`); urlencoded with `params` and from `text`; multipart with a file field and the dropped content-type; raw XML;
  an exotic mimeType adds the header; a missing url skipped with its index; `TRACE` skipped; query params split; not a HAR throws; an empty entries array gives the folder only; the responses `Info` count; names via `DescribeRequest`.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import HAR entries as requests`
