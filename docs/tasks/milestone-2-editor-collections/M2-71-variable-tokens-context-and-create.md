# M2-71: Variable tokens, context and create-variable popup model

Status: done
Platform: any
Depends on: M2-44, M2-46, M2-43

## Goal

One platform-neutral model that both apps use to colour `{{variables}}` **inside the text itself** (Postman / Bruno
style) and to drive the hover popup: token ranges with their resolution state, a per-request `VariableContext`, a
`VariablePopupViewModel` (details of a resolved variable, "Unresolved variable" with a create form for an unresolved one)
and the write path that creates or updates the variable in a chosen scope and refreshes the highlighting live. No view
code. This replaces the chip-strip idea of M2-46/M2-49/M2-52 (the chips are removed by M2-72; M2-52 is superseded by
M2-74).

## Why (read before coding)

The user reviewed the shipped chip strip under the URL bar (M2-49) and rejected it: Postman and Bruno colour the token
**in place** in the URL text (resolved vs unresolved colour) and show a popup on hover (Postman: name, "Unresolved
Variable", "This variable is not defined in the active collection, environment or globals."; Bruno: `{{baseUrl}} =
http://127.0.0.1:5199`, green when resolved and orange when not). A plain `TextBox`/`TextBlock` cannot colour
sub-ranges of editable text, so the views need a different control (M2-72 on WinUI, M2-74 on Avalonia). Whatever control
a platform uses, the **model is this card**, so the control only draws ranges and shows a popup; it never resolves a
variable or writes a value itself.

## Design decisions

- **Reuse** `VariableScanner` (M2-46) and `VariableHint` (name, `IsResolved`, `Value`, `Scope`); **change** the
  scanner so a single parser also returns positions; **keep** `RequestEditorViewModel.UrlVariables` /
  `HasUrlVariables` until M2-72 removes the chip strip (WinUI still binds them; do not break the build).
- **Core** (`src/Yaapic.Core/Variables/`):
  - `readonly record struct VariableSpan(int Start, int Length, string Name)` (public, XML docs): `Start`/`Length`
    cover the whole `{{ name }}` token **including the braces and inner spaces**; `Name` is the trimmed name; property
    `End` = `Start + Length`.
  - `VariableScanner.ScanSpans(string? text)` returns every occurrence, in order, with the exact token rules of
    `VariableResolver` (`{{`, non-empty trimmed name without braces, `}}`; anything else is plain text; spans never
    overlap). `Scan` is re-implemented on top of `ScanSpans` (distinct names, first-occurrence order) so there is one
    parser. Tests: the existing agreement table of M2-46 extended with offsets (`{{ a }}` -> Start 0, Length 7;
    `x{{a}}{{b}}` -> two spans; `{{{a}}}` and `{{a` and `{{}}` -> as `Resolve` treats them; repeated names -> two spans,
    one name).
- **Provider fix** (`src/Yaapic.ViewModels/Requests/VariableScopeProvider.cs`): `ScopesChanged` is currently **not**
  raised when the variables of the active environment change (only on active-environment switch, globals edits and
  collection refresh), so a variable created in the environment would not recolour the text. Also raise it when
  `EnvironmentsViewModel.ActiveEnvironment.Variables.Changed` fires (re-subscribe on `ActiveEnvironmentChanged`,
  unsubscribe from the previous item). Test: editing the active environment's rows raises the event; editing an inactive
  environment's rows does not.
- **Writing through the existing stores**, no new storage code. Extend `IVariableScopeProvider` (it already reaches the
  three view models; the only other implementer is `tests/Yaapic.TestDoubles/FakeVariableScopeProvider.cs`, update it):
  - `IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId)`: `Environment` when an environment is active,
    `Collection` when the request lives in a collection (top-level folder, see `FindCollectionVariables`), `Globals`
    always. **No Folder scope**: M2-43/M2-44 resolve only the top-level collection's variables, so a variable created on
    a sub-folder would never resolve; do not offer it, and say so in Notes (ADR 0008 may add folder scope later). No
    `Dynamic`.
  - `Task<string?> SetVariableAsync(Guid requestId, VariableScopeKind scope, string name, string value)` returning an
    error message or `null`. **Upsert**: if an enabled or disabled row with that exact name exists in the scope, set its
    value (and enable it); otherwise append a row. Implementation per scope, reusing view models:
    - Environment: the active `EnvironmentItemViewModel.Variables` (`KeyValueEditorViewModel.Load`/rows) then its save
      path used by `EnvironmentsViewModel` (`SaveCommand`); a `StorageException` becomes the returned message.
    - Globals: `GlobalsViewModel.Variables` then `SaveCommand`; honour `CanSave` (corrupt file: return `LastError`).
    - Collection: build a `FolderSettingsViewModel` for the collection folder (M2-43), set the row, run its save; its
      `Saved` event already triggers `CollectionTreeViewModel.RefreshAsync`, which raises `ScopesChanged`. Add a small
      public helper on `CollectionTreeViewModel` if needed (for example `FindCollectionNode(Guid requestId)`).
    - Known limitation to note in Notes: saving the environment or globals also saves any other unsaved edits of the same
      scope that an open dialog holds.
  - Validate the name with the scanner rules (non-empty, no braces; a token coming from a span always passes), reject a
    name starting with `$` (reserved for dynamic variables), and never write when the scope is not in
    `GetWritableScopes`.
- **`VariableToken`** (`Yaapic.ViewModels.Requests`): `sealed record VariableToken(int Start, int Length, VariableHint Hint)`
  with `Name`, `End`, `State` (`VariableTokenState` enum: `Resolved`, `Dynamic`, `Unresolved`; `Dynamic` when
  `Hint.Scope == Dynamic`).
- **`VariableContext`** (`Yaapic.ViewModels.Requests`, one per request tab, exposed as
  `RequestEditorViewModel.Variables`; created in the factories with the request `Id` and the scope provider):
  - `IReadOnlyList<VariableToken> Tokenize(string? text)`: `ScanSpans` + `scopes.GetScopes(id).TryResolve(...)`, same
    value/dynamic rules as the current `RefreshUrlVariables`. Pure and fast (called on every keystroke by the view;
    no allocation when the text has no `{{`).
  - `VariableHint Describe(string name)`.
  - `event EventHandler? Changed`: raised on `IVariableScopeProvider.ScopesChanged` **only while `IsActive`**
    (`RequestEditorViewModel.OnIsActiveChanged` sets `Variables.IsActive`; subscribe/unsubscribe exactly as
    `RefreshUrlVariables` does today, so closed tabs never leak). Views re-tokenise on `Changed` and on text change.
  - `VariablePopupViewModel CreatePopup(string name)`.
  - `event EventHandler<VariableScopeKind>? OpenRequested`: raised by the popup's "open" action; each platform wires it
    to the existing environments dialog (Environment, Globals) and folder settings dialog (Collection) in its view cards.
- **`VariablePopupViewModel`** (`ObservableObject`, CommunityToolkit source generators, no reflection):
  - Read-only: `Name`, `IsResolved`, `IsDynamic`, `Value` (as stored; null when dynamic), `Scope`, `ScopeLabel`
    ("Request" is not a scope in M2: the labels are `Environment`, `Collection`, `Globals`, `Dynamic`), `Title`
    (`"Unresolved variable"` when unresolved, otherwise the name), `Message` (exactly `"This variable is not defined in the
    active collection, environment or globals."` when unresolved, `"Changes every time the request is sent."` when
    dynamic, empty otherwise). The strings are public `const`s so tests and both views share them.
  - Form: `IReadOnlyList<VariableScopeChoice> ScopeChoices` (`sealed record VariableScopeChoice(VariableScopeKind Kind,
    string Label, bool IsEnabled, string? DisabledReason)`: Environment ("Active environment", disabled with reason
    "No environment is active" when none), Collection ("Collection", disabled "Request is not in a collection"), Globals;
    order Environment, Collection, Globals), `SelectedScope` (default: first enabled choice in that order; for a resolved
    variable its own scope), `EditableValue` (initial: stored value, or empty), `CanSubmit` (name valid, scope enabled,
    not dynamic, not busy), `IsBusy`, `LastError`, `SubmitLabel` (`"Create"` unresolved / `"Save"` resolved),
    `SubmitCommand` (async: `SetVariableAsync`; on success raise `Submitted`, the context re-tokenises through
    `ScopesChanged`; on failure set `LastError` and keep the form open), `OpenCommand` (raises
    `VariableContext.OpenRequested(Scope or SelectedScope)`).
  - A resolved variable shows the value read-only plus an **Edit** toggle (`IsEditing`) that reveals `EditableValue` and
    `SubmitCommand`; an unresolved one starts in form mode. Dynamic variables are never editable.
  - Secrets: values are shown as stored (the secret vault is M3), as in M2-46.
- Tests (`tests/Yaapic.ViewModels.Tests/`, `tests/Yaapic.Core.Tests/`): scanner spans table; `Tokenize` for environment,
  collection, globals, unknown and dynamic names with correct offsets and states; `Changed` raised after
  `RaiseScopesChanged` while active, not while inactive, and not after the tab became inactive again; provider raises
  `ScopesChanged` on active-environment variable edits; popup defaults and disabled choices (no environment, unfiled
  request); creating into each scope through real view models with the in-memory store doubles (the value appears in the
  store and the next `Tokenize` reports it resolved with that scope); update of an existing row (no duplicate row);
  store failure keeps the form open with `LastError`; `$name` and empty names rejected; the two message strings.

## Out of scope

Any view, control, colour or popup positioning; headers/params/body wiring (views decide which text fields get a
context); folder scope; autocomplete; variable rename or delete; request-level variables.

## Acceptance criteria

- [x] `dotnet build Yaapic.sln -c Release` (and `Yaapic.CrossPlatform.slnf`) 0 warnings; every public type and member has
      an XML doc comment; all tests green; architecture tests green (`Yaapic.ViewModels` still references no UI
      framework, no reflection).
- [x] The tests listed above exist and pass; existing `RequestEditorUrlVariablesTests` still pass unchanged.
- [x] Notes record: the final shape of the interface changes, the collection write path chosen, and the no-folder-scope
      decision.
- [x] SonarCloud issues of the changed files checked and fixed.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test) in the foreground; do not poll.
- Stop every process you launched (`dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m2-editor-collections` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`feat: model inline variable tokens, hover details and create-in-scope`

## Notes

- Interface: `IVariableScopeProvider` gained `GetWritableScopes(Guid)` and `SetVariableAsync(Guid, VariableScopeKind, string, string)`
  (returns an error message or `null`); `FakeVariableScopeProvider` records writes (`Writes`, `WritableScopes`, `WriteError`).
  `VariableScanner` gained `ScanSpans` and `IsValidName`; `Scan` is built on `ScanSpans`.
- Collection write path: `SetVariableAsync` builds a `FolderSettingsViewModel` for the collection returned by the new
  `CollectionTreeViewModel.FindCollection(requestId)`, upserts the row, runs its `SaveCommand`, then awaits
  `CollectionTreeViewModel.RefreshAsync()` itself (that editor is not the dialog's, so no `Saved` subscriber exists); the refresh
  raises `ScopesChanged`.
- No Folder scope: M2-43/M2-44 resolve only the top-level collection's variables, so a sub-folder variable would never
  resolve; ADR 0008 may add folder scope later.
- Known limitation: saving the environment or globals also saves other unsaved edits of the same scope held by an open dialog.
- SonarCloud: no open issue on the touched files (the API reported 0 open issues; the new code is analysed after the push).
