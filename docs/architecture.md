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
     -> RequestResolver       (Core: {{variables}} resolved by scope, highest first: active environment, collection
                               variables, globals, dynamic variables; the scopes come from IVariableScopeProvider;
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
sees one native function, `__host(op, argsJson) -> resultJson`; `pm`, `console` and `expect` are preludes written in the
script language (C# `const string`s). `HostBridge` decodes the arguments with the source-generated `HostJsonContext`,
calls the typed `IScriptHost` and answers `{"ok":...}` or `{"err":"..."}`. The operations (`HostOps`) are `var.*`
(get, set, unset, has, clear, list, replace), `req.get`/`req.set`, `res.get`, `info.get`, `console`, `console.plain`, `test`,
`send` (`pm.sendRequest`), `cookies` and `unsupported`. `Yaapic.Composition` registers both engines; a library that cannot
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
