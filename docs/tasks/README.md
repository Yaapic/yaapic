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
| [M0-03](milestone-0-foundations/M0-03-adrs.md) | ADRs 0001-0007 | any | M0-01 | done |
| [M0-04](milestone-0-foundations/M0-04-design-tokens-generator.md) | Design tokens generator and snapshot test | any | M0-02 | done |
| [M0-05](milestone-0-foundations/M0-05-avalonia-themed-window.md) | Avalonia empty themed window (Native AOT) | unix | M0-04 | todo |
| [M0-06](milestone-0-foundations/M0-06-winui-themed-window.md) | WinUI 3 empty themed window (Native AOT) | windows | M0-04 | done |
| [M0-07](milestone-0-foundations/M0-07-ci.md) | CI: build, tests, AOT publish and smoke test | any | M0-05, M0-06 | todo |
| [M0-08](milestone-0-foundations/M0-08-contributor-docs.md) | Contributor README (clone to running in 15 minutes) | any | M0-07 | todo |

## Milestone 1: HTTP core

Scope: domain model, variable engine, GET/POST, Pretty/Raw, tabs, file storage.

| Card | Title | Platform | Depends on | Status |
|---|---|---|---|---|
| [M1-01](milestone-1-http-core/M1-01-domain-request-model.md) | Domain request model | any | none | done |
| [M1-02](milestone-1-http-core/M1-02-domain-response-model.md) | Domain response and send-result model | any | M1-01 | done |
| [M1-03](milestone-1-http-core/M1-03-domain-environment-model.md) | Domain environment and session model | any | M1-01 | done |
| [M1-04](milestone-1-http-core/M1-04-variable-resolver.md) | Variable resolver | any | M1-03 | done |
| [M1-05](milestone-1-http-core/M1-05-dynamic-variables.md) | Dynamic variables | any | M1-04 | done |
| [M1-06](milestone-1-http-core/M1-06-request-resolution.md) | Request resolution | any | M1-01, M1-04 | done |
| [M1-07](milestone-1-http-core/M1-07-request-sender.md) | Transport port and request sender | any | M1-02, M1-05, M1-06 | done |
| [M1-08](milestone-1-http-core/M1-08-http-client-transport.md) | HttpClient transport and Infrastructure project | any | M1-07 | done |
| [M1-09](milestone-1-http-core/M1-09-response-formatter.md) | Response body formatter (Pretty / Raw) | any | M1-02 | done |
| [M1-10](milestone-1-http-core/M1-10-vyaml-and-file-format.md) | VYaml adoption and workspace file format v1 | any | M1-08 | done |
| [M1-11](milestone-1-http-core/M1-11-storage-ports-and-test-doubles.md) | Storage ports and shared test doubles | any | M1-03, M1-07 | done |
| [M1-12](milestone-1-http-core/M1-12-yaml-request-serializer.md) | YAML request serializer | any | M1-10, M1-11 | done |
| [M1-13](milestone-1-http-core/M1-13-file-request-store.md) | File request store | any | M1-12 | done |
| [M1-14](milestone-1-http-core/M1-14-file-environment-store.md) | Environment YAML and file environment store | any | M1-13 | done |
| [M1-15](milestone-1-http-core/M1-15-session-store-and-workspace-bootstrap.md) | Session store and workspace bootstrap | any | M1-13 | done |
| [M1-16](milestone-1-http-core/M1-16-mvvm-and-key-value-editor.md) | MVVM toolkit and key/value editor ViewModel | any | M1-11 | done |
| [M1-17](milestone-1-http-core/M1-17-request-editor-viewmodel.md) | Request editor ViewModel | any | M1-16 | done |
| [M1-18](milestone-1-http-core/M1-18-response-panel-viewmodel.md) | Response panel ViewModel | any | M1-09, M1-16 | done |
| [M1-19](milestone-1-http-core/M1-19-send-command.md) | Send and cancel commands | any | M1-17, M1-18 | done |
| [M1-20](milestone-1-http-core/M1-20-tabs-viewmodel.md) | Tabs ViewModel, save and dialog service | any | M1-19 | done |
| [M1-21](milestone-1-http-core/M1-21-request-list-viewmodel.md) | Saved requests list ViewModel | any | M1-20 | done |
| [M1-22](milestone-1-http-core/M1-22-environments-viewmodel.md) | Environments ViewModel | any | M1-20 | done |
| [M1-23](milestone-1-http-core/M1-23-shell-viewmodel-and-composition.md) | Shell ViewModel and composition root | any | M1-14, M1-15, M1-21, M1-22 | done |
| [M1-24](milestone-1-http-core/M1-24-winui-shell-and-tabs.md) | WinUI shell: sidebar, tabs and dialogs | windows | M1-23 | done |
| [M1-25](milestone-1-http-core/M1-25-winui-request-editor.md) | WinUI request editor view | windows | M1-24 | done |
| [M1-26](milestone-1-http-core/M1-26-winui-response-viewer.md) | WinUI response viewer | windows | M1-25 | done |
| [M1-27](milestone-1-http-core/M1-27-winui-environments.md) | WinUI environment management | windows | M1-25 | done |
| [M1-28](milestone-1-http-core/M1-28-avalonia-shell-and-tabs.md) | Avalonia shell: sidebar, tabs and dialogs | unix | M0-05, M1-23 | todo |
| [M1-29](milestone-1-http-core/M1-29-avalonia-request-editor.md) | Avalonia request editor view | unix | M1-28 | todo |
| [M1-30](milestone-1-http-core/M1-30-avalonia-response-viewer.md) | Avalonia response viewer | unix | M1-29 | todo |
| [M1-31](milestone-1-http-core/M1-31-avalonia-environments.md) | Avalonia environment management | unix | M1-29 | todo |
| [M1-32](milestone-1-http-core/M1-32-end-to-end-tests-and-docs.md) | End-to-end tests and milestone docs | any | M1-23 | done |
| [M1-33](milestone-1-http-core/M1-33-token-generator-metrics-and-fluent-aliases.md) | Token generator: metrics, colour resources and Fluent aliases | any | M0-04 | done |
| [M1-34](milestone-1-http-core/M1-34-winui-screenshot-tooling.md) | WinUI screenshot tooling | windows | M1-27 | done |
| [M1-35](milestone-1-http-core/M1-35-winui-typography-and-code-font.md) | WinUI typography and bundled code font | windows | M1-33, M1-34 | done |
| [M1-36](milestone-1-http-core/M1-36-winui-button-and-input-styles.md) | WinUI control styles: buttons and text inputs | windows | M1-35 | done |
| [M1-37](milestone-1-http-core/M1-37-winui-list-tab-chip-and-dialog-styles.md) | WinUI control styles: lists, tabs, chips, feedback and dialogs | windows | M1-36 | done |
| [M1-38](milestone-1-http-core/M1-38-winui-shell-title-bar-rail-and-sidebar.md) | WinUI shell restyle: title bar, rail and sidebar | windows | M1-37 | done |
| [M1-39](milestone-1-http-core/M1-39-winui-request-tabs-and-empty-state.md) | WinUI request tabs and empty state restyle | windows | M1-38 | done |
| [M1-40](milestone-1-http-core/M1-40-winui-key-value-table.md) | WinUI key/value table restyle | windows | M1-37 | done |
| [M1-41](milestone-1-http-core/M1-41-winui-request-editor-restyle.md) | WinUI request editor restyle | windows | M1-39, M1-40 | done |
| [M1-42](milestone-1-http-core/M1-42-winui-response-panel-restyle.md) | WinUI response panel restyle | windows | M1-41 | todo |
| [M1-43](milestone-1-http-core/M1-43-winui-environments-restyle.md) | WinUI environment management restyle | windows | M1-38, M1-40 | todo |
| [M1-44](milestone-1-http-core/M1-44-winui-code-editor-loomeditor.md) | WinUI code editor: evaluate and adopt LoomEditor.WinUI | windows | M1-42 | todo |
| [M1-45](milestone-1-http-core/M1-45-winui-visual-verification.md) | WinUI visual verification against the mockups | windows | M1-43, M1-44 | todo |
| [M1-46](milestone-1-http-core/M1-46-winui-request-response-splitter.md) | WinUI resizable request / response splitter | windows | M1-42 | todo |

M1-33 to M1-46 bring the WinUI app in line with the design mockups (`design/mockups/`, see its README). The
equivalent Avalonia visual cards are out of scope for now and will be written after M1-28 to M1-31.

## Milestones 2 to 10

| Milestone | Scope | Status |
|---|---|---|
| M2 | Full request editor, collections, environments, history, command palette | planned |
| M3 | Scripting (JS + Lua), pm.*, console, test results, secret vault | planned |
| M4 | Advanced auth and response (OAuth 2.0, JWT, mTLS, timings, diff, code generation) | planned |
| M5 | Import/export (Postman, OpenAPI, cURL, HAR, Insomnia, Bruno) | planned |
| M6 | Automation (Runner, load tests, mock servers, monitors) | planned |
| M7 | Protocols (WebSocket, SSE, Socket.IO, GraphQL, gRPC, MQTT, SOAP) | planned |
| M8 | Collaboration (Git, versions, comments, OpenAPI editor and docs) | planned |
| M9 | Flows, plugins, optional AI assistant | planned |
| M10 | Polish (accessibility, localization, onboarding, updates, packaging, hardening) | planned |
