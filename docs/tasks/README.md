# Task index

Cards are small, reviewable and reversible. `/next-task` picks the first card in table order whose status is `todo`
and whose dependencies are all `done`.

Statuses: `todo`, `in progress`, `done`, `blocked` (waiting for a product decision), `planned` (milestone not detailed
yet: write its cards before it can be picked).

Platform column: `any`, `windows` (WinUI 3 only), `unix` (macOS + Linux, Avalonia).

## Milestone 0: Foundations

| Card | Title | Platform | Depends on | Status |
|---|---|---|---|---|
| [M0-01](milestone-0-foundations/M0-01-solution-and-conventions.md) | Solution, build conventions, AOT analyzers | any | none | done |
| [M0-02](milestone-0-foundations/M0-02-core-skeleton-and-architecture-tests.md) | Core project skeletons and architecture tests | any | M0-01 | done |
| [M0-03](milestone-0-foundations/M0-03-adrs.md) | ADRs 0001-0007 | any | M0-01 | blocked |
| [M0-04](milestone-0-foundations/M0-04-design-tokens-generator.md) | Design tokens generator and snapshot test | any | M0-02 | done |
| [M0-05](milestone-0-foundations/M0-05-avalonia-themed-window.md) | Avalonia empty themed window (Native AOT) | unix | M0-04 | todo |
| [M0-06](milestone-0-foundations/M0-06-winui-themed-window.md) | WinUI 3 empty themed window (Native AOT) | windows | M0-04 | done |
| [M0-07](milestone-0-foundations/M0-07-ci.md) | CI: build, tests, AOT publish and smoke test | any | M0-05, M0-06 | todo |
| [M0-08](milestone-0-foundations/M0-08-contributor-docs.md) | Contributor README (clone to running in 15 minutes) | any | M0-07 | todo |

M0-03 is `blocked` until the open product decisions are answered: storage format (YAML or JSON), Git client (git
binary or LibGit2Sharp), SDK (.NET 11 RC or .NET 10 LTS). Already decided: scripting engines are JavaScript
(QuickJS-NG) and Lua behind `IScriptEngine`.

## Milestones 1 to 10

| Milestone | Scope | Status |
|---|---|---|
| M1 | HTTP core: domain model, variable engine, GET/POST, Pretty/Raw, tabs, file storage | planned |
| M2 | Full request editor, collections, environments, history, command palette | planned |
| M3 | Scripting (JS + Lua), pm.*, console, test results, secret vault | planned |
| M4 | Advanced auth and response (OAuth 2.0, JWT, mTLS, timings, diff, code generation) | planned |
| M5 | Import/export (Postman, OpenAPI, cURL, HAR, Insomnia, Bruno) | planned |
| M6 | Automation (Runner, load tests, mock servers, monitors) | planned |
| M7 | Protocols (WebSocket, SSE, Socket.IO, GraphQL, gRPC, MQTT, SOAP) | planned |
| M8 | Collaboration (Git, versions, comments, OpenAPI editor and docs) | planned |
| M9 | Flows, plugins, optional AI assistant | planned |
| M10 | Polish (accessibility, localization, onboarding, updates, packaging, hardening) | planned |
