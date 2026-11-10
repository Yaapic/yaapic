# Task index

Cards are small, reviewable and reversible. `/next-task` picks the first card in table order whose status is `todo`
and whose dependencies are all `done`.

Statuses: `todo`, `in progress`, `done`, `blocked` (waiting for a product decision), `planned` (milestone not detailed
yet: write its cards before it can be picked), `superseded` (replaced by another card, never picked; its card says by
which). In the milestone summary table, `detailed` means its cards are written.

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
| [M1-54](milestone-1-http-core/M1-54-avalonia-request-editor-restyle.md) | Avalonia request editor restyle | unix | M1-52, M1-53, M2-22 | done |
| [M1-55](milestone-1-http-core/M1-55-avalonia-response-panel-restyle.md) | Avalonia response panel restyle | unix | M1-54 | done |
| [M1-56](milestone-1-http-core/M1-56-avalonia-environments-restyle.md) | Avalonia environment management and prompt restyle | unix | M1-51, M1-53 | done |
| [M1-57](milestone-1-http-core/M1-57-avalonia-visual-verification.md) | Avalonia visual verification against the mockups | unix | M1-55, M1-56 | done |

M1-33 to M1-46 bring the WinUI app in line with the design mockups (`design/mockups/`, see its README).

M1-47 to M1-57 are the Avalonia counterpart (the same pass as M1-34 to M1-45, written after M2-22 because the user found
the Avalonia app much less faithful than the WinUI one). They are numbered as a Milestone 1 addendum but run inside
Milestone 2: M1-47 depends on M2-22, and they sit before the remaining Avalonia UI cards of M2 so that fidelity is fixed
before more Avalonia UI is added. M2-23, M2-24, M2-38, M2-50, M2-52 and M2-59 depend on M1-57 for that reason (the other
Avalonia cards follow them). The WinUI-only M1-44 (code editor) and M1-46 (splitter) have no separate Avalonia card: the
splitter is part of M1-54, and an AvaloniaEdit-based editor is a candidate follow-up listed by the [Avalonia visual verification](../screenshots/m1-avalonia/README.md) (M1-57).

## Milestone 2: Full request editor, collections, environments, history, command palette

Scope: all HTTP methods and body kinds, authentication and per-request settings, URL and params sync, variable hints,
collections as folders on disk, globals and collection variables, request history, command palette. Storage decisions are
in [ADR 0008](../adr/0008-collections-globals-history-storage.md) (written by M2-01, read it first). Each slice runs
Domain, Core, Infrastructure and ViewModels (`any`) first, then WinUI (`windows`), then Avalonia (`unix`) cards. M2-67 is
the WinUI visual check; the Avalonia visual pass is M1-47 to M1-57 (see the Milestone 1 block above). M2-68 to M2-70 add the syntax-highlighted code editors (Windows App SDK 2.x migration, LoomEditor on WinUI, LoomEditor on Avalonia).

M2-71 to M2-75 rework the variable UX after the user's review of the chip strip (M2-46, M2-49): variables are coloured
**inside the text** (Postman / Bruno style) with a hover popup that shows the value or creates the variable in a scope.
M2-71 is the shared model (token ranges, `VariableContext`, popup view model, write path); M2-72/M2-73 (WinUI, URL bar
then header/param values; M2-72 removes the M2-49 chips) and M2-74/M2-75 (Avalonia) draw it with a transparent-text
`TextBox` over a coloured `TextBlock` overlay (fallbacks in the cards). M2-52 (the never-built Avalonia chip strip) is
`superseded` by M2-74. These cards sit right after M2-52 in the table so they run before the history slice; they do
**not** wait for M2-68 to M2-70 (the code editors only own body text, where `{{variable}}` highlighting is a later card
that reuses `VariableContext`). The numbers follow M2-70 only because they were added afterwards.

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
| [M2-23](milestone-2-editor-collections/M2-23-avalonia-auth-and-settings-tabs.md) | Avalonia Auth and Settings tabs | unix | M2-11, M2-21, M1-57 | done |
| [M2-24](milestone-2-editor-collections/M2-24-avalonia-response-copy-and-save.md) | Avalonia response copy and save buttons | unix | M2-14, M2-20, M1-57 | done |
| [M2-25](milestone-2-editor-collections/M2-25-domain-folders-and-collection-nodes.md) | Domain: folders and collection nodes | any | M2-01 | done |
| [M2-26](milestone-2-editor-collections/M2-26-collection-store-port-and-double.md) | Core: collection store port and in-memory double | any | M2-25 | done |
| [M2-27](milestone-2-editor-collections/M2-27-yaml-folder-serializer.md) | YAML folder serializer | any | M2-25 | done |
| [M2-28](milestone-2-editor-collections/M2-28-workspace-index-scanner.md) | Workspace index scanner | any | M2-27 | done |
| [M2-29](milestone-2-editor-collections/M2-29-file-request-store-folders.md) | File request store: requests inside folders | any | M2-28 | done |
| [M2-30](milestone-2-editor-collections/M2-30-file-collection-store-tree-and-folders.md) | File collection store: tree and folder management | any | M2-26, M2-29 | done |
| [M2-31](milestone-2-editor-collections/M2-31-file-collection-store-move.md) | File collection store: move folders and requests | any | M2-30 | done |
| [M2-32](milestone-2-editor-collections/M2-32-collection-tree-viewmodel.md) | Collection tree ViewModel | any | M2-26 | done |
| [M2-33](milestone-2-editor-collections/M2-33-collection-tree-folder-commands.md) | Collection tree: folder commands | any | M2-32, M2-10 | done |
| [M2-34](milestone-2-editor-collections/M2-34-collection-tree-request-commands.md) | Collection tree: request commands and move | any | M2-33 | done |
| [M2-35](milestone-2-editor-collections/M2-35-shell-and-composition-collections.md) | Shell and composition: collections | any | M2-31, M2-34 | done |
| [M2-36](milestone-2-editor-collections/M2-36-winui-collections-tree.md) | WinUI collections tree | windows | M2-35, M2-16 | done |
| [M2-37](milestone-2-editor-collections/M2-37-winui-tree-menus-and-prompts.md) | WinUI tree context menus and prompts | windows | M2-36, M2-15 | done |
| [M2-38](milestone-2-editor-collections/M2-38-avalonia-collections-tree.md) | Avalonia collections tree | unix | M2-35, M2-21, M1-57 | done |
| [M2-39](milestone-2-editor-collections/M2-39-avalonia-tree-menus-and-prompts.md) | Avalonia tree context menus and prompts | unix | M2-38, M2-20 | done |
| [M2-40](milestone-2-editor-collections/M2-40-core-variable-scopes-and-globals-port.md) | Core: variable scopes, sender and globals port | any | M2-01 | done |
| [M2-41](milestone-2-editor-collections/M2-41-file-globals-store.md) | Globals YAML serializer and file store | any | M2-40 | done |
| [M2-42](milestone-2-editor-collections/M2-42-globals-viewmodel-and-environment-duplicate.md) | Globals ViewModel and environment duplicate | any | M2-40 | done |
| [M2-43](milestone-2-editor-collections/M2-43-folder-settings-viewmodel.md) | Folder and collection settings ViewModel | any | M2-34 | done |
| [M2-44](milestone-2-editor-collections/M2-44-variable-scope-provider.md) | Variable scope provider: sending with all scopes | any | M2-35, M2-41, M2-42, M2-43 | done |
| [M2-45](milestone-2-editor-collections/M2-45-shell-globals.md) | Shell: globals | any | M2-44 | done |
| [M2-46](milestone-2-editor-collections/M2-46-variable-scanner-and-url-hints.md) | Variable scanner and URL variable hints | any | M2-44 | done |
| [M2-47](milestone-2-editor-collections/M2-47-winui-environments-globals-and-duplicate.md) | WinUI environments dialog: globals and duplicate | windows | M2-42, M2-45 | done |
| [M2-48](milestone-2-editor-collections/M2-48-winui-folder-settings-dialog.md) | WinUI folder and collection settings dialog | windows | M2-43, M2-37 | done |
| [M2-49](milestone-2-editor-collections/M2-49-winui-url-variable-hints.md) | WinUI URL variable hints | windows | M2-46, M2-16 | done |
| [M2-50](milestone-2-editor-collections/M2-50-avalonia-environments-globals-and-duplicate.md) | Avalonia environments window: globals and duplicate | unix | M2-42, M2-45, M1-57 | done |
| [M2-51](milestone-2-editor-collections/M2-51-avalonia-folder-settings-window.md) | Avalonia folder and collection settings window | unix | M2-43, M2-39 | done |
| [M2-52](milestone-2-editor-collections/M2-52-avalonia-url-variable-hints.md) | Avalonia URL variable hints (chip strip) | unix | M2-46, M2-21, M1-57 | superseded |
| [M2-71](milestone-2-editor-collections/M2-71-variable-tokens-context-and-create.md) | Variable tokens, context and create-variable popup model | any | M2-44, M2-46, M2-43 | done |
| [M2-72](milestone-2-editor-collections/M2-72-winui-inline-variable-highlighting-url-bar.md) | WinUI inline variable highlighting and popup in the URL bar (replaces the M2-49 chips) | windows | M2-71, M2-49, M2-16 | done |
| [M2-73](milestone-2-editor-collections/M2-73-winui-variables-in-headers-and-params.md) | WinUI inline variables in header and param values | windows | M2-72 | done |
| [M2-74](milestone-2-editor-collections/M2-74-avalonia-inline-variable-highlighting-url-bar.md) | Avalonia inline variable highlighting and popup in the URL bar | unix | M2-71, M2-21, M1-57 | done |
| [M2-75](milestone-2-editor-collections/M2-75-avalonia-variables-in-headers-and-params.md) | Avalonia inline variables in header and param values | unix | M2-74 | done |
| [M2-53](milestone-2-editor-collections/M2-53-domain-history-entry.md) | Domain: history entry | any | M2-01 | done |
| [M2-54](milestone-2-editor-collections/M2-54-history-store-port-and-double.md) | Core: history store port and in-memory double | any | M2-53 | done |
| [M2-55](milestone-2-editor-collections/M2-55-file-history-store.md) | File history store (JSON Lines) | any | M2-54, M2-08 | done |
| [M2-56](milestone-2-editor-collections/M2-56-history-viewmodel-and-recording.md) | History ViewModel and recording | any | M2-54, M2-11 | done |
| [M2-57](milestone-2-editor-collections/M2-57-history-commands-sidebar-mode-and-wiring.md) | History commands, sidebar mode and shell wiring | any | M2-56, M2-55, M2-35 | done |
| [M2-58](milestone-2-editor-collections/M2-58-winui-sidebar-history.md) | WinUI sidebar mode switch and history list | windows | M2-57, M2-36 | done |
| [M2-59](milestone-2-editor-collections/M2-59-avalonia-sidebar-history.md) | Avalonia sidebar mode switch and history list | unix | M2-57, M2-38, M1-57 | done |
| [M2-60](milestone-2-editor-collections/M2-60-core-fuzzy-matcher.md) | Core: fuzzy matcher | any | M2-01 | done |
| [M2-61](milestone-2-editor-collections/M2-61-command-palette-viewmodel.md) | Command palette ViewModel | any | M2-60 | done |
| [M2-62](milestone-2-editor-collections/M2-62-tab-navigation-and-command-source.md) | Tab navigation and the commands palette source | any | M2-61, M2-57, M2-33 | done |
| [M2-63](milestone-2-editor-collections/M2-63-palette-sources-and-shell-wiring.md) | Palette sources for requests, environments and tabs, and shell wiring | any | M2-62, M2-44 | done |
| [M2-64](milestone-2-editor-collections/M2-64-winui-command-palette.md) | WinUI command palette | windows | M2-63, M2-58 | done |
| [M2-65](milestone-2-editor-collections/M2-65-avalonia-command-palette.md) | Avalonia command palette | unix | M2-63, M2-59 | done |
| [M2-66](milestone-2-editor-collections/M2-66-end-to-end-tests-and-docs.md) | End-to-end tests and milestone docs | any | M2-07, M2-31, M2-45, M2-57, M2-63 | done |
| [M2-67](milestone-2-editor-collections/M2-67-winui-visual-verification.md) | WinUI visual verification of the milestone 2 views | windows | M2-18, M2-19, M2-37, M2-47, M2-48, M2-49, M2-58, M2-64, M2-72, M2-73 | done |
| [M2-68](milestone-2-editor-collections/M2-68-winui-windows-app-sdk-2.md) | WinUI: migrate to Windows App SDK 2.x | windows | M2-67 | done |
| [M2-69](milestone-2-editor-collections/M2-69-winui-loomeditor-code-editor.md) | WinUI: adopt LoomEditor in CodeEditorView | windows | M2-68 | done |
| [M2-70](milestone-2-editor-collections/M2-70-avalonia-loomeditor-code-editor.md) | Avalonia: adopt LoomEditor in CodeEditorView | unix | M2-69 | done |
| [M2-76](milestone-2-editor-collections/M2-76-winui-palette-ctrl-p-and-button.md) | WinUI palette Ctrl+P shortcut and button | windows | M2-64 | done |
| [M2-77](milestone-2-editor-collections/M2-77-avalonia-palette-ctrl-p-and-button.md) | Avalonia palette Ctrl+P shortcut and button | unix | M2-65 | done |

### Milestone 2 summary

Delivered: every HTTP method and body kind (raw, url-encoded, form-data, binary), basic, bearer and API key auth,
per-request redirect and timeout settings, query/URL sync, collections and folders as directories, globals, collection
and environment variables with the precedence environment > collection > globals > dynamic, inline variable
highlighting, send history (JSON Lines), the command palette with fuzzy search, and end-to-end tests over a loopback
server (`Milestone2EndToEndTests`). No package was added.

Deferred (ADR 0008): manual ordering and drag and drop, cookie jar, proxy and TLS settings, workspace picker,
`{{variable}}` autocomplete, request scripts (M3). Still open in M2: M2-67 to M2-70 (visual check, Windows App SDK 2.x,
code editors).

## Milestone 3: Scripting (JS + Lua), pm.*, console, test results, secret vault

Scope: pre-request and post-response scripts at request, folder and collection level in JavaScript (QuickJS-NG) and Lua 5.4
behind one `IScriptEngine` port, a Postman-compatible `pm.*` API (variables, request, response, `pm.test`, `pm.expect`,
`pm.sendRequest`, read-only cookies), a console, test results, and an encrypted secret vault with variable integration. The
decisions are in [ADR 0009](../adr/0009-scripting.md) and [ADR 0010](../adr/0010-secret-vault.md) (written by M3-01 and M3-02,
read them first). Build order: `any` cards (Domain, Core, Scripting, Infrastructure, ViewModels), then `windows`, then `unix`
cards.

Slices: M3-01 to M3-02 ADRs; M3-03 to M3-13 domain, YAML and the scripted send pipeline (fakes only); M3-14 to M3-23 the
`Yaapic.Scripting` project, the native build pipeline, QuickJS bindings and the JavaScript `pm`; M3-24 to M3-32 the same for
Lua (including a C shim, because Lua errors use `longjmp`); M3-33 to M3-38 and M3-45 to M3-46 the vault (flag, ports, AES-GCM, file
vault, decorators, redaction, view models); M3-39 to M3-44 and M3-47 the view models and the composition; M3-48 and M3-49 the
OS key protectors (DPAPI, Keychain, Secret Service); M3-50 to M3-56 WinUI; M3-57 to M3-63 Avalonia; M3-64 to M3-66 end-to-end
tests; M3-67 docs; M3-68 the WinUI visual check (the Avalonia cards carry their own runtime checks).

Native code: the engines are built from pinned upstream sources by `native/Build-Natives.ps1` (M3-15) and are **not** committed;
a C compiler and CMake are required (already needed for Native AOT). Cards from M3-16 on need the libraries for the host RID.
Cards that touch a platform the host is not on (M3-49 on Windows, the Avalonia runtime checks on a non-matching OS) say what was
not run in their Notes.

| Card | Title | Platform | Depends on | Status |
|---|---|---|---|---|
| [M3-01](milestone-3-scripting/M3-01-adr-scripting-architecture.md) | ADR 0009: scripting architecture, host protocol and file format | any | none | done |
| [M3-02](milestone-3-scripting/M3-02-adr-secret-vault.md) | ADR 0010: secret vault | any | none | done |
| [M3-03](milestone-3-scripting/M3-03-domain-script-model.md) | Domain: script model on requests and folders | any | M3-01 | done |
| [M3-04](milestone-3-scripting/M3-04-domain-console-tests-and-run-result.md) | Domain: console entries, test results and script run result | any | M3-03 | done |
| [M3-05](milestone-3-scripting/M3-05-yaml-request-scripts.md) | YAML request serializer: scripts | any | M3-03 | done |
| [M3-06](milestone-3-scripting/M3-06-yaml-folder-scripts.md) | YAML folder serializer: scripts | any | M3-05 | done |
| [M3-07](milestone-3-scripting/M3-07-core-script-engine-port.md) | Core: script engine port, host contract and registry | any | M3-03, M3-04 | done |
| [M3-08](milestone-3-scripting/M3-08-core-script-variable-layers.md) | Core: script variable layers and change tracking | any | M3-07 | done |
| [M3-09](milestone-3-scripting/M3-09-core-script-request-and-response.md) | Core: script request and response snapshots, cookies | any | M3-07 | done |
| [M3-10](milestone-3-scripting/M3-10-script-chain-provider.md) | Script chain: collection, folders, request | any | M3-03, M3-06 | done |
| [M3-11](milestone-3-scripting/M3-11-core-scripted-sender-pre-request.md) | Core: scripted request sender, pre-request stage | any | M3-08, M3-09, M3-10 | done |
| [M3-12](milestone-3-scripting/M3-12-core-scripted-sender-post-response.md) | Core: post-response stage, test results and network console line | any | M3-11 | done |
| [M3-13](milestone-3-scripting/M3-13-core-script-send-request.md) | Core: host support for `pm.sendRequest` | any | M3-12 | done |
| [M3-14](milestone-3-scripting/M3-14-scripting-project-and-host-protocol.md) | `Yaapic.Scripting` project, host bridge and JSON protocol | any | M3-07 | done |
| [M3-15](milestone-3-scripting/M3-15-native-build-pipeline.md) | Native build pipeline for QuickJS-NG and Lua 5.4 | any | M3-14 | done |
| [M3-16](milestone-3-scripting/M3-16-quickjs-bindings.md) | QuickJS-NG P/Invoke bindings and AOT smoke | any | M3-15 | done |
| [M3-17](milestone-3-scripting/M3-17-quickjs-engine.md) | `QuickJsEngine`: run a block with console, timeout and the host bridge | any | M3-16, M3-14 | done |
| [M3-18](milestone-3-scripting/M3-18-js-pm-variables-and-info.md) | JavaScript `pm`: info, variables, environment, collection variables, globals | any | M3-17, M3-08 | done |
| [M3-19](milestone-3-scripting/M3-19-js-pm-request-and-response.md) | JavaScript `pm`: request and response | any | M3-18, M3-09 | done |
| [M3-20](milestone-3-scripting/M3-20-js-pm-test-and-expect-core.md) | JavaScript `pm.test` and `pm.expect`, core assertions | any | M3-19 | done |
| [M3-21](milestone-3-scripting/M3-21-js-pm-expect-collections-and-response-assertions.md) | JavaScript `pm.expect` collections/strings and `pm.response.to.*` | any | M3-20 | done |
| [M3-22](milestone-3-scripting/M3-22-js-pm-send-request-and-cookies.md) | JavaScript `pm.sendRequest` and `pm.cookies` | any | M3-21, M3-13 | done |
| [M3-23](milestone-3-scripting/M3-23-js-postman-conformance-suite.md) | JavaScript Postman conformance suite | any | M3-22 | done |
| [M3-24](milestone-3-scripting/M3-24-lua-bindings.md) | Lua 5.4 native shim, bindings and AOT smoke | any | M3-15 | done |
| [M3-25](milestone-3-scripting/M3-25-lua-engine.md) | `LuaEngine`: run a block with console, limits and the host bridge | any | M3-24, M3-17 | done |
| [M3-26](milestone-3-scripting/M3-26-lua-json-codec-and-host-wrapper.md) | Lua prelude: JSON codec and typed host wrapper | any | M3-25, M3-14 | done |
| [M3-27](milestone-3-scripting/M3-27-lua-pm-variables-and-info.md) | Lua `pm`: info, variables, environment, collection variables, globals | any | M3-26, M3-08 | done |
| [M3-28](milestone-3-scripting/M3-28-lua-pm-request-and-response.md) | Lua `pm`: request and response | any | M3-27, M3-09 | done |
| [M3-29](milestone-3-scripting/M3-29-lua-pm-test-and-expect-core.md) | Lua `pm.test` and `pm.expect`, core assertions | any | M3-28 | done |
| [M3-30](milestone-3-scripting/M3-30-lua-pm-expect-collections-and-response-assertions.md) | Lua `pm.expect` collections/strings and `pm.response.to` | any | M3-29 | done |
| [M3-31](milestone-3-scripting/M3-31-lua-pm-send-request-and-cookies.md) | Lua `pm.sendRequest` and `pm.cookies` | any | M3-30, M3-13 | done |
| [M3-32](milestone-3-scripting/M3-32-lua-conformance-suite.md) | Lua conformance suite and compatibility doc | any | M3-31, M3-23 | done |
| [M3-33](milestone-3-scripting/M3-33-domain-secret-flag-and-serializers.md) | Domain and YAML: secret flag on variable rows | any | M3-02 | done |
| [M3-34](milestone-3-scripting/M3-34-core-vault-ports-and-doubles.md) | Core: vault ports and test doubles | any | M3-33 | done |
| [M3-35](milestone-3-scripting/M3-35-infra-aes-gcm-secret-cipher.md) | Infrastructure: AES-GCM secret cipher and key derivation | any | M3-34 | done |
| [M3-36](milestone-3-scripting/M3-36-infra-file-secret-vault.md) | Infrastructure: file secret vault, passphrase protector and workspace id | any | M3-35 | done |
| [M3-37](milestone-3-scripting/M3-37-secret-aware-environment-and-globals-stores.md) | Core: secret-aware environment and globals stores | any | M3-34, M3-33 | done |
| [M3-38](milestone-3-scripting/M3-38-secret-aware-collection-store-and-redactor.md) | Core: secret-aware collection store and secret redaction | any | M3-37, M3-12 | done |
| [M3-39](milestone-3-scripting/M3-39-request-editor-scripts-viewmodel.md) | Request editor ViewModel: scripts | any | M3-03, M3-05 | done |
| [M3-40](milestone-3-scripting/M3-40-folder-settings-scripts-viewmodel.md) | Folder and collection settings ViewModel: scripts | any | M3-39, M3-06 | done |
| [M3-41](milestone-3-scripting/M3-41-console-viewmodel.md) | Console ViewModel | any | M3-04 | done |
| [M3-42](milestone-3-scripting/M3-42-test-results-viewmodel.md) | Test results ViewModel and response panel integration | any | M3-04, M3-12 | done |
| [M3-43](milestone-3-scripting/M3-43-send-integration.md) | Request editor: send through scripts | any | M3-12, M3-39, M3-41, M3-42, M3-10 | done |
| [M3-44](milestone-3-scripting/M3-44-apply-script-variable-changes.md) | Persist the variable changes made by scripts | any | M3-43, M3-08 | done |
| [M3-45](milestone-3-scripting/M3-45-vault-viewmodel.md) | Vault ViewModel | any | M3-34, M3-37 | done |
| [M3-46](milestone-3-scripting/M3-46-secret-rows-and-masking.md) | Secret rows in the key/value editor and masking in variable hints | any | M3-33, M3-45 | done |
| [M3-47](milestone-3-scripting/M3-47-shell-console-vault-and-composition.md) | Shell, palette and composition: engines, console, vault | any | M3-17, M3-25, M3-38, M3-43, M3-44, M3-45, M3-46 | done |
| [M3-48](milestone-3-scripting/M3-48-windows-dpapi-key-protector.md) | Windows: DPAPI key protector | windows | M3-36, M3-47 | done |
| [M3-49](milestone-3-scripting/M3-49-unix-keychain-and-secret-service-protectors.md) | macOS Keychain and Linux Secret Service key protectors | unix | M3-36, M3-47 | done |
| [M3-50](milestone-3-scripting/M3-50-winui-code-editor-javascript-and-lua.md) | WinUI: JavaScript and Lua in `CodeEditorView` | windows | M3-03 | done |
| [M3-51](milestone-3-scripting/M3-51-winui-scripts-tab.md) | WinUI: Scripts tab in the request editor | windows | M3-39, M3-50 | done |
| [M3-52](milestone-3-scripting/M3-52-winui-folder-scripts-dialog.md) | WinUI: scripts in the folder and collection settings dialog | windows | M3-40, M3-50, M3-51 | done |
| [M3-53](milestone-3-scripting/M3-53-winui-console-panel-and-status-bar.md) | WinUI: status bar and console panel | windows | M3-47 | done |
| [M3-54](milestone-3-scripting/M3-54-winui-tests-tab-in-response.md) | WinUI: Tests tab and script errors in the response panel | windows | M3-42, M3-47 | done |
| [M3-55](milestone-3-scripting/M3-55-winui-secret-variables-in-tables.md) | WinUI: secret variables in the variable tables and popup | windows | M3-46, M3-47 | done |
| [M3-56](milestone-3-scripting/M3-56-winui-vault-dialogs-and-status.md) | WinUI: vault dialogs, status bar item and DPAPI wiring | windows | M3-45, M3-48, M3-53, M3-55 | done |
| [M3-57](milestone-3-scripting/M3-57-avalonia-code-editor-javascript-and-lua.md) | Avalonia: JavaScript and Lua in `CodeEditorView` | unix | M3-03, M3-50 | done |
| [M3-58](milestone-3-scripting/M3-58-avalonia-scripts-tab.md) | Avalonia: Scripts tab in the request editor | unix | M3-39, M3-57, M3-51 | done |
| [M3-59](milestone-3-scripting/M3-59-avalonia-folder-scripts-window.md) | Avalonia: scripts in the folder and collection settings window | unix | M3-40, M3-58 | done |
| [M3-60](milestone-3-scripting/M3-60-avalonia-console-panel-and-status-bar.md) | Avalonia: status bar and console panel | unix | M3-47, M3-53 | done |
| [M3-61](milestone-3-scripting/M3-61-avalonia-tests-tab-in-response.md) | Avalonia: Tests tab and script errors in the response panel | unix | M3-42, M3-47, M3-54 | done |
| [M3-62](milestone-3-scripting/M3-62-avalonia-secret-variables-in-tables.md) | Avalonia: secret variables in the variable tables and popup | unix | M3-46, M3-47, M3-55 | done |
| [M3-63](milestone-3-scripting/M3-63-avalonia-vault-windows-and-status.md) | Avalonia: vault windows, status bar item and platform protector wiring | unix | M3-45, M3-49, M3-60, M3-62, M3-56 | done |
| [M3-64](milestone-3-scripting/M3-64-end-to-end-javascript-scripting.md) | End-to-end tests: JavaScript scripting | any | M3-47, M3-44, M3-23 | done |
| [M3-65](milestone-3-scripting/M3-65-end-to-end-lua-scripting.md) | End-to-end tests: Lua scripting and mixed languages | any | M3-64, M3-32 | done |
| [M3-66](milestone-3-scripting/M3-66-end-to-end-secret-vault.md) | End-to-end tests: secret vault | any | M3-47, M3-64 | todo |
| [M3-67](milestone-3-scripting/M3-67-milestone-docs.md) | Milestone 3 docs | any | M3-64, M3-65, M3-66, M3-32, M3-49 | todo |
| [M3-68](milestone-3-scripting/M3-68-winui-visual-verification.md) | WinUI visual verification of the milestone 3 views | windows | M3-51, M3-52, M3-53, M3-54, M3-55, M3-56 | todo |

## Milestones 2 to 10

| Milestone | Scope | Status |
|---|---|---|
| M2 | Full request editor, collections, environments, history, command palette | detailed (77 cards, see above) |
| M3 | Scripting (JS + Lua), pm.*, console, test results, secret vault | detailed (68 cards, see above; summary written by M3-67) |
| M4 | Advanced auth and response (OAuth 2.0, JWT, mTLS, timings, diff, code generation) | planned |
| M5 | Import/export (Postman, OpenAPI, cURL, HAR, Insomnia, Bruno) | planned |
| M6 | Automation (Runner, load tests, mock servers, monitors) | planned |
| M7 | Protocols (WebSocket, SSE, Socket.IO, GraphQL, gRPC, MQTT, SOAP) | planned |
| M8 | Collaboration (Git, versions, comments, OpenAPI editor and docs) | planned |
| M9 | Flows, plugins, optional AI assistant | planned |
| M10 | Polish (accessibility, localization, onboarding, updates, packaging, hardening) | planned |

