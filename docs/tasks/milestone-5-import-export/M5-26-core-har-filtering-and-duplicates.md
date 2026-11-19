# M5-26: Core: HAR import, static resources, preflights and duplicates

Status: done
Platform: any
Depends on: M5-25

## Goal

A browser HAR holds hundreds of entries that are not API calls. Filter them by default and remove exact duplicates, reporting the counts.

## Design decisions

- Read first: M5-25, M5-04 (`ImportOptions.IncludeStaticResources`).
- `src/Yaapic.Core/Import/Har/HarFilter.cs` (internal static) used by `HarEntries` before mapping: `bool ShouldSkip(DataNode entry, ImportOptions options, out HarSkipReason reason)` with `enum HarSkipReason { None, Static, Preflight, Socket, DataUrl }`.
- Rules (skip counts are aggregated per reason, one `Info` per reason per import, never one entry per skipped item):
  - **Static** (skipped unless `options.IncludeStaticResources`): `entry._resourceType` (Chrome, lower case) in `stylesheet`, `image`, `media`, `font`, `texttrack`, `manifest`, `script`, `document`... **no**: only `stylesheet, image, media, font, texttrack, manifest` are static (scripts and documents stay, they are often the page the user wants to replay);
    when `_resourceType` is absent, an entry whose `response.content.mimeType` (lower case) starts with `image/`, `font/`, `audio/` or `video/` or equals `text/css` is static. `Strings.ImportHarSkippedStatic(count)`.
  - **Preflight** (always skipped, ignoring the option): method `OPTIONS` with a request header named `Access-Control-Request-Method` (any case). `Strings.ImportHarSkippedPreflight(count)`.
  - **Socket** (always skipped): `_resourceType` `websocket` or `eventsource`, or a url starting `ws://` or `wss://`. `Strings.ImportHarSkippedSocket(count)`.
  - **Data URL** (always skipped): url starting `data:` or `blob:`. `Strings.ImportHarSkippedDataUrl(count)`.
- **Duplicates**: after mapping, a request equal to an **earlier kept** one in method, URL (with params), enabled headers (name ignoring case and value, in order) and body (kind and text, or fields) is removed; first occurrence wins. One `Info` `Strings.ImportHarDuplicates(count)` when any.
  Compare with a canonical string key built from those members (no reflection, no hashing of records with lists).
- The order of the checks: skip reasons first (in the order Static, Preflight, Socket, DataUrl, first match is the reason counted), then mapping, then duplicates. Skipped entries do not appear in the `entries[<index>]` numbering of later messages (the index is the position in the **source** array).
- Strings (EN and FR): `import.har.skippedStatic` = "{0} static resources (images, fonts, styles, media) were skipped."; `import.har.skippedPreflight` = "{0} CORS preflight requests were skipped."; `import.har.skippedSocket` = "{0} WebSocket or event stream entries were skipped.";
  `import.har.skippedDataUrl` = "{0} data and blob URLs were skipped."; `import.har.duplicates` = "{0} duplicate requests were removed.".

## Out of scope

A UI toggle for `IncludeStaticResources` (M5-51 exposes it), filtering by domain.

## Acceptance criteria

- [x] Tests: each skip reason with and without `_resourceType`; `IncludeStaticResources = true` keeps static ones but never preflights, sockets, data urls; a `script` and a `document` entry are kept; aggregated `Info` appears once per reason with the right count; duplicates (same request twice, same URL different body kept,
  header order and value compared, header name case ignored); the source index used in a later `Skipped` message after filtered entries.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): filter static resources and duplicates when importing HAR`
