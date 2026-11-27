# ADR 0020: Mock servers

Status: Accepted

See also: [ADR 0005](0005-storage-format.md), [ADR 0018](0018-collection-runner.md). The mock-server cards of
milestone 6 implement this ADR; it is their reference.

## Context

Milestone 6 adds mock servers: a local HTTP server that answers with canned responses, so a client can be developed
before the real API exists. The definition must be shareable through Git like requests, the server must be testable
without UI, and it must not expose anything beyond the local machine.

## Options considered

1. **HTTP server.**
   - *Our own minimal HTTP/1.1 server on `TcpListener` bound to `IPAddress.Loopback` (chosen).* Same approach as the
     OAuth loopback receiver (`TcpLoopbackRedirectReceiver`); no URL reservation, no framework, AOT-safe, small.
   - `HttpListener`. Rejected: needs URL reservations (or admin rights) on Windows for some prefixes.
   - Kestrel. Rejected: pulls the ASP.NET Core shared framework, heavy for a loopback mock.
2. **Storage.**
   - *Workspace files `mocks/<slug>.mock.yaml` (chosen).* Shared through Git like requests, reviewable in diffs.
   - Per-user storage. Rejected: mocks would not travel with the collection.
3. **Route selection.**
   - *First enabled match in list order (chosen).* Predictable and visible in the editor; the user orders routes.
   - Most specific match. Rejected: implicit rules that are hard to explain and to test.

## Native AOT risks

None new: no reflection, no package. The definition uses VYaml `[YamlObject]` DTOs (source generated); the server uses
only `System.Net.Sockets`.

## Decision

### What a mock server is

A named local HTTP server with an ordered list of routes, stored in the workspace: `mocks/<slug>.mock.yaml`. It runs
inside Yaapic while the app is open, on `127.0.0.1` only. It is never started automatically at launch; all servers stop
when the app closes.

### File

VYaml `[YamlObject]` DTOs, `version: 1`: `id` (GUID), `name`, `port` (1024 to 65535, default 3000), `cors` (bool,
default true), `routes`. Each route: `id`, `name`, `enabled` (default true), `method` (`GET`, `POST`, `PUT`, `PATCH`,
`DELETE`, `HEAD`, `OPTIONS` or `ANY`), `path` (template, starts with `/`), `status` (100 to 599), `headers` (list of
`key`/`value`), `body` (text, literal block in YAML), `delayMs` (0 to 60 000). No secrets in mock files.

### Matching

The request path is percent-decoded and its query string removed; a trailing `/` is ignored (except for `/`). Template
segments: a literal (ordinal, case-sensitive), `:name` or `{name}` (one segment, captured), `*` (only as the last
segment: the rest of the path, captured as `*`). The first **enabled** route in list order whose method (or `ANY`) and
path match wins. A `HEAD` request also matches a `GET` route (headers and `Content-Length` sent, no body).

### Templating

Response body and header values only: `{{params.<name>}}`, `{{query.<name>}}` (first value), `{{headers.<name>}}`
(case-insensitive), `{{body}}` (the request body as UTF-8 text), and the dynamic variables `{{$guid}}`,
`{{$timestamp}}`, `{{$isoTimestamp}}`, `{{$randomInt}}` (the existing `DynamicVariableSource`). An unknown or missing
name becomes an empty string for `params`/`query`/`headers` and stays as written otherwise.

### No match

`404`, `Content-Type: application/json`, body `{"error":"No mock route matches GET /path"}`.

### CORS

When on, every response gets `Access-Control-Allow-Origin: *`. An `OPTIONS` request carrying
`Access-Control-Request-Method` and matching no `OPTIONS` route gets `204` with `Access-Control-Allow-Origin: *`,
`Access-Control-Allow-Methods: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS`, `Access-Control-Allow-Headers` (the
request's `Access-Control-Request-Headers`, or `*`) and `Access-Control-Max-Age: 600`.

### HTTP server

Keep-alive (HTTP/1.1 default, `Connection: close` honoured, HTTP/1.0 closes), `Content-Length` and chunked request
bodies, `Expect: 100-continue`. Responses always carry `Content-Length` and `Date`, plus `Server: Yaapic-Mock`. Limits:
request line and headers 64 KiB, 100 headers, body 10 MiB (`413`), 100 open connections (more are answered `503` and
closed), 30 s read and keep-alive idle timeouts. A malformed request gets `400` and the connection closes. No TLS, no
HTTP/2.

### Ports

A port in use makes the start fail with "Port {0} is already in use". The base URL shown and copied is
`http://127.0.0.1:{port}` (not `localhost`, which may resolve to `::1`).

### Log

Each running server keeps the last 500 requests: time, method, path and query, status, matched route name or "no match",
duration, the request headers and the first 4 096 characters of the request body (for the detail view). The log is in
memory only.

### Generate from a collection

Creates a mock server (name "{folder} mock", port 3000) with one route per request of a folder (depth first, tree
order): method from the request, path from its URL with the scheme, host and any leading `{{variable}}` prefix removed,
`{{name}}` segments turned into `:name`, query dropped; status 200, header `Content-Type: application/json`, body `{}`;
duplicates (same method and path) are skipped and counted.

### UI

A Mock servers window (non-modal, one at a time) opened from the palette ("Mock servers"), the folder context menu
("Create a mock server...") and the status bar item "Mocks: {n} running" (hidden when none run): server list
(start/stop, port, status), route list editor (add, duplicate, delete, move up/down, enable), response editor (status,
headers, body), the log with a detail pane, Copy URL. Edits are saved with a Save button; a saved change applies to a
running server at its next request (the server reads the current definition for each request); changing the port of a
running server restarts it.

### Out of scope

Binding to other interfaces (card M6-64, answered "not now": use a tunnel tool), HTTPS, matching on query, headers or body, response sequences or
scripts, recording a real API, generating mocks from OpenAPI, WebSocket mocks.
