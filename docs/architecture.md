# Architecture

Yaapic is a layered solution. Dependencies only point downwards; `tests/Yaapic.Architecture.Tests`
(`DependencyRulesTests`) enforces the table below by reading the `ProjectReference` items of every csproj.

## Projects and allowed references

| Project | May reference |
|---|---|
| `Yaapic.Domain` | nothing |
| `Yaapic.Core` | `Domain` |
| `Yaapic.Infrastructure` | `Core`, `Domain` |
| `Yaapic.Scripting` | `Core`, `Domain` |
| `Yaapic.UI.Abstractions` | nothing |
| `Yaapic.Design.Tokens` | nothing |
| `Yaapic.ViewModels` | `Core`, `UI.Abstractions` (never `Infrastructure`) |
| `Yaapic.Composition` | `Core`, `Domain`, `Infrastructure`, `Scripting`, `UI.Abstractions`, `ViewModels` |
| `Yaapic.App.*` (WinUI, Avalonia) | `Composition`, `UI.Abstractions`, `ViewModels`; no business logic |

No library references an App project.

## Request flow

```
RequestEditorViewModel.SendCommand
  -> RequestSender            (Core: use case)
     -> RequestResolver       (Core: {{variables}} resolved by scope, highest first: script-local values, the Runner
                               iteration data row (empty outside a run), active environment, collection variables,
                               globals, dynamic variables; the scopes come from IVariableScopeProvider;
                               unresolved names are reported, they never block the send)
     -> IHttpTransport        (Core port, implemented by HttpClientTransport in Infrastructure)
  -> SendReport               (result + unresolved variables)
  -> ResponsePanelViewModel   (status, time, size, Pretty/Raw body, headers, errors)
```

Every completed send raises `Sent`; the composition root records it through `HistoryViewModel` (`IHistoryStore`).
Cancellation maps to a failed result "Request canceled"; any other transport error becomes a failed result too.

## Scripted request flow

When the composition root provides `ScriptSendServices` (always, in the apps), the editor sends through the scripts
([ADR 0009](adr/0009-scripting.md)); the plain flow above runs inside it.

```
RequestEditorViewModel.SendCommand
  -> ScriptedRequestSender        (Core)
     -> chain                     (IScriptChainProvider: collection, folders outermost to innermost, request)
     -> pre-request runs          (IScriptEngine per block; a failed block aborts the send, nothing is sent)
     -> RequestSender             (the flow above, on the possibly mutated definition)
     -> post-response runs        (same order; a failed block never discards the response)
  -> ScriptedSendReport           (SendReport, runs, variable changes, warnings, network log)
     -> ResponsePanelViewModel    (response; TestResultsViewModel: tests and script errors)
     -> ConsoleViewModel          (console lines of every run, the network lines, system warnings)
     -> IScriptVariableApplier    (persists the environment, collection and globals changes through the stores)
```

`ScriptedRequestSender` redacts every console line, test result and script error with the `ISecretRedactor` before it
leaves the report. `VariableScopeProvider` implements `IScriptVariableApplier`; `CollectionTreeScriptChainProvider`
(ViewModels) builds the chain from the collection tree.

### Engines and the host bridge

`IScriptEngine`, `IScriptHost`, `ScriptInvocation`, `ScriptLimits` and `ScriptEngineRegistry` are in `Yaapic.Core.Scripting`.
`Yaapic.Scripting` holds the adapters: `QuickJs.QuickJsEngine` (QuickJS-NG) and `Lua.LuaEngine` (Lua 5.4), both bound with
`LibraryImport` to native libraries built by `native/Build-Natives.ps1` (see [dependencies.md](dependencies.md)). A script
sees one native function, `__host(op, argsJson) -> resultJson`; `yaapic`, `console` and `expect` are preludes written in the
script language (C# `const string`s). `HostBridge` decodes the arguments with the source-generated `HostJsonContext`,
calls the typed `IScriptHost` and answers `{"ok":...}` or `{"err":"..."}`. The operations (`HostOps`) are `var.*`
(get, set, unset, has, clear, list, replace), `req.get`/`req.set`, `res.get`, `info.get`, `console`, `console.plain`, `test`,
`send` (`yaapic.sendRequest`), `cookies` and `unsupported`. `Yaapic.Composition` registers both engines; a library that cannot
be loaded makes that language report itself unavailable, the rest of the app keeps working.

## Secret vault

[ADR 0010](adr/0010-secret-vault.md). The ports are in `Core.Secrets`: `ISecretVault` (state `NotCreated`, `Locked`,
`Unlocked`; get, set, remove by `SecretRef`), `IKeyProtector` and `IKeyProtectorFactory`, `ISecretRedactor`.
`Infrastructure.Secrets.FileSecretVault` implements the vault as one encrypted file per user and workspace (AES-256-GCM,
`SecretCipher`), located by `IVaultLocation` (`DefaultVaultLocation`). The data key is wrapped by an `IKeyProtector`:
`PassphraseKeyProtector` (PBKDF2, every platform), `DpapiKeyProtector` (Windows), `MacKeychainKeyProtector` and
`SecretToolKeyProtector` (macOS, Linux); `DpapiKeyProtectorFactory`, `UnixKeyProtectorFactory` and
`PassphraseKeyProtectorFactory` list what is available. The App projects pass the platform factory to
`AppComposition.CreateShell` (`DpapiKeyProtectorFactory` on Windows, `UnixKeyProtectorFactory` elsewhere).

The stores are decorated in the composition root: `SecretAwareEnvironmentStore`, `SecretAwareGlobalsStore` and
`SecretAwareCollectionStore` (Core) blank secret rows on save and fill them from the vault on load. `SecretRedactor`
masks the values held by the vault in script output; a locked vault yields no values, so nothing is masked and the
secret variables stay unresolved.

## View models added by scripting

`ScriptsEditorViewModel` (ViewModels/Scripting: phase, language and source of one script set, used by the request
editor and the folder settings), `ConsoleViewModel` (filterable console lines, status bar counters), `TestResultsViewModel`
(response "Tests" tab) and `VaultViewModel` (vault state, setup, unlock, lock, protector choice).

## Storage

`Core.Storage` defines the ports `IRequestStore`, `IEnvironmentStore`, `ISessionStore`, `ICollectionStore` (folder tree,
moves), `IGlobalsStore` and `IHistoryStore`. `Infrastructure` implements them in the workspace folder: YAML files
(`FileRequestStore`, `FileEnvironmentStore`, `FileSessionStore`, `FileCollectionStore`, `FileGlobalsStore`) and an
append-only JSON Lines file (`FileHistoryStore`); see [file-format.md](file-format.md) and
[ADR 0008](adr/0008-collections-globals-history-storage.md). `Composition.AppComposition.CreateShell` wires the whole
graph for every App project.
It builds once and shares the M4 services: the OAuth 2.0 token service (in-memory cache, loopback redirect receiver,
system browser), the request authenticator, the client certificate provider and store, the HTTP/3 support probe and the
six code generators. The `HttpClientTransport` and the certificate provider are owned by the shell and disposed when it
closes; the shell creates the certificates and JWT decoder view models on demand (`CreateCertificates`, `CreateJwtDecoder`).

## Authentication, transport and response tools (milestone 4)

Decisions: [ADR 0013](adr/0013-advanced-authentication.md), [ADR 0014](adr/0014-transport-certificates-timings.md),
[ADR 0015](adr/0015-response-diff-and-code-generation.md); user level: [authentication.md](authentication.md) and
[response-tools.md](response-tools.md).

```
RequestSender
  -> RequestResolver          Basic, Bearer, API key resolved at once; OAuth 2.0, JWT, Digest and AWS v4 are
                              substituted and kept in ResolvedRequest.PendingAuth
  -> IRequestAuthenticator    (Core: RequestAuthenticator, one IAuthMethod per kind: JwtAuthMethod, DigestAuthMethod,
                               AwsV4AuthMethod, OAuth2AuthMethod; an error is SendFailureKind.AuthenticationFailed)
       |-- OAuth2AuthMethod -> IOAuth2TokenService -> IOAuth2TokenCache (memory), IExternalBrowser,
       |                       ILoopbackRedirectReceiver (authorization code with PKCE)
       `-- DigestAuthMethod -> IHttpTransport (the body-less probe)
  -> IHttpTransport           (HttpClientTransport)
       |-- IClientCertificateProvider  host pattern match, certificate cache, one HttpClient per certificate (mTLS)
       |-- TimingCollector             per-request, in HttpRequestMessage.Options; ConnectCallback times DNS and
       |                               connect, PlaintextStreamFilter ends TLS; Wait and Download from the headers
       `-- HttpVersionMapping          HttpVersionPreference -> version and policy (IHttpVersionSupport for HTTP/3)
  -> HttpResponseData         status, body, Protocol, Timings (RequestTimings)
```

Response side: `ResponseDiffer` and `LineDiff` (Core.Diff, Myers) compare two `ResponseSnapshot`s for
`ResponseDiffViewModel`; `JwtDecoder` (Core.Auth.Jwt) feeds `JwtDecoderViewModel`; `CodeRequestFactory` turns a `ResolvedRequest`
into a `CodeRequest` and `CodeGeneratorRegistry` offers the six `ICodeGenerator`s (cURL, JavaScript, Python, C#, PowerShell, Go)
to `CodeGenerationViewModel`. Client certificates are listed in `ICertificateStore` (`FileCertificateStore`,
`certificates.json` outside the workspace) and their passphrases are vault entries of kind `Certificate`.

## Import and export (milestone 5)

Decisions: [ADR 0016](adr/0016-import-export-architecture.md), [ADR 0017](adr/0017-import-export-formats.md), the
[YAML spike](spikes/m5-yaml-tree.md); user level: [import-export.md](import-export.md).

```
Import:
IImportInputReader (Infrastructure: file, folder, pasted text; size limits)
  -> ImportService.Preview     detects the format through ImporterRegistry (Sniff), runs one IImporter, writes nothing
       |-- importers (Core.Import.<Format>): PostmanImporter, PostmanEnvironmentImporter, CurlImporter, OpenApiImporter
       |   (SwaggerNormalizer first), HarImporter, InsomniaImporter, BrunoImporter
       |-- DataNode                the neutral tree every importer reads (DataNodeJson from JsonDocument,
       |                           DataNodeYaml through StructuredTextReader and IYamlTreeReader, VYaml in Infrastructure)
       `-- ImportHelpers, ScriptConverter (Postman and Bruno scripts)
  -> ImportBundle + ImportReport (ImportEntry: Info, Warning, Skipped)
  -> ImportService.Apply -> ImportApplier -> ICollectionStore, IEnvironmentStore, IGlobalsStore (secret-aware, vault)

Export:
ExportSourceReader (reads the stores, blanks every secret value, reports the count)
  -> IExporter (ExporterRegistry): Postman collection and environment, OpenAPI JSON and YAML, Insomnia, Bruno
  -> ExportResult (ExportFile list + report) -> ExportPackager (one file as is, several as a .zip)
  -> IDialogService.SaveFileAsync
```

`ImportViewModel` (source, format override, preview, `ImportReportViewModel` rows with a severity filter) and `ExportViewModel`
(collection, format, environments, globals) live in `Yaapic.ViewModels`; the shell opens them from the sidebar, the context
menu of a top-level folder and the palette (`RequestImport`, `RequestExport`). Both front-ends only render them (WinUI and
Avalonia import and export windows). Importers and exporters are pure Core code: no file access, no network, no reflection.
`IDialogService` gains `PickFolderAsync` for Bruno folders.

## Automation (milestone 6)

Decisions: [ADR 0018](adr/0018-collection-runner.md), [ADR 0019](adr/0019-load-tests.md), [ADR 0020](adr/0020-mock-servers.md),
[ADR 0021](adr/0021-monitors.md), the [load spike](spikes/m6-load.md); user level: [automation.md](automation.md). No new package.

```
Runner:
RunnerViewModel (setup, run, results, exports)
  -> CollectionRunner                  (Core.Runner: iterations, delay, stop, cancel, setNextRequest, loop guard)
     -> RunStepExecutor                one request: scripts, send, outcome, RunRequestResult
        -> ScriptedRequestSender       the scripted flow above (same pipeline as the editor)
     |-- RunVariableState              the run's own copy of script changes, re-applied on top of the scopes
     |-- IRunScopeSource               GetScopes before each request (VariableScopeRunSource in ViewModels, over the stores)
     `-- IterationDataReader           CSV and JSON rows (IImportInputReader reads the file) -> the data variable layer
  -> RunResult -> RunReportJson ("yaapic-run/1"), RunReportJUnit (XmlWriter) -> IDialogService.SaveFileAsync

Load test:
LoadTestViewModel -> LoadTestRunner    (Core.LoadTesting: one task per virtual user)
  |-- VirtualUserSchedule              fixed and ramp-up user counts over time (pure)
  |-- RunStepExecutor + RunVariableState (one state per user, changes never persisted)
  `-- LoadMetrics                      thread-safe: samples, percentiles, per-second buckets, snapshots every second
  -> LoadTestReportJson ("yaapic-loadtest/1")

Mock servers:
MockServersViewModel -> MockServerService          (Core.Mocks: start, stop, log of 500, reads the definition per request)
  |-- IMockServerStore                  FileMockServerStore: mocks/<slug>.mock.yaml (VYaml DTOs)
  |-- MockRouteMatcher -> MockResponder (first enabled match, MockTemplate, CORS, 404)
  |-- MockGenerator                     one route per request of a folder
  `-- IMockHttpHost                     TcpMockHttpHost (Infrastructure): TcpListener on 127.0.0.1,
                                        Http11RequestReader / Http11ResponseWriter

Monitors:
MonitorsViewModel -> MonitorScheduler  (Core.Monitors: TimeProvider + PeriodicTimer, 30 s tick, one run at a time)
  |-- CollectionRunner                  one iteration, nothing persisted, not recorded in the history
  |-- IMonitorStore                     FileMonitorStore: monitors/<slug>.monitor.yaml
  `-- IMonitorRunStore                  FileMonitorRunStore: .yaapic/monitor-runs.jsonl (200 runs per monitor)
```

The data layer is the new `VariableScopeKind.Data` (fifth optional parameter of `VariableScopes`): local, data, environment,
collection, globals, dynamic; scripts read it through `yaapic.iterationData` and never write it. `ShellViewModel.Section` (`ShellSection`: `Requests`,
`Runner`, `LoadTest`, `MockServers`, `Monitors`) says what the main area shows; the rail commands (`ShowRunnerCommand`...), the folder
context menu, the palette (`palette.runCollection`, `palette.loadTest`, `palette.mockServers`, `palette.monitors`) and the status bar
items (Runner progress, "Mocks: {n} running", "Monitors: {n} failing") set it, and `TabsViewModel.TabShown` sets it back to `Requests`
(ADR 0023). `AppComposition.CreateShell` wires the stores, the host, the scheduler (which starts with the shell and
stops when it closes) and the services; closing the main window asks first while a Runner run or a load
test is in progress (not for mock servers), then stops the mock servers. `IDialogService.ConfirmAsync` backs the
load-test confirmation and `ContextProgress<T>` reports progress to the UI context. WinUI and Avalonia only render the view
models (`RunnerView`, `LoadTestView`, `MockServersView`, `MonitorsView`, a shared line chart for the load charts).

## Search and command palette

`Core.Search.FuzzyMatcher` ranks and highlights matches. `CommandPaletteViewModel` aggregates `IPaletteSource`s
(commands, requests, environments, tabs), ranks them with the matcher and runs the chosen item; the composition root
attaches it to the shell with `ShellViewModel.AttachPalette`. The App projects only render it.

## End-to-end tests

`tests/Yaapic.Infrastructure.Tests/EndToEnd` drives the shell view model against a loopback `HttpListener` server
(echo, `/status/{code}`, `/delay/{ms}`, `/redirect`) on a temporary workspace, with no external network.
Milestone 2 scenarios (verbs, bodies, auth, redirects, collections, variable scopes, history, palette) are in
`Milestone2EndToEndTests`. Milestone 3 adds `Milestone3ScriptingEndToEndTests` (JavaScript), `Milestone3LuaEndToEndTests`
(Lua and mixed-language chains) and `Milestone3VaultEndToEndTests` (secret vault), which build the shell with the real
engines and vault on a temporary workspace through `ScriptedShellFixture`; they need the native libraries
(`pwsh native/Build-Natives.ps1`, run automatically by the first build of `Yaapic.Scripting`).
Milestone 6 adds `Milestone6RunnerEndToEndTests`, `Milestone6MocksAndLoadEndToEndTests` (mock servers, the Runner and load tests
against a mock) and `Milestone6MonitorsEndToEndTests` (a manual `TimeProvider` drives the scheduler).
