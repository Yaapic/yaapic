# Task index

Cards are small, reviewable and reversible. `/next-task` picks the first card in table order whose status is `todo`
and whose dependencies are all `done`.

Statuses: `todo`, `in progress`, `done`, `blocked` (waiting for a product decision), `planned` (milestone not detailed
yet: write its cards before it can be picked). In the milestone summary table, `detailed` means its cards are written.

Platform column: `any`, `windows` (WinUI 3 only), `unix` (macOS + Linux, Avalonia).

## Milestone 0: Foundations

| Card | Title | Platform | Depends on | Status |
|---|---|---|---|---|
| [M0-01](milestone-0-foundations/M0-01-solution-and-conventions.md) | Solution, build conventions, AOT analyzers | any | none | done |
| [M0-02](milestone-0-foundations/M0-02-core-skeleton-and-architecture-tests.md) | Core project skeletons and architecture tests | any | M0-01 | done |
| [M0-03](milestone-0-foundations/M0-03-adrs.md) | ADRs 0001-0007 | any | M0-01 | done |
| [M0-04](milestone-0-foundations/M0-04-design-tokens-generator.md) | Design tokens generator and snapshot test | any | M0-02 | done |
| [M0-05](milestone-0-foundations/M0-05-avalonia-themed-window.md) | Avalonia empty themed window (Native AOT) | unix | M0-04 | done |
| [M0-06](milestone-0-foundations/M0-06-winui-themed-window.md) | WinUI 3 empty themed window (Native AOT) | windows | M0-04 | done |
| [M0-07](milestone-0-foundations/M0-07-ci.md) | CI: build, tests, AOT publish and smoke test | any | M0-05, M0-06 | done |
| [M0-08](milestone-0-foundations/M0-08-contributor-docs.md) | Contributor README (clone to running in 15 minutes) | any | M0-07 | done |

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
| [M1-28](milestone-1-http-core/M1-28-avalonia-shell-and-tabs.md) | Avalonia shell: sidebar, tabs and dialogs | unix | M0-05, M1-23 | done |
| [M1-29](milestone-1-http-core/M1-29-avalonia-request-editor.md) | Avalonia request editor view | unix | M1-28 | done |
| [M1-30](milestone-1-http-core/M1-30-avalonia-response-viewer.md) | Avalonia response viewer | unix | M1-29 | done |
| [M1-31](milestone-1-http-core/M1-31-avalonia-environments.md) | Avalonia environment management | unix | M1-29 | done |
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
| [M1-42](milestone-1-http-core/M1-42-winui-response-panel-restyle.md) | WinUI response panel restyle | windows | M1-41 | done |
| [M1-43](milestone-1-http-core/M1-43-winui-environments-restyle.md) | WinUI environment management restyle | windows | M1-38, M1-40 | done |
| [M1-44](milestone-1-http-core/M1-44-winui-code-editor-loomeditor.md) | WinUI code editor: evaluate and adopt LoomEditor.WinUI | windows | M1-42 | done |
| [M1-45](milestone-1-http-core/M1-45-winui-visual-verification.md) | WinUI visual verification against the mockups | windows | M1-43, M1-44 | done |
| [M1-46](milestone-1-http-core/M1-46-winui-request-response-splitter.md) | WinUI resizable request / response splitter | windows | M1-42 | done |
| [M1-47](milestone-1-http-core/M1-47-avalonia-screenshot-tooling.md) | Avalonia screenshot tooling | unix | M1-31, M2-22 | done |
| [M1-48](milestone-1-http-core/M1-48-avalonia-typography-and-resources.md) | Avalonia typography, fonts and Fluent resource aliases | unix | M1-33, M1-47 | done |
| [M1-49](milestone-1-http-core/M1-49-avalonia-button-and-input-styles.md) | Avalonia control styles: buttons and inputs | unix | M1-48 | done |
| [M1-50](milestone-1-http-core/M1-50-avalonia-list-tab-chip-and-dialog-styles.md) | Avalonia control styles: lists, tabs, chips, feedback and dialogs | unix | M1-49 | done |
| [M1-51](milestone-1-http-core/M1-51-avalonia-shell-title-bar-rail-and-sidebar.md) | Avalonia shell restyle: title bar, rail and sidebar | unix | M1-50 | done |
| [M1-52](milestone-1-http-core/M1-52-avalonia-request-tabs-and-empty-state.md) | Avalonia request tabs and empty state restyle | unix | M1-51 | done |
| [M1-53](milestone-1-http-core/M1-53-avalonia-key-value-table.md) | Avalonia key/value table restyle | unix | M1-50 | done |
| [M1-54](milestone-1-http-core/M1-54-avalonia-request-editor-restyle.md) | Avalonia request editor restyle | unix | M1-52, M1-53, M2-22 | todo |
| [M1-55](milestone-1-http-core/M1-55-avalonia-response-panel-restyle.md) | Avalonia response panel restyle | unix | M1-54 | todo |
| [M1-56](milestone-1-http-core/M1-56-avalonia-environments-restyle.md) | Avalonia environment management and prompt restyle | unix | M1-51, M1-53 | todo |
| [M1-57](milestone-1-http-core/M1-57-avalonia-visual-verification.md) | Avalonia visual verification against the mockups | unix | M1-55, M1-56 | todo |

M1-33 to M1-46 bring the WinUI app in line with the design mockups (`design/mockups/`, see its README).

M1-47 to M1-57 are the Avalonia counterpart (the same pass as M1-34 to M1-45, written after M2-22 because the user found
the Avalonia app much less faithful than the WinUI one). They are numbered as a Milestone 1 addendum but run inside
Milestone 2: M1-47 depends on M2-22, and they sit before the remaining Avalonia UI cards of M2 so that fidelity is fixed
before more Avalonia UI is added. M2-23, M2-24, M2-38, M2-50, M2-52 and M2-59 depend on M1-57 for that reason (the other
Avalonia cards follow them). The WinUI-only M1-44 (code editor) and M1-46 (splitter) have no separate Avalonia card: the
splitter is part of M1-54, and an AvaloniaEdit-based editor is a candidate follow-up listed by M1-57.

## Milestone 2: Full request editor, collections, environments, history, command palette

Scope: all HTTP methods and body kinds, authentication and per-request settings, URL and params sync, variable hints,
collections as folders on disk, globals and collection variables, request history, command palette. Storage decisions are
in [ADR 0008](../adr/0008-collections-globals-history-storage.md) (written by M2-01, read it first). Each slice runs
Domain, Core, Infrastructure and ViewModels (`any`) first, then WinUI (`windows`), then Avalonia (`unix`) cards. M2-67 is
the WinUI visual check; the Avalonia visual pass is M1-47 to M1-57 (see the Milestone 1 block above).

Deferred beyond M2 (listed in ADR 0008): manual ordering and drag and drop, cookie jar, proxy and TLS settings, workspace
picker, `{{variable}}` autocomplete, request scripts (M3). No card is `blocked`: every product choice is fixed in M2-01.

| Card | Title | Platform | Depends on | Status |
|---|---|---|---|---|
| [M2-01](milestone-2-editor-collections/M2-01-adr-workspace-layout-and-format.md) | ADR 0008 and file format extensions | any | none | done |
| [M2-02](milestone-2-editor-collections/M2-02-domain-methods-and-content-types.md) | Domain: HTTP methods and raw content types | any | M2-01 | done |
| [M2-03](milestone-2-editor-collections/M2-03-domain-body-kinds-and-form-fields.md) | Domain: body kinds and form fields | any | M2-02 | done |
| [M2-04](milestone-2-editor-collections/M2-04-domain-auth-and-settings.md) | Domain: request authentication and settings | any | M2-03 | done |
| [M2-05](milestone-2-editor-collections/M2-05-resolver-methods-bodies-settings.md) | Request resolution: methods, bodies and settings | any | M2-04 | done |
| [M2-06](milestone-2-editor-collections/M2-06-resolver-authentication.md) | Request resolution: authentication | any | M2-05 | done |
| [M2-07](milestone-2-editor-collections/M2-07-transport-bodies-redirects-timeout.md) | HttpClient transport: all methods, bodies, redirects and timeout | any | M2-06 | done |
| [M2-08](milestone-2-editor-collections/M2-08-yaml-request-serializer-extensions.md) | YAML request serializer: methods, bodies, auth and settings | any | M2-04 | done |
| [M2-09](milestone-2-editor-collections/M2-09-query-string-sync.md) | Core: query string parsing and composition | any | M2-01 | done |
| [M2-10](milestone-2-editor-collections/M2-10-dialog-service-prompt-pickers-clipboard.md) | Dialog service: text prompt, file pickers and clipboard | any | M2-01 | done |
| [M2-11](milestone-2-editor-collections/M2-11-request-editor-methods-auth-settings-state.md) | Request editor ViewModel: methods, authentication and settings | any | M2-04 | done |
| [M2-12](milestone-2-editor-collections/M2-12-request-editor-body-editors.md) | Request editor ViewModel: body editors | any | M2-03, M2-10, M2-11 | done |
| [M2-13](milestone-2-editor-collections/M2-13-request-editor-url-params-sync.md) | Request editor ViewModel: URL and query params two-way sync | any | M2-09, M2-11 | done |
| [M2-14](milestone-2-editor-collections/M2-14-response-copy-and-save.md) | Response panel ViewModel: copy and save body | any | M2-10, M2-12 | done |
| [M2-15](milestone-2-editor-collections/M2-15-winui-dialog-service-members.md) | WinUI dialog service: prompt, file pickers and clipboard | windows | M2-10 | done |
| [M2-16](milestone-2-editor-collections/M2-16-winui-method-picker-and-url-bar.md) | WinUI method picker and URL bar for all methods | windows | M2-11, M2-13 | done |
| [M2-17](milestone-2-editor-collections/M2-17-winui-body-pane.md) | WinUI body pane: all body kinds | windows | M2-12, M2-15, M2-16 | done |
| [M2-18](milestone-2-editor-collections/M2-18-winui-auth-and-settings-tabs.md) | WinUI Auth and Settings tabs | windows | M2-11, M2-16 | done |
| [M2-19](milestone-2-editor-collections/M2-19-winui-response-copy-and-save.md) | WinUI response copy and save buttons | windows | M2-14, M2-15 | done |
| [M2-20](milestone-2-editor-collections/M2-20-avalonia-dialog-service-members.md) | Avalonia dialog service: prompt, file pickers and clipboard | unix | M2-10 | done |
| [M2-21](milestone-2-editor-collections/M2-21-avalonia-method-picker-and-url-bar.md) | Avalonia method picker and URL bar for all methods | unix | M2-11, M2-13 | done |
| [M2-22](milestone-2-editor-collections/M2-22-avalonia-body-pane.md) | Avalonia body pane: all body kinds | unix | M2-12, M2-20, M2-21 | done |
| [M2-23](milestone-2-editor-collections/M2-23-avalonia-auth-and-settings-tabs.md) | Avalonia Auth and Settings tabs | unix | M2-11, M2-21, M1-57 | todo |
| [M2-24](milestone-2-editor-collections/M2-24-avalonia-response-copy-and-save.md) | Avalonia response copy and save buttons | unix | M2-14, M2-20, M1-57 | todo |
| [M2-25](milestone-2-editor-collections/M2-25-domain-folders-and-collection-nodes.md) | Domain: folders and collection nodes | any | M2-01 | todo |
| [M2-26](milestone-2-editor-collections/M2-26-collection-store-port-and-double.md) | Core: collection store port and in-memory double | any | M2-25 | todo |
| [M2-27](milestone-2-editor-collections/M2-27-yaml-folder-serializer.md) | YAML folder serializer | any | M2-25 | todo |
| [M2-28](milestone-2-editor-collections/M2-28-workspace-index-scanner.md) | Workspace index scanner | any | M2-27 | todo |
| [M2-29](milestone-2-editor-collections/M2-29-file-request-store-folders.md) | File request store: requests inside folders | any | M2-28 | todo |
| [M2-30](milestone-2-editor-collections/M2-30-file-collection-store-tree-and-folders.md) | File collection store: tree and folder management | any | M2-26, M2-29 | todo |
| [M2-31](milestone-2-editor-collections/M2-31-file-collection-store-move.md) | File collection store: move folders and requests | any | M2-30 | todo |
| [M2-32](milestone-2-editor-collections/M2-32-collection-tree-viewmodel.md) | Collection tree ViewModel | any | M2-26 | todo |
| [M2-33](milestone-2-editor-collections/M2-33-collection-tree-folder-commands.md) | Collection tree: folder commands | any | M2-32, M2-10 | todo |
| [M2-34](milestone-2-editor-collections/M2-34-collection-tree-request-commands.md) | Collection tree: request commands and move | any | M2-33 | todo |
| [M2-35](milestone-2-editor-collections/M2-35-shell-and-composition-collections.md) | Shell and composition: collections | any | M2-31, M2-34 | todo |
| [M2-36](milestone-2-editor-collections/M2-36-winui-collections-tree.md) | WinUI collections tree | windows | M2-35, M2-16 | todo |
| [M2-37](milestone-2-editor-collections/M2-37-winui-tree-menus-and-prompts.md) | WinUI tree context menus and prompts | windows | M2-36, M2-15 | todo |
| [M2-38](milestone-2-editor-collections/M2-38-avalonia-collections-tree.md) | Avalonia collections tree | unix | M2-35, M2-21, M1-57 | todo |
| [M2-39](milestone-2-editor-collections/M2-39-avalonia-tree-menus-and-prompts.md) | Avalonia tree context menus and prompts | unix | M2-38, M2-20 | todo |
| [M2-40](milestone-2-editor-collections/M2-40-core-variable-scopes-and-globals-port.md) | Core: variable scopes, sender and globals port | any | M2-01 | todo |
| [M2-41](milestone-2-editor-collections/M2-41-file-globals-store.md) | Globals YAML serializer and file store | any | M2-40 | todo |
| [M2-42](milestone-2-editor-collections/M2-42-globals-viewmodel-and-environment-duplicate.md) | Globals ViewModel and environment duplicate | any | M2-40 | todo |
| [M2-43](milestone-2-editor-collections/M2-43-folder-settings-viewmodel.md) | Folder and collection settings ViewModel | any | M2-34 | todo |
| [M2-44](milestone-2-editor-collections/M2-44-variable-scope-provider.md) | Variable scope provider: sending with all scopes | any | M2-35, M2-41, M2-42, M2-43 | todo |
| [M2-45](milestone-2-editor-collections/M2-45-shell-globals.md) | Shell: globals | any | M2-44 | todo |
| [M2-46](milestone-2-editor-collections/M2-46-variable-scanner-and-url-hints.md) | Variable scanner and URL variable hints | any | M2-44 | todo |
| [M2-47](milestone-2-editor-collections/M2-47-winui-environments-globals-and-duplicate.md) | WinUI environments dialog: globals and duplicate | windows | M2-42, M2-45 | todo |
| [M2-48](milestone-2-editor-collections/M2-48-winui-folder-settings-dialog.md) | WinUI folder and collection settings dialog | windows | M2-43, M2-37 | todo |
| [M2-49](milestone-2-editor-collections/M2-49-winui-url-variable-hints.md) | WinUI URL variable hints | windows | M2-46, M2-16 | todo |
| [M2-50](milestone-2-editor-collections/M2-50-avalonia-environments-globals-and-duplicate.md) | Avalonia environments window: globals and duplicate | unix | M2-42, M2-45, M1-57 | todo |
| [M2-51](milestone-2-editor-collections/M2-51-avalonia-folder-settings-window.md) | Avalonia folder and collection settings window | unix | M2-43, M2-39 | todo |
| [M2-52](milestone-2-editor-collections/M2-52-avalonia-url-variable-hints.md) | Avalonia URL variable hints | unix | M2-46, M2-21, M1-57 | todo |
| [M2-53](milestone-2-editor-collections/M2-53-domain-history-entry.md) | Domain: history entry | any | M2-01 | todo |
| [M2-54](milestone-2-editor-collections/M2-54-history-store-port-and-double.md) | Core: history store port and in-memory double | any | M2-53 | todo |
| [M2-55](milestone-2-editor-collections/M2-55-file-history-store.md) | File history store (JSON Lines) | any | M2-54, M2-08 | todo |
| [M2-56](milestone-2-editor-collections/M2-56-history-viewmodel-and-recording.md) | History ViewModel and recording | any | M2-54, M2-11 | todo |
| [M2-57](milestone-2-editor-collections/M2-57-history-commands-sidebar-mode-and-wiring.md) | History commands, sidebar mode and shell wiring | any | M2-56, M2-55, M2-35 | todo |
| [M2-58](milestone-2-editor-collections/M2-58-winui-sidebar-history.md) | WinUI sidebar mode switch and history list | windows | M2-57, M2-36 | todo |
| [M2-59](milestone-2-editor-collections/M2-59-avalonia-sidebar-history.md) | Avalonia sidebar mode switch and history list | unix | M2-57, M2-38, M1-57 | todo |
| [M2-60](milestone-2-editor-collections/M2-60-core-fuzzy-matcher.md) | Core: fuzzy matcher | any | M2-01 | todo |
| [M2-61](milestone-2-editor-collections/M2-61-command-palette-viewmodel.md) | Command palette ViewModel | any | M2-60 | todo |
| [M2-62](milestone-2-editor-collections/M2-62-tab-navigation-and-command-source.md) | Tab navigation and the commands palette source | any | M2-61, M2-57, M2-33 | todo |
| [M2-63](milestone-2-editor-collections/M2-63-palette-sources-and-shell-wiring.md) | Palette sources for requests, environments and tabs, and shell wiring | any | M2-62, M2-44 | todo |
| [M2-64](milestone-2-editor-collections/M2-64-winui-command-palette.md) | WinUI command palette | windows | M2-63, M2-58 | todo |
| [M2-65](milestone-2-editor-collections/M2-65-avalonia-command-palette.md) | Avalonia command palette | unix | M2-63, M2-59 | todo |
| [M2-66](milestone-2-editor-collections/M2-66-end-to-end-tests-and-docs.md) | End-to-end tests and milestone docs | any | M2-07, M2-31, M2-45, M2-57, M2-63 | todo |
| [M2-67](milestone-2-editor-collections/M2-67-winui-visual-verification.md) | WinUI visual verification of the milestone 2 views | windows | M2-18, M2-19, M2-37, M2-47, M2-48, M2-49, M2-58, M2-64 | todo |

## Milestones 2 to 10

| Milestone | Scope | Status |
|---|---|---|
| M2 | Full request editor, collections, environments, history, command palette | detailed (67 cards, see above) |
| M3 | Scripting (JS + Lua), pm.*, console, test results, secret vault | planned |
| M4 | Advanced auth and response (OAuth 2.0, JWT, mTLS, timings, diff, code generation) | planned |
| M5 | Import/export (Postman, OpenAPI, cURL, HAR, Insomnia, Bruno) | planned |
| M6 | Automation (Runner, load tests, mock servers, monitors) | planned |
| M7 | Protocols (WebSocket, SSE, Socket.IO, GraphQL, gRPC, MQTT, SOAP) | planned |
| M8 | Collaboration (Git, versions, comments, OpenAPI editor and docs) | planned |
| M9 | Flows, plugins, optional AI assistant | planned |
| M10 | Polish (accessibility, localization, onboarding, updates, packaging, hardening) | planned |

