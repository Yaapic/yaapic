# Automation: Runner, load tests, mock servers and monitors

Milestone 6 adds four tools that work on the requests you already have. Each one has an entry on the left rail (under
Collections and History) and opens in the main window, in place of the request tabs; the folder context menu, the command palette
and the status bar lead to the same views. Collections or History, or opening a request, brings the request tabs back as you left
them. A tool keeps working while another view is shown. Design decisions:
[ADR 0018](adr/0018-collection-runner.md) (Runner), [ADR 0019](adr/0019-load-tests.md) (load tests),
[ADR 0020](adr/0020-mock-servers.md) (mock servers) and [ADR 0021](adr/0021-monitors.md) (monitors); internals in
[architecture.md](architecture.md) and [ADR 0023](adr/0023-automation-views-in-main-window.md) (views in the main window).

| Tool | Rail | Folder context menu | Palette | Status bar |
|---|---|---|---|---|
| Runner | Runner icon, tooltip "Runner" | Run folder... | Run collection... | Runner (shows `3/20` during a run) |
| Load test | Load test icon, tooltip "Load test" | Load test... | Load test... | none |
| Mock servers | Mock servers icon, tooltip "Mock servers" | Create a mock server... | Mock servers | Mocks: {n} running (hidden when none) |
| Monitors | Monitors icon, tooltip "Monitors" | Monitor folder... | Monitors | Monitors: {n} failing (hidden when 0) |

## Runner

The Runner sends every request of one folder (a collection or a nested folder), in order, as many times as you ask. It sends
exactly like the request editor: authentication, client certificates, scripts, redaction of secrets. Runner sends are not
recorded in the history.

**What runs.** The requests of the folder, depth first in tree order (sub-folders first, then requests, each by name). Unfiled
requests cannot be run. In the setup you can untick requests and move them up and down; that order is kept while Yaapic
runs (runner configurations are not saved).

**Choosing the folder.** The setup pane starts with a folder selector (every folder of the collections, by path). From the
rail, the status bar or the palette it shows "Choose a folder to run" and Run stays disabled until you pick one; from a folder's
context menu the folder is already selected. You can change the folder at any time except while a run is going on. The pane
beside the results can be resized by dragging the splitter or with the Left and Right arrow keys (a double click resets it); the
width is remembered across restarts, as is the one of the main window sidebar (see [personalization](personalization.md)).

| Option | Range | Default |
|---|---|---|
| Iterations | 1 to 10 000 | 1 (the row count when a data file is chosen) |
| Delay between two requests | 0 to 60 000 ms, never after the last request | 0 |
| Stop on failure | on or off | off |
| Keep variable values | on or off | on |

The active environment is used; switching it during a run affects the requests that follow. With "Keep variable values" on, the
variables your scripts change are saved after each request, as in the editor. With it off they are kept only for the run: later
requests still see earlier changes, but nothing is written to the environment, collection or globals.

**Outcomes.** `Passed`; `Failed` (at least one test failed); `Error` (the send failed or a pre-request script aborted it);
`Skipped` (`yaapic.execution.skipRequest()`). A 4xx or 5xx status is not a failure by itself: your tests decide. The run
ends as `Completed`, `Stopped` (a script called `setNextRequest(null)`), `Stopped on the first failure`, `Canceled` (Stop) or
`Failed` (an unknown `setNextRequest` name or the loop guard).

### Data files

Pick a CSV or JSON file to run one iteration per row. The Runner previews the column names and the first five rows. Iteration
`i` (starting at 0) uses row `i`; past the end it keeps using the last row. The format comes from the extension, else from the
first character (`[` means JSON). Limits: 10 MiB, 100 000 rows, 1 000 columns.

CSV (header row, comma separator, `"` quoting with `""` as an escape, UTF-8):

```csv
user,note
alice,"likes tea, not coffee"
bob,plain
```

JSON (an array of objects; strings as they are, numbers and booleans as their JSON text, `null` as an empty string, nested
objects and arrays as compact JSON):

```json
[
  { "user": "alice", "age": 31, "tags": ["a", "b"] },
  { "user": "bob", "age": 28, "tags": [] }
]
```

**Data variables.** Each column is a variable of the current iteration. `{{user}}` in a URL, header or body resolves from the
row; the data layer sits between local and environment values (see [file-format.md](file-format.md#variable-precedence)) and is
read-only. Outside a Runner run it is empty. In scripts:

```js
const user = yaapic.iterationData.get('user');   // string, or undefined
yaapic.iterationData.has('age');                 // true
yaapic.iterationData.toObject();                 // the whole row
yaapic.info.iteration;                           // 0-based
yaapic.info.iterationCount;
```

In Lua the same calls exist as `yaapic.iteration_data.get('user')` (`nil` when missing).

### Flow control

- `yaapic.execution.setNextRequest('Name or id')` (alias `postman.setNextRequest`): after the current request, jump to the first
  request of the run with that name (or id). The last call of a request wins. `setNextRequest(null)` stops the whole run after
  the current request. An unknown name ends the run as `Failed`. More than 1 000 sends in one iteration stop the run (loop guard).
- `yaapic.execution.skipRequest()`: in a pre-request script only; nothing is sent and no post-response script runs. It also works
  in the request editor ("Request skipped by a script"). In a post-response script it throws.
- Outside the Runner `setNextRequest` does nothing and adds a console warning.

### Results and reports

The results pane shows the summary tiles (sent, skipped, errors, failures, tests passed and failed, average time, total time),
the iterations and one row per request, a filter (All, Passed, Failed, Skipped) and the details of the selected row (status,
time, size, tests, console, error). The console lines also go to the console panel. Only one run at a time; showing another view
does not stop it.

Reports list the URL **template** of each request, never the resolved URL, and console lines and test messages are redacted, so
secret values do not reach a report.

- **JSON** (`"schema": "yaapic-run/1"`): the plan, every iteration and request, and the summary. UTF-8, `\n` line ends.
- **JUnit XML**: one `<testsuite>` per request per iteration, named `Iteration {n} / {path}/{name}`, one `<testcase>` per test.
  A request in `Error` is a `<testcase name="request">` with an `<error>`. Most CI systems read it directly, for example
  GitHub Actions with a test-report action, Jenkins with the JUnit plugin, GitLab with `artifacts:reports:junit`. Yaapic has
  no command-line runner yet, so the file is exported by hand from the Runner (see "Not delivered").

## Load tests

A load test has several virtual users send the folder's requests in a loop for a fixed time. It sends through the same step as
the Runner, so scripts run (about 5 ms of one core per JavaScript run), which caps throughput at roughly 1 000 to 1 600 requests
per second whatever the number of users ([spike](spikes/m6-load.md)).

| Setting | Range |
|---|---|
| Virtual users | 1 to 100 |
| Duration | 10 to 3 600 s |
| Profile | Fixed (all users from t = 0) or Ramp-up (users grow from 1 to the target over 1 s up to the duration) |
| Think time | 0 to 10 000 ms between two requests of a user (default 0) |
| Data file | optional, same files as the Runner; rows are handed out round-robin, one per user iteration |

The folder selector, request list, selection and order work as in the Runner setup (no folder is chosen from the rail until you pick one; the folder cannot change during a test). Variable changes made by scripts are never saved, and
nothing goes to the history or the console. `setNextRequest` is ignored; `skipRequest` skips.

**Confirmation.** The first start after Yaapic starts asks "Start a load test against {hosts}?" and shows the target hosts.
Only load servers you are allowed to load.

**What counts as an error.** A sample (one request) is an error when the send failed, a pre-request script aborted it, a test
failed, or the status is 400 or more (unlike the Runner, HTTP errors count). Skipped requests are not samples.

**Metrics.** Overall and per request: count, errors, error rate, min, average, max, p50, p90, p95, p99 (nearest rank), and
throughput (count divided by elapsed seconds). The view shows live tiles (requests, throughput, average, p95, error rate,
active users) and four charts (requests per second, average response time, errors per second, active users). At the end of the
duration no new request starts and in-flight ones finish; Stop cancels them at once.

**Report.** JSON, `"schema": "yaapic-loadtest/1"`: the plan, overall and per-request metrics, per-second buckets and the first 20
distinct error messages; URL templates only.

**Limits.** At most 2 000 000 samples are kept for percentiles; past that the counts and min, average and max keep updating and the
report says "percentiles computed on the first 2000000 samples". No ramp-down, no distributed load.

## Mock servers

A mock server is a small local HTTP server that answers with canned responses. It runs inside Yaapic, on `127.0.0.1` only (so
nothing else on your network can reach it), only while the app is open, and is never started at launch. All servers stop when
the app closes. Closing Yaapic while a Runner run or a load test is in progress asks first, since its results would be lost;
running mock servers stop without a question. The base URL shown and copied is `http://127.0.0.1:{port}`.

**Definition.** One file per server in the workspace, `mocks/<slug>.mock.yaml`, shared through Git (format and an example:
[file-format.md](file-format.md#mock-server-file)). A server has a name, a port (1024 to 65535, default 3000), a CORS switch (on
by default) and an ordered list of routes: name, enabled, method (`GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS` or
`ANY`), path, status (100 to 599), headers, body and delay (0 to 60 000 ms). Do not put secrets in a mock file.

**Matching.** The first enabled route, in list order, whose method and path match wins. The request path is percent-decoded,
its query string is ignored and a trailing `/` is ignored. Path segments are literals (case-sensitive), `:name` or `{name}`
(one segment, captured) or `*` as the last segment (the rest of the path). A `HEAD` request also matches a `GET` route and gets
the headers without the body. Matching on query, headers or body is not supported.

**Templates.** Response bodies and header values can use `{{params.name}}`, `{{query.name}}` (first value),
`{{headers.name}}` (case-insensitive), `{{body}}` (the request body as text) and the dynamic variables `{{$guid}}`,
`{{$timestamp}}`, `{{$isoTimestamp}}` and `{{$randomInt}}`. A missing `params`, `query` or `headers` name becomes an empty string;
any other unknown name stays as written. Example body for `GET /users/:id`:

```json
{ "id": "{{params.id}}", "verbose": "{{query.verbose}}", "requestId": "{{$guid}}" }
```

**No match.** `404` with `Content-Type: application/json` and `{"error":"No mock route matches GET /path"}`.

**CORS.** With CORS on, every response carries `Access-Control-Allow-Origin: *`. A preflight `OPTIONS` request (with
`Access-Control-Request-Method`) that matches no `OPTIONS` route gets `204` with the allow headers and `Access-Control-Max-Age: 600`.

**Log.** Each running server keeps the last 500 requests in memory: time, method, path, status, matched route (or "no match"),
duration, request headers and the first 4 096 characters of the body. The log is lost when the server stops.

**Generate from a folder.** "Create a mock server..." on a folder creates "{folder} mock" (port 3000) with one route per request:
the request's method, its path (scheme, host and a leading `{{variable}}` prefix removed, `{{name}}` segments turned into `:name`,
query dropped), status 200, `Content-Type: application/json` and body `{}`. Requests that would repeat a method and path are
skipped, and the number skipped is reported.

**Editing.** Changes are applied with Save. A saved change reaches a running server at its next request; changing the port of a
running server restarts it. If the port is taken, the start fails with "Port {n} is already in use".

**Limits.** Request line and headers 64 KiB, 100 headers, body 10 MiB (413 above), a 30 s read timeout per request (this also
closes idle keep-alive connections), no TLS and no HTTP/2. Known limit: a server accepts at most 100 open connections
(`TcpMockHttpHost.MaxConnections`). A connection over the cap is answered `503` and closed without reaching the routes, so
those 503 answers are **not** in the log: a client that opens too many connections sees 503 and the log shows nothing about it.
Idle connections count towards the cap until they close, so close or reuse connections in a client that makes many requests.

## Monitors

A monitor runs a folder on a schedule and tells you when it starts failing. It runs **only while Yaapic is open on this
computer**: there is no cloud and no background service, and nothing runs when the app is closed.

- **Definition:** `monitors/<slug>.monitor.yaml` ([format](file-format.md#monitor-file)): name, folder, environment (fixed, or none;
  never "the active one", so the monitor does not change when you switch environments), interval (5, 10, 15, 30, 60, 180, 360, 720
  or 1440 minutes, default 60) and enabled. The file is committed with the workspace, so a teammate who opens it runs the enabled
  monitors too.
- **A run** is a Runner run with one iteration, no data and no delay. Variable changes are never saved, runs are not recorded in
  the request history and their console lines do not go to the console panel. Secret values come from the vault when it is
  unlocked.
- **Schedule:** every 30 seconds the scheduler starts the monitors that are due (never run, or `last run + interval` reached).
  One run at a time across all monitors. A monitor that became overdue while the app was closed runs once when the app starts (no
  catch-up). "Run now" works even for a disabled monitor.
- **Status:** `Passed` (every request passed or was skipped), `Failed` (a request failed or errored) or `Error` (the run could not
  start, such as a deleted folder or environment, or the run itself failed, such as a `setNextRequest` loop).
- **History:** per user, never committed, in `.yaapic/monitor-runs.jsonl` ([format](file-format.md#monitor-run-history)). Each
  line holds counts, the status, a message and at most 20 failures (request name, outcome, status, first failed test or error);
  never bodies or headers. The last 200 runs per monitor are kept, and deleting a monitor deletes its history. The Monitors view shows the
  last 50 runs and the failures of the selected one.
- **Failures:** a failed run adds the console warning "Monitor '{name}' failed: {reason}", and the status bar shows
  "Monitors: {n} failing" (enabled monitors whose last run is not `Passed`). There are no operating-system, e-mail or webhook
  notifications.

## Troubleshooting

- **"Port {n} is already in use" when starting a mock server.** Another program (or another mock server) holds the port. Pick
  another port in the editor, or stop the other server.
- **A data file is refused.** The Runner shows the reader's message. Check the limits (10 MiB, 100 000 rows, 1 000 columns),
  that a CSV starts with a header row, and that a JSON file is valid and is an array of objects. Fix the file and choose it
  again.
- **A run stops with "no request named '...'".** `setNextRequest` takes the name (or id) of a request that is **in this run**:
  an unticked request, or a name typed differently (the match is exact), is not found.
- **"The run was stopped: more than 1000 requests in one iteration".** A `setNextRequest` chain loops. Make the loop end, for
  example by counting in a variable and calling `setNextRequest(null)`.
- **A monitor fails with blank secrets, or an `{{variable}}` stays unresolved.** The vault is locked: secret values are blank
  until it is unlocked. Unlock the vault, then use "Run now".
- **A monitor reports `Error` with "folder (or environment) no longer exists".** Open the monitor and pick an existing folder
  or environment.
- **A client sees 503 from a mock server but the log is empty.** See the connection cap under "Mock servers".

## Not delivered in milestone 6

HTML reports, saved runner and load-test configurations, spike and peak load profiles, thresholds, HTTPS mocks, mock matching on
query, headers or body, operating-system notifications and cron schedules are not implemented.

- **Command-line runner:** decided (`yaapic-cli`, a separate executable for CI, task M6-62) but not built yet (tasks M6-65 to
  M6-74); until then reports are exported from the Runner.
- **Monitors while Yaapic is closed:** not planned for now (M6-63). Monitors run only while the app is open; there is no tray mode and
  no background service.
- **Mock servers reachable from other devices:** not planned for now (M6-64). Mocks listen on `127.0.0.1` over HTTP only; to reach one
  from a phone, a virtual machine or a teammate, forward the port with a tunnel tool of your choice.

See the milestone 6 summary in [tasks/README.md](tasks/README.md).
