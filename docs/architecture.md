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
`Milestone2EndToEndTests`.
