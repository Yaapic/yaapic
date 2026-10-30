# Architecture

Yaapic is a layered solution. Dependencies only point downwards; `tests/Yaapic.Architecture.Tests`
(`DependencyRulesTests`) enforces the table below by reading the `ProjectReference` items of every csproj.

## Projects and allowed references

| Project | May reference |
|---|---|
| `Yaapic.Domain` | nothing |
| `Yaapic.Core` | `Domain` |
| `Yaapic.Infrastructure` | `Core`, `Domain` |
| `Yaapic.UI.Abstractions` | nothing |
| `Yaapic.Design.Tokens` | nothing |
| `Yaapic.ViewModels` | `Core`, `UI.Abstractions` (never `Infrastructure`) |
| `Yaapic.Composition` | `Core`, `Domain`, `Infrastructure`, `UI.Abstractions`, `ViewModels` |
| `Yaapic.App.*` (WinUI, Avalonia) | `Composition`, `UI.Abstractions`, `ViewModels`; no business logic |

No library references an App project.

## Request flow

```
RequestEditorViewModel.SendCommand
  -> RequestSender            (Core: use case)
     -> RequestResolver       (Core: {{variables}} resolved from the active environment and dynamic variables;
                               unresolved names are reported, they never block the send)
     -> IHttpTransport        (Core port, implemented by HttpClientTransport in Infrastructure)
  -> SendReport               (result + unresolved variables)
  -> ResponsePanelViewModel   (status, time, size, Pretty/Raw body, headers, errors)
```

Cancellation maps to a failed result "Request canceled"; any other transport error becomes a failed result too.

## Storage

`Core.Storage` defines `IRequestStore`, `IEnvironmentStore` and `ISessionStore`. `Infrastructure` implements them as
YAML files in the workspace folder (`FileRequestStore`, `FileEnvironmentStore`, `FileSessionStore`); see
[file-format.md](file-format.md). `Composition.AppComposition.CreateShell` wires the whole graph for every App project.

## End-to-end tests

`tests/Yaapic.Infrastructure.Tests/EndToEnd` drives the shell view model against a loopback `HttpListener` server
(echo, `/status/{code}`, `/delay/{ms}`) on a temporary workspace, with no external network.
