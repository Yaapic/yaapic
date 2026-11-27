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
| [M3-66](milestone-3-scripting/M3-66-end-to-end-secret-vault.md) | End-to-end tests: secret vault | any | M3-47, M3-64 | done |
| [M3-67](milestone-3-scripting/M3-67-milestone-docs.md) | Milestone 3 docs | any | M3-64, M3-65, M3-66, M3-32, M3-49 | done |
| [M3-68](milestone-3-scripting/M3-68-winui-visual-verification.md) | WinUI visual verification of the milestone 3 views | windows | M3-51, M3-52, M3-53, M3-54, M3-55, M3-56 | done |

### Milestone 3 summary

Delivered: pre-request and post-response scripts on requests, folders and collections (collection first, then folders, then the
request) in JavaScript (QuickJS-NG) and Lua 5.4 behind one `IScriptEngine` port, a Postman-style `pm.*` API (`pm.info`,
`pm.variables`, `pm.environment`, `pm.collectionVariables`, `pm.globals`, `pm.request`, `pm.response`, `pm.test`, `pm.expect`,
`pm.sendRequest`, read-only `pm.cookies`, `console`), limits (5 s, 64 MiB, 20 `pm.sendRequest` calls, 1 000 console entries per
send), the console panel and status bar, the response Tests tab, scripts persisted in the YAML files, and variable changes made
by scripts persisted through the stores. The engines are native libraries built from pinned sources by `native/Build-Natives.ps1`.
The secret vault: a `secret` flag on variable rows, an AES-256-GCM vault file per user and workspace outside the workspace,
key protectors (passphrase, DPAPI, macOS Keychain, Linux Secret Service), secret-aware stores, redaction of script output,
masking in the variable tables and popup, and the vault dialogs and status bar item on both front-ends. Both code editors
highlight JavaScript and Lua (LoomEditor 0.5.0 had no Lua grammar, so Lua was plain text until 0.6.0). End-to-end tests:
`Milestone3ScriptingEndToEndTests`, `Milestone3LuaEndToEndTests`, `Milestone3VaultEndToEndTests`; compatibility is documented
in [scripting-compatibility.md](../scripting-compatibility.md) and checked by the conformance suites. New dependencies:
QuickJS-NG, Lua, the Lua shim and `System.Security.Cryptography.ProtectedData` (see [dependencies.md](../dependencies.md)).

Deferred (ADRs 0009 and 0010): C# and Python scripting, npm modules and `require`, a script debugger, a snippets sidebar,
timers, `pm.vault`, `pm.execution.setNextRequest` and `pm.iterationData` (Runner, M6), JSON schema assertions, the cookie jar and
a Cookies tab (milestone 4 or later), `pm.request.auth`, creating a secret from the variable popup, a helper that moves auth
fields to secret variables, changing the vault passphrase or protector, sharing secrets between machines, request-level
variables, importing Postman scripts (M5).

Nothing is open in M3. Runtime checks not run by hand: the real macOS Keychain and Linux
`secret-tool` protectors (M3-49), the Avalonia vault windows end to end (M3-63), and the interactive and send steps of the folder
scripts dialogs (M3-52, M3-59; the send itself is covered by the end-to-end tests). The Linux and macOS native builds are built by
CI only (M3-15). M3-68 is done, so M3 is closed.

## Release: installer and automatic updates

Scope: Velopack installers for Windows, macOS and Linux, GitHub Releases as the update feed, `stable` and `preview` channels,
background download and apply on restart. Pulled forward from M10 (updates, packaging). Decisions in ADR 0011 (R-01, read it first).

| Card | Title | Platform | Depends on | Status |
|---|---|---|---|---|
| [R-01](milestone-release-distribution/R-01-adr-distribution.md) | ADR 0011, installer and automatic updates | any | none | done |
| [R-02](milestone-release-distribution/R-02-versioning-and-about.md) | Versioning from the tag and the About information | any | R-01 | done |
| [R-03](milestone-release-distribution/R-03-update-port-and-settings.md) | Update port, domain model and settings | any | R-02 | done |
| [R-04](milestone-release-distribution/R-04-velopack-adapter.md) | Velopack adapter and startup hook | any | R-03 | done |
| [R-05](milestone-release-distribution/R-05-update-viewmodel.md) | Update ViewModel and command palette entries | any | R-03 | done |
| [R-06](milestone-release-distribution/R-06-winui-update-ui.md) | WinUI update banner, settings and About | windows | R-05 | done |
| [R-07](milestone-release-distribution/R-07-avalonia-update-ui.md) | Avalonia update banner, settings and About | unix | R-05 | done |
| [R-08](milestone-release-distribution/R-08-release-workflow.md) | Release workflow and packaging | any | R-02, R-04 | in progress |
| [R-09](milestone-release-distribution/R-09-update-end-to-end-and-docs.md) | Update end-to-end tests and docs | any | R-06, R-07, R-08 | done |

Done except one acceptance step of R-08, which needs a real tag: the workflow is written and a dry run is the check on the pull
request, but the upload and merge across the three OS jobs, delta packages, a real update from one published version to the next, and
packing on Linux and macOS have not run yet (`docs/releasing.md`, "Checked by hand"). Checked locally on Windows: `vpk pack` of the
WinUI AOT build, a start from the packed layout, both update UIs with a scripted service (`docs/screenshots/updates`). Not checked:
the update UI on macOS and Linux, and signing (no certificate yet).

## UX improvements

| Card | Title | Platform | Depends on | Status |
|---|---|---|---|---|
| [U-01](milestone-ux-improvements/U-01-request-response-layout.md) | Request / response layout toggle (stacked or side by side) | any | none | done |
| [U-02](milestone-ux-improvements/U-02-script-api-yaapic-namespace.md) | `yaapic` script namespace replaces `pm` | any | none | done |
| [U-03](milestone-ux-improvements/U-03-script-editor-autocompletion.md) | Script editor autocompletion | any | U-02 | done |
| [U-04](milestone-ux-improvements/U-04-script-snippets.md) | Script snippets | any | U-02, U-03 | done |
| [U-05](milestone-ux-improvements/U-05-script-converter-postman-bruno.md) | Convert Postman and Bruno scripts to `yaapic` | any | U-02 | done |

## Personalization: language and accent colour

| Card | Title | Platform | Depends on | Status |
|---|---|---|---|---|
| [P-01](milestone-personalization/P-01-adr-localization-and-accent.md) | ADR: localization (FR/EN) and accent colour | any | none | done |
| [P-02](milestone-personalization/P-02-preferences-model-and-store.md) | Preferences model and store | any | P-01 | done |
| [P-03](milestone-personalization/P-03-string-catalogue-and-localizer.md) | String catalogue, generator and localizer | any | P-01, P-02 | done |
| [P-04](milestone-personalization/P-04-localize-viewmodels-and-core.md) | Localize the view model and Core texts | any | P-03 | done |
| [P-05](milestone-personalization/P-05-winui-localization.md) | WinUI: localize the views | windows | P-04 | done |
| [P-06](milestone-personalization/P-06-avalonia-localization.md) | Avalonia: localize the views | unix | P-04, P-05 | done |
| [P-07](milestone-personalization/P-07-accent-derivation.md) | Accent colour derivation | any | P-02 | done |
| [P-08](milestone-personalization/P-08-winui-accent-at-runtime.md) | WinUI: apply the accent at runtime | windows | P-07 | done |
| [P-09](milestone-personalization/P-09-avalonia-accent-at-runtime.md) | Avalonia: apply the accent at runtime | unix | P-07, P-08 | done |
| [P-10](milestone-personalization/P-10-settings-viewmodel.md) | Settings ViewModel | any | P-02, P-03, P-07 | done |
| [P-11](milestone-personalization/P-11-winui-settings-dialog.md) | WinUI: Settings dialog | windows | P-08, P-10 | done |
| [P-12](milestone-personalization/P-12-avalonia-settings-window.md) | Avalonia: Settings window | unix | P-09, P-10, P-11 | done |
| [P-13](milestone-personalization/P-13-end-to-end-and-docs.md) | End-to-end tests and docs | any | P-05, P-06, P-11, P-12 | done |

## Milestone 4: Advanced authentication and response tools

Scope: OAuth 2.0 (client credentials, authorization code with PKCE, password, refresh), JWT signing, Digest and AWS Signature v4 authentication; mTLS
client certificates; request timings and the HTTP version (with HTTP/3 opt-in); response diff; code generation (cURL, JavaScript, Python, C#,
PowerShell, Go); a JWT decoder. The decisions are in [ADR 0013](../adr/0013-advanced-authentication.md), [ADR 0014](../adr/0014-transport-certificates-timings.md)
and [ADR 0015](../adr/0015-response-diff-and-code-generation.md) (written by M4-01, M4-03 and M4-04, read them first). Build order: `any` cards
(Domain, YAML, Core, Infrastructure, ViewModels, composition), then `windows`, then `unix` cards, then the end-to-end tests and docs.

Slices: M4-01 to M4-04 the ADRs and the timing spike; M4-05 to M4-28 authentication (domain and YAML M4-05 to M4-08, the authenticator pipeline M4-09 and
M4-10, JWT M4-11 to M4-14, Digest M4-15 and M4-16, AWS M4-17 and M4-18, OAuth 2.0 M4-19 to M4-27); M4-29 to M4-37 client certificates, timings and TLS;
M4-38 and M4-39 the HTTP version; M4-40 to M4-49, M4-59 and M4-60 the view models; M4-46 to M4-48 the diff; M4-50 to M4-58 code generation; M4-61 the
composition; M4-62 to M4-69 WinUI; M4-70 to M4-77 Avalonia; M4-78 and M4-79 end-to-end tests; M4-80 docs; M4-81 the WinUI visual check.

Blocked (product decisions, each card states the question): M4-28 auth inherited from folders and collections, M4-37 skipping TLS verification or a custom CA,
M4-55 more code generation languages. No other card depends on them. Cards added after an answer are numbered after M4-81.

Sonnet-sized cards: each card is one session; the card text holds every decision (field names, algorithms, file shapes, strings, test vectors), so no card needs a design choice.

| Card | Title | Platform | Depends on | Status |
|---|---|---|---|---|
| [M4-01](milestone-4-advanced-auth-response/M4-01-adr-advanced-authentication.md) | ADR 0013: advanced authentication | any | none | done |
| [M4-02](milestone-4-advanced-auth-response/M4-02-spike-request-timing-capture.md) | Spike: which request phases .NET can report | any | none | done |
| [M4-03](milestone-4-advanced-auth-response/M4-03-adr-transport-certificates-timings.md) | ADR 0014: transport, client certificates, timings and HTTP version | any | M4-02 | done |
| [M4-04](milestone-4-advanced-auth-response/M4-04-adr-response-diff-and-code-generation.md) | ADR 0015: response diff and code generation | any | none | done |
| [M4-05](milestone-4-advanced-auth-response/M4-05-domain-oauth2-settings.md) | Domain: OAuth 2.0 settings and the new auth kinds | any | M4-01 | done |
| [M4-06](milestone-4-advanced-auth-response/M4-06-domain-jwt-and-aws-settings.md) | Domain: JWT and AWS Signature v4 settings | any | M4-05 | done |
| [M4-07](milestone-4-advanced-auth-response/M4-07-yaml-request-oauth2.md) | YAML request serializer: OAuth 2.0 | any | M4-05 | done |
| [M4-08](milestone-4-advanced-auth-response/M4-08-yaml-request-jwt-digest-aws.md) | YAML request serializer: JWT, Digest and AWS Signature v4 | any | M4-06, M4-07 | done |
| [M4-09](milestone-4-advanced-auth-response/M4-09-core-resolved-advanced-auth.md) | Core: resolved advanced authentication | any | M4-06 | done |
| [M4-10](milestone-4-advanced-auth-response/M4-10-core-authenticator-port-and-pipeline.md) | Core: authenticator port and send pipeline stage | any | M4-09 | done |
| [M4-11](milestone-4-advanced-auth-response/M4-11-core-jwt-hmac-signing.md) | Core: JWT signing with HMAC | any | M4-01 | done |
| [M4-12](milestone-4-advanced-auth-response/M4-12-core-jwt-rsa-ecdsa-signing.md) | Core: JWT signing with RSA and ECDSA | any | M4-11 | done |
| [M4-13](milestone-4-advanced-auth-response/M4-13-core-jwt-authenticator.md) | Core: JWT authenticator | any | M4-10, M4-12 | done |
| [M4-14](milestone-4-advanced-auth-response/M4-14-core-jwt-decoder.md) | Core: JWT decoder | any | M4-11 | done |
| [M4-15](milestone-4-advanced-auth-response/M4-15-core-digest-challenge-and-response.md) | Core: Digest challenge parsing and response computation | any | M4-01 | done |
| [M4-16](milestone-4-advanced-auth-response/M4-16-core-digest-authenticator.md) | Core: Digest authenticator | any | M4-10, M4-15 | done |
| [M4-17](milestone-4-advanced-auth-response/M4-17-core-aws-sigv4-signer.md) | Core: AWS Signature v4 signer | any | M4-01 | done |
| [M4-18](milestone-4-advanced-auth-response/M4-18-core-aws-sigv4-authenticator.md) | Core: AWS Signature v4 authenticator | any | M4-10, M4-17 | done |
| [M4-19](milestone-4-advanced-auth-response/M4-19-core-oauth2-token-model-and-cache.md) | Core: OAuth 2.0 token model and in-memory cache | any | M4-05 | done |
| [M4-20](milestone-4-advanced-auth-response/M4-20-core-oauth2-token-requests-and-parsing.md) | Core: OAuth 2.0 token requests and response parsing | any | M4-19 | done |
| [M4-21](milestone-4-advanced-auth-response/M4-21-core-oauth2-token-client.md) | Core: OAuth 2.0 token client (client credentials, password, refresh) | any | M4-20 | done |
| [M4-22](milestone-4-advanced-auth-response/M4-22-core-pkce-and-authorization-url.md) | Core: PKCE and the authorization request URL | any | M4-05, M4-11 | done |
| [M4-23](milestone-4-advanced-auth-response/M4-23-core-oauth2-authorization-code-flow.md) | Core: authorization-code flow ports and orchestration | any | M4-21, M4-22 | done |
| [M4-24](milestone-4-advanced-auth-response/M4-24-infra-loopback-redirect-receiver.md) | Infrastructure: loopback redirect receiver | any | M4-23 | done |
| [M4-25](milestone-4-advanced-auth-response/M4-25-infra-system-browser-launcher.md) | Infrastructure: system browser launcher | any | M4-23 | done |
| [M4-26](milestone-4-advanced-auth-response/M4-26-core-oauth2-auth-method.md) | Core: OAuth 2.0 authentication method | any | M4-10, M4-21 | done |
| [M4-27](milestone-4-advanced-auth-response/M4-27-core-composite-authenticator.md) | Core: the composite authenticator | any | M4-13, M4-16, M4-18, M4-26 | done |
| [M4-28](milestone-4-advanced-auth-response/M4-28-auth-inheritance-from-folders-and-collections.md) | Authentication inherited from folders and collections | any | M4-27 | blocked |
| [M4-29](milestone-4-advanced-auth-response/M4-29-domain-client-certificate.md) | Domain: client certificate model | any | M4-03 | done |
| [M4-30](milestone-4-advanced-auth-response/M4-30-core-client-certificate-matching.md) | Core: client certificate matching and provider port | any | M4-29 | done |
| [M4-31](milestone-4-advanced-auth-response/M4-31-infra-certificate-settings-store.md) | Infrastructure: the client certificate list store | any | M4-29 | done |
| [M4-32](milestone-4-advanced-auth-response/M4-32-vault-certificate-owner-kind.md) | Vault: certificate passphrases | any | M4-29 | done |
| [M4-33](milestone-4-advanced-auth-response/M4-33-infra-client-certificate-loader.md) | Infrastructure: client certificate loader and provider | any | M4-30, M4-31, M4-32 | done |
| [M4-34](milestone-4-advanced-auth-response/M4-34-infra-transport-client-certificates.md) | Infrastructure: send with a client certificate (mTLS) | any | M4-33 | done |
| [M4-35](milestone-4-advanced-auth-response/M4-35-domain-request-timings.md) | Domain: request timings | any | M4-03 | done |
| [M4-36](milestone-4-advanced-auth-response/M4-36-infra-capture-timings.md) | Infrastructure: capture request timings in the transport | any | M4-34, M4-35 | done |
| [M4-37](milestone-4-advanced-auth-response/M4-37-tls-verification-and-custom-ca.md) | Server certificate verification: skip or custom CA | any | M4-34 | blocked |
| [M4-38](milestone-4-advanced-auth-response/M4-38-domain-yaml-http-version.md) | Domain and YAML: HTTP version preference | any | M4-03 | done |
| [M4-39](milestone-4-advanced-auth-response/M4-39-infra-transport-http-version.md) | Infrastructure: send with the chosen HTTP version | any | M4-36, M4-38 | done |
| [M4-40](milestone-4-advanced-auth-response/M4-40-viewmodel-oauth2-editor.md) | ViewModel: OAuth 2.0 editor | any | M4-05, M4-22 | done |
| [M4-41](milestone-4-advanced-auth-response/M4-41-viewmodel-oauth2-token-actions.md) | ViewModel: OAuth 2.0 token actions and status | any | M4-21, M4-23, M4-40 | done |
| [M4-42](milestone-4-advanced-auth-response/M4-42-viewmodel-jwt-digest-aws-editors.md) | ViewModel: JWT and AWS Signature v4 editors | any | M4-06, M4-40 | done |
| [M4-43](milestone-4-advanced-auth-response/M4-43-viewmodel-auth-editor-integration.md) | ViewModel: the auth editor offers the new kinds | any | M4-08, M4-41, M4-42 | done |
| [M4-44](milestone-4-advanced-auth-response/M4-44-viewmodel-certificates.md) | ViewModel: client certificates settings | any | M4-31, M4-32 | done |
| [M4-45](milestone-4-advanced-auth-response/M4-45-viewmodel-response-timings.md) | ViewModel: response timings and protocol | any | M4-36, M4-39 | done |
| [M4-46](milestone-4-advanced-auth-response/M4-46-core-text-line-diff.md) | Core: line diff (Myers) | any | M4-04 | done |
| [M4-47](milestone-4-advanced-auth-response/M4-47-core-response-differ.md) | Core: response differ (headers and body) | any | M4-46 | done |
| [M4-48](milestone-4-advanced-auth-response/M4-48-viewmodel-response-diff.md) | ViewModel: response diff | any | M4-45, M4-47 | done |
| [M4-49](milestone-4-advanced-auth-response/M4-49-viewmodel-jwt-decoder.md) | ViewModel: JWT decoder | any | M4-14 | done |
| [M4-50](milestone-4-advanced-auth-response/M4-50-core-code-generation-model-and-registry.md) | Core: code generation model, registry and shared samples | any | M4-04, M4-09 | done |
| [M4-51](milestone-4-advanced-auth-response/M4-51-core-codegen-curl.md) | Core: code generator, cURL | any | M4-50 | done |
| [M4-52](milestone-4-advanced-auth-response/M4-52-core-codegen-javascript-fetch.md) | Core: code generator, JavaScript (fetch) | any | M4-50 | done |
| [M4-53](milestone-4-advanced-auth-response/M4-53-core-codegen-python-requests.md) | Core: code generator, Python (requests) | any | M4-50 | done |
| [M4-54](milestone-4-advanced-auth-response/M4-54-core-codegen-csharp-httpclient.md) | Core: code generator, C# (HttpClient) | any | M4-50 | done |
| [M4-55](milestone-4-advanced-auth-response/M4-55-more-code-generation-languages.md) | More code generation languages | any | M4-50 | blocked |
| [M4-56](milestone-4-advanced-auth-response/M4-56-core-codegen-powershell.md) | Core: code generator, PowerShell (Invoke-RestMethod) | any | M4-50 | done |
| [M4-57](milestone-4-advanced-auth-response/M4-57-core-codegen-go.md) | Core: code generator, Go (net/http) | any | M4-50 | done |
| [M4-58](milestone-4-advanced-auth-response/M4-58-viewmodel-code-generation.md) | ViewModel: code generation dialog | any | M4-51, M4-52, M4-53, M4-54, M4-56, M4-57 | done |
| [M4-59](milestone-4-advanced-auth-response/M4-59-viewmodel-http-version-setting.md) | ViewModel: HTTP version in the request settings | any | M4-38, M4-39 | done |
| [M4-60](milestone-4-advanced-auth-response/M4-60-viewmodel-palette-and-shell-entries.md) | ViewModel: palette commands and shell entries for the M4 tools | any | M4-41, M4-44, M4-48, M4-49, M4-58 | done |
| [M4-61](milestone-4-advanced-auth-response/M4-61-composition-m4-wiring.md) | Composition: wire the M4 services | any | M4-27, M4-24, M4-25, M4-33, M4-36, M4-39, M4-41, M4-43, M4-58, M4-59, M4-60 | done |
| [M4-62](milestone-4-advanced-auth-response/M4-62-winui-auth-oauth2.md) | WinUI: OAuth 2.0 in the Auth tab | windows | M4-43, M4-61 | done |
| [M4-63](milestone-4-advanced-auth-response/M4-63-winui-auth-jwt-digest-aws.md) | WinUI: JWT, Digest and AWS Signature v4 in the Auth tab | windows | M4-62 | done |
| [M4-64](milestone-4-advanced-auth-response/M4-64-winui-certificates-dialog.md) | WinUI: client certificates dialog | windows | M4-44, M4-61 | done |
| [M4-65](milestone-4-advanced-auth-response/M4-65-winui-jwt-decoder-dialog.md) | WinUI: JWT decoder dialog | windows | M4-49, M4-61 | done |
| [M4-66](milestone-4-advanced-auth-response/M4-66-winui-response-timings-tab.md) | WinUI: response Timing tab | windows | M4-45, M4-61 | done |
| [M4-67](milestone-4-advanced-auth-response/M4-67-winui-response-diff-tab.md) | WinUI: response Diff tab | windows | M4-48, M4-61 | done |
| [M4-68](milestone-4-advanced-auth-response/M4-68-winui-code-generation-dialog.md) | WinUI: code generation dialog | windows | M4-58, M4-61 | done |
| [M4-69](milestone-4-advanced-auth-response/M4-69-winui-http-version-setting.md) | WinUI: HTTP version in the Settings tab | windows | M4-59, M4-61 | done |
| [M4-70](milestone-4-advanced-auth-response/M4-70-avalonia-auth-oauth2.md) | Avalonia: OAuth 2.0 in the Auth tab | unix | M4-43, M4-61 | done |
| [M4-71](milestone-4-advanced-auth-response/M4-71-avalonia-auth-jwt-digest-aws.md) | Avalonia: JWT, Digest and AWS Signature v4 in the Auth tab | unix | M4-70 | done |
| [M4-72](milestone-4-advanced-auth-response/M4-72-avalonia-jwt-decoder-window.md) | Avalonia: JWT decoder window | unix | M4-49, M4-61 | done |
| [M4-73](milestone-4-advanced-auth-response/M4-73-avalonia-certificates-window.md) | Avalonia: client certificates window | unix | M4-44, M4-61 | done |
| [M4-74](milestone-4-advanced-auth-response/M4-74-avalonia-response-timings-tab.md) | Avalonia: response Timing tab | unix | M4-45, M4-61 | done |
| [M4-75](milestone-4-advanced-auth-response/M4-75-avalonia-response-diff-tab.md) | Avalonia: response Diff tab | unix | M4-48, M4-61 | done |
| [M4-76](milestone-4-advanced-auth-response/M4-76-avalonia-code-generation-window.md) | Avalonia: code generation window | unix | M4-58, M4-61 | done |
| [M4-77](milestone-4-advanced-auth-response/M4-77-avalonia-http-version-setting.md) | Avalonia: HTTP version in the Settings tab | unix | M4-59, M4-61 | done |
| [M4-78](milestone-4-advanced-auth-response/M4-78-end-to-end-authentication.md) | End-to-end tests: authentication | any | M4-27, M4-61 | done |
| [M4-79](milestone-4-advanced-auth-response/M4-79-end-to-end-mtls-timings-and-http-version.md) | End-to-end tests: mTLS, timings, HTTP version, diff and code generation | any | M4-39, M4-48, M4-58, M4-61 | done |
| [M4-80](milestone-4-advanced-auth-response/M4-80-milestone-docs.md) | Milestone 4 docs | any | M4-78, M4-79, M4-62, M4-63, M4-64, M4-65, M4-66, M4-67, M4-68, M4-69, M4-70, M4-71, M4-72, M4-73, M4-74, M4-75, M4-76, M4-77 | done |
| [M4-81](milestone-4-advanced-auth-response/M4-81-winui-visual-verification.md) | WinUI visual verification of the milestone 4 views | windows | M4-62, M4-63, M4-64, M4-65, M4-66, M4-67, M4-68, M4-69 | done |

### Milestone 4 summary

Delivered: authentication beyond Basic, Bearer and API key, resolved through an `IRequestAuthenticator` pipeline (`PendingAuth` on
the resolved request): OAuth 2.0 (client credentials, authorization code with PKCE through a loopback redirect receiver and the
system browser, password, automatic refresh, in-memory token cache), JWT signing (HS, RS and ES 256/384/512), Digest (RFC 7616,
body-less probe) and AWS Signature v4. Client certificates (mTLS) matched by host pattern, stored outside the workspace with
passphrases in the vault. Per-phase response timings (DNS, connect, TLS, waiting, download, reused connection) and the protocol
used, the per-request HTTP version (HTTP/3 opt-in), a response diff (previous response or pinned baseline), code generation (cURL,
JavaScript, Python, C#, PowerShell, Go) and a JWT decoder, on both front-ends, with palette entries. End-to-end tests:
`Milestone4AuthenticationEndToEndTests` and `Milestone4TransportEndToEndTests`. Documentation:
[authentication.md](../authentication.md), [response-tools.md](../response-tools.md), [architecture.md](../architecture.md),
[file-format.md](../file-format.md) and ADRs [0013](../adr/0013-advanced-authentication.md),
[0014](../adr/0014-transport-certificates-timings.md), [0015](../adr/0015-response-diff-and-code-generation.md). No new
dependency (see [dependencies.md](../dependencies.md)).

Deferred: auth inherited from folders and collections, tokens persisted across restarts, presigned AWS URLs and signed multipart
payloads, Digest `auth-int`, PS256, EdDSA and JWE, OAuth Implicit, Device Code and DPoP, a custom CA or skipping TLS
verification, proxy settings, diff against history entries, importing from cURL (M5).

Open (blocked, product decisions): M4-28 auth inheritance, M4-37 TLS verification and custom CA, M4-55 more code generation
languages.

Runtime checks not run by hand: HTTP/3 against a real QUIC server and the Avalonia views on macOS and Linux; the WinUI visual
check is M4-81.

## Milestone 5: Import and export

Scope: importing Postman collections (v2.0 and v2.1) and environments, OpenAPI 3.x and Swagger 2.0 (JSON or YAML), cURL commands, HAR files, Insomnia v4 exports and Bruno collection folders, with a report of what was converted, skipped or lost;
exporting to Postman v2.1 (collection and environment), OpenAPI 3.0 (inferred, JSON or YAML), Insomnia v4 and Bruno (zip). The decisions are in [ADR 0016](../adr/0016-import-export-architecture.md) and [ADR 0017](../adr/0017-import-export-formats.md)
(written by M5-01 and M5-03, read them first). Build order: ADRs and the YAML spike, then `any` cards (Core model and helpers, Infrastructure, importers per format, exporters, view models, composition), then `windows`, then `unix` cards, then the end-to-end tests, docs and the WinUI visual check.

Slices: M5-01 to M5-03 the ADRs and the spike; M5-04 to M5-11 the shared model, `DataNode`, helpers, applier, registry and file reader; M5-12 to M5-16 Postman import; M5-17 and M5-18 cURL; M5-19 to M5-24 OpenAPI and Swagger 2.0; M5-25 and M5-26 HAR; M5-27 to M5-30 Insomnia;
M5-31 to M5-35 Bruno; M5-36 and M5-37 the export model and packager; M5-38 to M5-41 Postman export; M5-42 and M5-43 OpenAPI export; M5-44 Insomnia export; M5-45 and M5-46 Bruno export; M5-47 and M5-48 blocked; M5-49 to M5-53 view models and shell entries; M5-54 the composition;
M5-55 to M5-59 WinUI; M5-60 to M5-64 Avalonia; M5-65 and M5-66 end-to-end tests; M5-67 docs; M5-68 the WinUI visual check.

Blocked (product decisions, each card states the question): M5-30 Insomnia 5 YAML import, M5-47 HAR export, M5-48 pasting a cURL command into the URL box. No other card depends on them. Cards added after an answer are numbered after M5-68.
cURL export is the existing code generation (M4-51), so it has no card.

Sonnet-sized cards: each card is one session; the card text holds every decision (field names, mappings, algorithms, file shapes, strings, test cases), so no card needs a design choice. Shared types: `ImportBundle`, `ImportReport`, `IImporter` (M5-04), `DataNode` (M5-05), `IExporter` (M5-36).

| Card | Title | Platform | Depends on | Status |
|---|---|---|---|---|
| [M5-01](milestone-5-import-export/M5-01-adr-import-export-architecture.md) | ADR 0016: import and export architecture | any | none | done |
| [M5-02](milestone-5-import-export/M5-02-spike-yaml-tree-under-aot.md) | Spike: read arbitrary YAML into a tree under Native AOT | any | none | done |
| [M5-03](milestone-5-import-export/M5-03-adr-import-export-formats.md) | ADR 0017: supported formats, mappings and losses | any | M5-01, M5-02 | done |
| [M5-04](milestone-5-import-export/M5-04-core-import-model.md) | Core: import model, report and importer contract | any | M5-01 | done |
| [M5-05](milestone-5-import-export/M5-05-core-data-node-and-json.md) | Core: DataNode tree, JSON reading and writing | any | M5-01 | done |
| [M5-06](milestone-5-import-export/M5-06-core-import-helpers.md) | Core: import helpers (methods, URLs, names, dynamic variables) | any | M5-04 | done |
| [M5-07](milestone-5-import-export/M5-07-infra-vyaml-tree-reader.md) | Infrastructure: YAML tree reader (VYaml) | any | M5-02, M5-05 | done |
| [M5-08](milestone-5-import-export/M5-08-core-data-node-yaml-writer.md) | Core: DataNode YAML writer | any | M5-05 | done |
| [M5-09](milestone-5-import-export/M5-09-core-import-applier.md) | Core: import applier | any | M5-04 | done |
| [M5-10](milestone-5-import-export/M5-10-core-importer-registry-and-service.md) | Core: importer registry, import service and input reader port | any | M5-04, M5-09 | done |
| [M5-11](milestone-5-import-export/M5-11-infra-import-input-reader.md) | Infrastructure: import input reader (file and folder) | any | M5-10 | done |
| [M5-12](milestone-5-import-export/M5-12-core-postman-collection-requests.md) | Core: Postman collection import, folders, requests, URLs and headers | any | M5-05, M5-06 | done |
| [M5-13](milestone-5-import-export/M5-13-core-postman-bodies.md) | Core: Postman import, request bodies | any | M5-12 | done |
| [M5-14](milestone-5-import-export/M5-14-core-postman-auth.md) | Core: Postman import, authentication | any | M5-12 | done |
| [M5-15](milestone-5-import-export/M5-15-core-postman-scripts-and-variables.md) | Core: Postman import, scripts and collection variables | any | M5-12 | done |
| [M5-16](milestone-5-import-export/M5-16-core-postman-environment-import.md) | Core: Postman environment and globals import | any | M5-04, M5-05 | done |
| [M5-17](milestone-5-import-export/M5-17-core-curl-tokenizer.md) | Core: cURL command tokenizer | any | M5-04 | done |
| [M5-18](milestone-5-import-export/M5-18-core-curl-importer.md) | Core: cURL importer | any | M5-06, M5-17 | done |
| [M5-19](milestone-5-import-export/M5-19-core-openapi-document-and-refs.md) | Core: OpenAPI document reading and `$ref` resolution | any | M5-05 | done |
| [M5-20](milestone-5-import-export/M5-20-core-openapi-operations-and-parameters.md) | Core: OpenAPI import, operations, servers and parameters | any | M5-06, M5-19 | done |
| [M5-21](milestone-5-import-export/M5-21-core-openapi-schema-examples.md) | Core: OpenAPI schema example generator | any | M5-19 | done |
| [M5-22](milestone-5-import-export/M5-22-core-openapi-request-bodies.md) | Core: OpenAPI import, request bodies | any | M5-20, M5-21 | done |
| [M5-23](milestone-5-import-export/M5-23-core-openapi-security.md) | Core: OpenAPI import, security schemes to authentication | any | M5-20 | done |
| [M5-24](milestone-5-import-export/M5-24-core-swagger2-normalizer.md) | Core: Swagger 2.0 normalization | any | M5-19 | done |
| [M5-25](milestone-5-import-export/M5-25-core-har-entries.md) | Core: HAR import, entries to requests | any | M5-05, M5-06 | done |
| [M5-26](milestone-5-import-export/M5-26-core-har-filtering-and-duplicates.md) | Core: HAR import, static resources, preflights and duplicates | any | M5-25 | done |
| [M5-27](milestone-5-import-export/M5-27-core-insomnia-requests.md) | Core: Insomnia v4 import, workspace, folders, requests and bodies | any | M5-05, M5-06 | done |
| [M5-28](milestone-5-import-export/M5-28-core-insomnia-auth-and-template-tags.md) | Core: Insomnia import, authentication and template tags | any | M5-27 | done |
| [M5-29](milestone-5-import-export/M5-29-core-insomnia-environments.md) | Core: Insomnia import, environments | any | M5-27, M5-28 | done |
| [M5-30](milestone-5-import-export/M5-30-insomnia-5-yaml-import.md) | Insomnia 5 YAML import | any | M5-07, M5-27 | blocked |
| [M5-31](milestone-5-import-export/M5-31-core-bru-block-parser.md) | Core: Bruno `.bru` block parser | any | M5-04 | done |
| [M5-32](milestone-5-import-export/M5-32-core-bruno-requests.md) | Core: Bruno import, request files | any | M5-06, M5-31 | done |
| [M5-33](milestone-5-import-export/M5-33-core-bruno-collection-structure.md) | Core: Bruno import, collection structure and auth inheritance | any | M5-32 | done |
| [M5-34](milestone-5-import-export/M5-34-core-bruno-scripts-vars-and-tests.md) | Core: Bruno import, scripts, variables and tests | any | M5-33 | done |
| [M5-35](milestone-5-import-export/M5-35-core-bruno-environments.md) | Core: Bruno import, environments | any | M5-31, M5-33 | done |
| [M5-36](milestone-5-import-export/M5-36-core-export-model-and-source-reader.md) | Core: export model, contract, registry and source reader | any | M5-04 | done |
| [M5-37](milestone-5-import-export/M5-37-core-export-packager.md) | Core: export packager (zip) | any | M5-36 | done |
| [M5-38](milestone-5-import-export/M5-38-core-postman-export-requests.md) | Core: Postman collection export, folders, requests, URLs, headers and bodies | any | M5-05, M5-36 | done |
| [M5-39](milestone-5-import-export/M5-39-core-postman-export-auth-and-variables.md) | Core: Postman export, authentication and collection variables | any | M5-38 | done |
| [M5-40](milestone-5-import-export/M5-40-core-postman-export-scripts.md) | Core: Postman export, scripts | any | M5-39 | done |
| [M5-41](milestone-5-import-export/M5-41-core-postman-environment-export.md) | Core: Postman environment and globals export | any | M5-05, M5-36 | done |
| [M5-42](milestone-5-import-export/M5-42-core-openapi-export-paths-and-parameters.md) | Core: OpenAPI export, info, servers, paths, operations and parameters | any | M5-05, M5-36 | done |
| [M5-43](milestone-5-import-export/M5-43-core-openapi-export-bodies-security-and-exporters.md) | Core: OpenAPI export, bodies, security and the JSON and YAML exporters | any | M5-42, M5-08 | done |
| [M5-44](milestone-5-import-export/M5-44-core-insomnia-export.md) | Core: Insomnia v4 export | any | M5-05, M5-36 | done |
| [M5-45](milestone-5-import-export/M5-45-core-bruno-export-request-files.md) | Core: Bruno export, request files | any | M5-36 | done |
| [M5-46](milestone-5-import-export/M5-46-core-bruno-export-collection-and-zip.md) | Core: Bruno export, collection layout, environments and the exporter | any | M5-45, M5-37 | done |
| [M5-47](milestone-5-import-export/M5-47-har-export.md) | HAR export | any | M5-36 | blocked |
| [M5-48](milestone-5-import-export/M5-48-paste-curl-into-url-box.md) | Paste a cURL command into the URL box | any | M5-18 | blocked |
| [M5-49](milestone-5-import-export/M5-49-ui-abstractions-pick-folder.md) | UI abstractions: pick a folder | any | M5-01 | done |
| [M5-50](milestone-5-import-export/M5-50-viewmodel-import-report.md) | ViewModel: import report | any | M5-04 | done |
| [M5-51](milestone-5-import-export/M5-51-viewmodel-import.md) | ViewModel: import | any | M5-10, M5-49, M5-50 | done |
| [M5-52](milestone-5-import-export/M5-52-viewmodel-export.md) | ViewModel: export | any | M5-36, M5-37, M5-49 | done |
| [M5-53](milestone-5-import-export/M5-53-viewmodel-palette-and-shell-entries.md) | ViewModel: palette commands and shell entries for import and export | any | M5-51, M5-52 | done |
| [M5-54](milestone-5-import-export/M5-54-composition-m5-wiring.md) | Composition: wire the M5 services | any | M5-07, M5-08, M5-09, M5-10, M5-11, M5-13, M5-14, M5-15, M5-16, M5-18, M5-22, M5-23, M5-24, M5-26, M5-29, M5-34, M5-35, M5-37, M5-40, M5-41, M5-43, M5-44, M5-46, M5-53 | done |
| [M5-55](milestone-5-import-export/M5-55-winui-pick-folder.md) | WinUI: folder picker | windows | M5-49, M5-54 | done |
| [M5-56](milestone-5-import-export/M5-56-winui-import-report-view.md) | WinUI: import report view | windows | M5-50, M5-54 | done |
| [M5-57](milestone-5-import-export/M5-57-winui-import-dialog.md) | WinUI: import dialog | windows | M5-51, M5-55, M5-56 | done |
| [M5-58](milestone-5-import-export/M5-58-winui-export-dialog.md) | WinUI: export dialog | windows | M5-52, M5-55, M5-56 | done |
| [M5-59](milestone-5-import-export/M5-59-winui-import-export-entry-points.md) | WinUI: import and export entry points | windows | M5-53, M5-57, M5-58 | done |
| [M5-60](milestone-5-import-export/M5-60-avalonia-pick-folder.md) | Avalonia: folder picker | unix | M5-49, M5-54, M5-55 | done |
| [M5-61](milestone-5-import-export/M5-61-avalonia-import-report-view.md) | Avalonia: import report view | unix | M5-50, M5-54, M5-56 | done |
| [M5-62](milestone-5-import-export/M5-62-avalonia-import-window.md) | Avalonia: import window | unix | M5-51, M5-57, M5-60, M5-61 | done |
| [M5-63](milestone-5-import-export/M5-63-avalonia-export-window.md) | Avalonia: export window | unix | M5-52, M5-58, M5-60, M5-61 | done |
| [M5-64](milestone-5-import-export/M5-64-avalonia-import-export-entry-points.md) | Avalonia: import and export entry points | unix | M5-53, M5-59, M5-62, M5-63 | done |
| [M5-65](milestone-5-import-export/M5-65-end-to-end-import.md) | End-to-end tests: import | any | M5-54 | done |
| [M5-66](milestone-5-import-export/M5-66-end-to-end-export-and-round-trips.md) | End-to-end tests: export and round trips | any | M5-54 | done |
| [M5-67](milestone-5-import-export/M5-67-milestone-docs.md) | Milestone 5 docs | any | M5-65, M5-66, M5-57, M5-58, M5-59, M5-62, M5-63, M5-64 | done |
| [M5-68](milestone-5-import-export/M5-68-winui-visual-verification.md) | WinUI visual verification of the milestone 5 views | windows | M5-56, M5-57, M5-58, M5-59 | done |

### Milestone 5 summary

Delivered: import of Postman (collections v2.0 and v2.1, environments and globals), OpenAPI 3.x and Swagger 2.0 (JSON or YAML),
cURL, HAR, Insomnia v4 and Bruno folders, through a neutral `DataNode` tree (JSON from the framework, YAML through VYaml), with a
preview and a report (Info, Warning, Skipped) before the `ImportApplier` writes anything through the secret-aware stores; export
to Postman v2.1 (collection and environment), OpenAPI 3.0 (inferred, JSON or YAML), Insomnia v4 and Bruno (zip), with secret
values never exported; import and export windows, report view, sidebar, context-menu and palette entries on both front-ends.
End-to-end tests: `Milestone5ImportEndToEndTests` and `Milestone5ExportEndToEndTests` (with the round trips).
Documentation: [import-export.md](../import-export.md), [architecture.md](../architecture.md), [file-format.md](../file-format.md),
[scripting-compatibility.md](../scripting-compatibility.md) and ADRs [0016](../adr/0016-import-export-architecture.md),
[0017](../adr/0017-import-export-formats.md). No new dependency (see [dependencies.md](../dependencies.md)).

Deferred: undo of an import, import from a URL, drag and drop of files, history export, the Postman cloud API, OpenAPI editing,
gRPC, GraphQL and WebSocket items (milestone 7), Postman v1, WSDL/SOAP, Bruno OpenCollection YAML.

Open (blocked, product decisions): M5-30 Insomnia 5 YAML import, M5-47 HAR export, M5-48 pasting a cURL command into the URL box.

Runtime checks not run by hand: the Avalonia views on macOS and Linux, and imports of real exports (the Insomnia fixtures are
written by hand). The WinUI visual check is done: [screenshots](../screenshots/m5/README.md).

## Milestone 6: Automation (Runner, load tests, mock servers, monitors)

Scope: the collection Runner (iterations, CSV and JSON data files, a data variable layer, `yaapic.iterationData`, `yaapic.execution.setNextRequest` and `skipRequest`, results, JSON and JUnit reports), load tests (virtual users, fixed and ramp-up profiles, live metrics and charts, JSON report), local mock servers (routes in `mocks/*.mock.yaml`, templating, CORS, request log, generation from a folder, `127.0.0.1` only) and monitors (scheduled folder runs while the app is open, `monitors/*.monitor.yaml`, per-user run history). The decisions are in ADR 0018 (Runner), ADR 0019 (load tests), ADR 0020 (mock servers) and ADR 0021 (monitors), written by M6-01, M6-03, M6-04 and M6-05; read them first. Build order: ADRs and the load spike, then `any` cards (Core, Scripting, Infrastructure, view models, composition), then `windows`, then `unix` cards, then the end-to-end tests, docs and the WinUI visual check.

Slices: M6-01 to M6-05 the ADRs and the spike; M6-06 to M6-13 the data layer, data files and the script API (host, JavaScript, Lua, converter and completion); M6-14 to M6-21 the Runner (model, variable state, runner, flow control, reports, view models); M6-22 to M6-26 load tests; M6-27 to M6-35 mock servers (model, store, matcher, responder, generator, HTTP/1.1 server, service, view model); M6-36 to M6-40 monitors (model, stores, scheduler, view model); M6-41 the shell entries; M6-42 the composition; M6-43 to M6-49 WinUI; M6-50 to M6-56 Avalonia; M6-57 to M6-59 end-to-end tests; M6-60 docs; M6-61 the WinUI visual check.

Blocked (product decisions, each card states the question): M6-62 a command-line runner, M6-63 monitors while Yaapic is closed, M6-64 mock servers reachable from other devices. No other card depends on them. Cards added after an answer are numbered after M6-64.

Sonnet-sized cards: each card is one session; the card text holds every decision (types, signatures, rules, file shapes, strings, test cases), so no card needs a design choice. Shared types: `RunPlan`, `RunResult`, `RunStepExecutor`, `RunVariableState`, `IRunScopeSource` (M6-14 to M6-16), `ScriptRunContext` and `ScriptExecutionControl` (M6-09), `MockHttpRequest`/`MockHttpResponse` (M6-29), `IMockHttpHost` (M6-33), `MonitorRun` and the monitor store ports (M6-36), `ContextProgress<T>` (M6-21), `IDialogService.ConfirmAsync` (M6-26).

| Card | Title | Platform | Depends on | Status |
|---|---|---|---|---|
| [M6-01](milestone-6-automation/M6-01-adr-collection-runner.md) | ADR 0018: collection runner, iteration data and run reports | any | none | done |
| [M6-02](milestone-6-automation/M6-02-spike-concurrent-scripted-sends.md) | Spike: concurrent scripted sends under load | any | none | done |
| [M6-03](milestone-6-automation/M6-03-adr-load-tests.md) | ADR 0019: load tests | any | M6-01, M6-02 | done |
| [M6-04](milestone-6-automation/M6-04-adr-mock-servers.md) | ADR 0020: mock servers | any | none | done |
| [M6-05](milestone-6-automation/M6-05-adr-monitors.md) | ADR 0021: monitors | any | M6-01 | done |
| [M6-06](milestone-6-automation/M6-06-core-data-variable-layer.md) | Core: data variable layer | any | M6-01 | done |
| [M6-07](milestone-6-automation/M6-07-core-csv-iteration-data.md) | Core: CSV iteration data parser | any | M6-01 | done |
| [M6-08](milestone-6-automation/M6-08-core-json-iteration-data-and-reader.md) | Core: JSON iteration data and the data file reader | any | M6-07 | done |
| [M6-09](milestone-6-automation/M6-09-core-script-run-context.md) | Core: run context for scripts (iteration, data, execution control) | any | M6-06 | done |
| [M6-10](milestone-6-automation/M6-10-scripting-host-ops-run-context.md) | Scripting: host operations for iteration data and execution control | any | M6-09 | done |
| [M6-11](milestone-6-automation/M6-11-js-iteration-data-and-execution.md) | JavaScript: `yaapic.iterationData` and `yaapic.execution` | any | M6-10 | done |
| [M6-12](milestone-6-automation/M6-12-lua-iteration-data-and-execution.md) | Lua: `yaapic.iteration_data` and `yaapic.execution` | any | M6-10 | done |
| [M6-13](milestone-6-automation/M6-13-converter-autocompletion-runner-apis.md) | Converter, autocompletion and snippets for the runner APIs | any | M6-11, M6-12 | done |
| [M6-14](milestone-6-automation/M6-14-core-run-plan-and-results.md) | Core: run plan and result model | any | M6-07, M6-09 | done |
| [M6-15](milestone-6-automation/M6-15-core-run-variable-state.md) | Core: run variable state and scope source port | any | M6-06, M6-14 | done |
| [M6-16](milestone-6-automation/M6-16-core-collection-runner.md) | Core: collection runner (step executor, iterations, delay, stop and cancel) | any | M6-09, M6-15 | done |
| [M6-17](milestone-6-automation/M6-17-core-runner-flow-control.md) | Core: runner flow control (setNextRequest, skipRequest, loop guard) | any | M6-16 | done |
| [M6-18](milestone-6-automation/M6-18-core-run-report-json.md) | Core: run report, JSON | any | M6-14 | done |
| [M6-19](milestone-6-automation/M6-19-core-run-report-junit.md) | Core: run report, JUnit XML | any | M6-14 | done |
| [M6-20](milestone-6-automation/M6-20-viewmodel-runner-setup.md) | ViewModel: runner setup | any | M6-08, M6-14 | done |
| [M6-21](milestone-6-automation/M6-21-viewmodel-runner-run-and-results.md) | ViewModel: runner run and results | any | M6-17, M6-18, M6-19, M6-20 | done |
| [M6-22](milestone-6-automation/M6-22-core-load-profile-schedule.md) | Core: load-test plan and virtual-user schedule | any | M6-03, M6-14 | done |
| [M6-23](milestone-6-automation/M6-23-core-load-metrics.md) | Core: load metrics aggregator | any | M6-03, M6-22 | done |
| [M6-24](milestone-6-automation/M6-24-core-load-test-runner.md) | Core: load-test runner | any | M6-16, M6-22, M6-23 | done |
| [M6-25](milestone-6-automation/M6-25-core-load-test-report-json.md) | Core: load-test report, JSON | any | M6-24 | done |
| [M6-26](milestone-6-automation/M6-26-viewmodel-load-test.md) | ViewModel: load test | any | M6-20, M6-21, M6-25 | done |
| [M6-27](milestone-6-automation/M6-27-domain-mock-server-model.md) | Domain: mock server model and validation | any | M6-04 | done |
| [M6-28](milestone-6-automation/M6-28-infra-mock-server-store.md) | Infrastructure: mock server file store | any | M6-27 | done |
| [M6-29](milestone-6-automation/M6-29-core-mock-route-matcher.md) | Core: mock HTTP types and route matcher | any | M6-27 | done |
| [M6-30](milestone-6-automation/M6-30-core-mock-responder.md) | Core: mock responder and templating | any | M6-29 | done |
| [M6-31](milestone-6-automation/M6-31-core-mock-from-collection.md) | Core: generate a mock server from a folder | any | M6-14, M6-27 | done |
| [M6-32](milestone-6-automation/M6-32-infra-http11-reader-writer.md) | Infrastructure: HTTP/1.1 request reader and response writer | any | M6-29 | done |
| [M6-33](milestone-6-automation/M6-33-infra-loopback-mock-host.md) | Infrastructure: loopback HTTP host for mock servers | any | M6-32 | done |
| [M6-34](milestone-6-automation/M6-34-core-mock-server-service.md) | Core: mock server service | any | M6-28, M6-30, M6-33 | done |
| [M6-35](milestone-6-automation/M6-35-viewmodel-mock-servers.md) | ViewModel: mock servers | any | M6-21, M6-31, M6-34 | done |
| [M6-36](milestone-6-automation/M6-36-domain-core-monitor-model.md) | Domain and Core: monitor model and store ports | any | M6-05, M6-14 | done |
| [M6-37](milestone-6-automation/M6-37-infra-monitor-store.md) | Infrastructure: monitor file store | any | M6-28, M6-36 | done |
| [M6-38](milestone-6-automation/M6-38-infra-monitor-run-store.md) | Infrastructure: monitor run history store | any | M6-36 | done |
| [M6-39](milestone-6-automation/M6-39-core-monitor-scheduler.md) | Core: monitor scheduler | any | M6-16, M6-36 | done |
| [M6-40](milestone-6-automation/M6-40-viewmodel-monitors.md) | ViewModel: monitors | any | M6-21, M6-39 | done |
| [M6-41](milestone-6-automation/M6-41-viewmodel-shell-entries.md) | ViewModel: palette commands, shell entries and status items for the M6 tools | any | M6-21, M6-26, M6-35, M6-40 | done |
| [M6-42](milestone-6-automation/M6-42-composition-m6-wiring.md) | Composition: wire the M6 services | any | M6-28, M6-33, M6-37, M6-38, M6-41 | done |
| [M6-43](milestone-6-automation/M6-43-winui-tool-window-host.md) | WinUI: tool window host and line chart | windows | M6-42 | done |
| [M6-44](milestone-6-automation/M6-44-winui-runner-setup.md) | WinUI: Runner window, setup pane | windows | M6-43 | done |
| [M6-45](milestone-6-automation/M6-45-winui-runner-results.md) | WinUI: Runner window, results pane | windows | M6-44 | done |
| [M6-46](milestone-6-automation/M6-46-winui-load-test-window.md) | WinUI: Load test window | windows | M6-43, M6-44 | done |
| [M6-47](milestone-6-automation/M6-47-winui-mock-servers-window.md) | WinUI: Mock servers window | windows | M6-43 | done |
| [M6-48](milestone-6-automation/M6-48-winui-monitors-window.md) | WinUI: Monitors window | windows | M6-43 | done |
| [M6-49](milestone-6-automation/M6-49-winui-automation-entry-points.md) | WinUI: automation entry points and status bar items | windows | M6-45, M6-46, M6-47, M6-48 | todo |
| [M6-50](milestone-6-automation/M6-50-avalonia-tool-windows-and-chart.md) | Avalonia: tool windows, line chart and confirmation dialog | unix | M6-42, M6-43 | todo |
| [M6-51](milestone-6-automation/M6-51-avalonia-runner-setup.md) | Avalonia: Runner window, setup pane | unix | M6-50, M6-44 | todo |
| [M6-52](milestone-6-automation/M6-52-avalonia-runner-results.md) | Avalonia: Runner window, results pane | unix | M6-51, M6-45 | todo |
| [M6-53](milestone-6-automation/M6-53-avalonia-load-test-window.md) | Avalonia: Load test window | unix | M6-51, M6-46 | todo |
| [M6-54](milestone-6-automation/M6-54-avalonia-mock-servers-window.md) | Avalonia: Mock servers window | unix | M6-50, M6-47 | todo |
| [M6-55](milestone-6-automation/M6-55-avalonia-monitors-window.md) | Avalonia: Monitors window | unix | M6-50, M6-48 | todo |
| [M6-56](milestone-6-automation/M6-56-avalonia-automation-entry-points.md) | Avalonia: automation entry points and status bar items | unix | M6-49, M6-52, M6-53, M6-54, M6-55 | todo |
| [M6-57](milestone-6-automation/M6-57-end-to-end-runner.md) | End-to-end tests: Runner | any | M6-42 | todo |
| [M6-58](milestone-6-automation/M6-58-end-to-end-mocks-and-load.md) | End-to-end tests: mock servers and load tests | any | M6-42 | todo |
| [M6-59](milestone-6-automation/M6-59-end-to-end-monitors.md) | End-to-end tests: monitors | any | M6-42 | todo |
| [M6-60](milestone-6-automation/M6-60-milestone-docs.md) | Milestone 6 docs | any | M6-57, M6-58, M6-59, M6-49, M6-56 | todo |
| [M6-61](milestone-6-automation/M6-61-winui-visual-verification.md) | WinUI visual verification of the milestone 6 views | windows | M6-45, M6-46, M6-47, M6-48, M6-49 | todo |
| [M6-62](milestone-6-automation/M6-62-command-line-runner.md) | Command-line runner | any | M6-17, M6-18, M6-19 | blocked |
| [M6-63](milestone-6-automation/M6-63-monitors-while-closed.md) | Monitors while Yaapic is closed | any | M6-39 | blocked |
| [M6-64](milestone-6-automation/M6-64-mock-servers-on-the-network.md) | Mock servers reachable from other devices | any | M6-34 | blocked |

## Milestones 2 to 10

| Milestone | Scope | Status |
|---|---|---|
| M2 | Full request editor, collections, environments, history, command palette | detailed (77 cards, see above) |
| M3 | Scripting (JS + Lua), pm.*, console, test results, secret vault | detailed (68 cards, see above) |
| M4 | Advanced auth and response (OAuth 2.0, JWT, mTLS, timings, diff, code generation) | detailed (81 cards, see above) |
| M5 | Import/export (Postman, OpenAPI, cURL, HAR, Insomnia, Bruno) | detailed (68 cards, see above) |
| M6 | Automation (Runner, load tests, mock servers, monitors) | detailed (64 cards, see above) |
| M7 | Protocols (WebSocket, SSE, Socket.IO, GraphQL, gRPC, MQTT, SOAP) | planned |
| M8 | Collaboration (Git, versions, comments, OpenAPI editor and docs) | planned |
| M9 | Flows, plugins, optional AI assistant | planned |
| M10 | Polish (accessibility, localization, onboarding, hardening; updates and packaging moved to the Release cards) | planned |
| R | Installer and automatic updates (Velopack), 9 cards, see above | detailed |

