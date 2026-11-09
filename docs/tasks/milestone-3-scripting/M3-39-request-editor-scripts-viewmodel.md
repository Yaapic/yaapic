# M3-39: Request editor ViewModel: scripts

Status: done
Platform: any
Depends on: M3-03, M3-05

## Goal

The editable scripts of a request: two blocks (pre-request, post-response), each with a language and a source, tracked as part
of the request's dirty state and saved with it.

## Design decisions

- Read first: `src/Yaapic.ViewModels/Requests/RequestEditorViewModel.cs` (how `Headers`, `Auth` feed `ToDefinition`,
  `IsDirty`, `Title`, `MarkSaved`, `FromDefinition`; follow exactly the same notification pattern), `AuthEditorViewModel.cs`
  (a small child editor view model), `src/Yaapic.ViewModels/Editing/KeyValueEditorViewModel.cs`.
- New `src/Yaapic.ViewModels/Scripting/`:
  - `ScriptBlockEditorViewModel : ObservableObject`: `ScriptLanguage Language` (observable), `string Source` (observable),
    static `IReadOnlyList<ScriptLanguageChoice> Languages` (`sealed record ScriptLanguageChoice(ScriptLanguage Language, string Label)`:
    `JavaScript`, `Lua`), `bool IsEmpty`, `string Placeholder` (depends on language and phase, constants below),
    `ScriptBlock ToBlock()`, `void Load(ScriptBlock block)`, `event EventHandler? Changed`.
  - `ScriptsEditorViewModel : ObservableObject`: `PreRequest`, `PostResponse` (`ScriptBlockEditorViewModel`), `ScriptPhase
    SelectedPhase` (default `PreRequest`; the view switches between two editors with it), `ScriptBlockEditorViewModel Selected`,
    `int FilledCount` (0..2) and `string BadgeText` (`""` for 0, otherwise the count), `ScriptSet ToSet()`, `void Load(ScriptSet)`,
    `event EventHandler? Changed` raised when either block changes.
  - Placeholders (constants, tests pin them): JavaScript pre `// Runs before the request is sent.\n// pm.environment.set("requestId", pm.variables.replaceIn("{{$guid}}"));`;
    JavaScript post `// Runs after the response arrives.\n// pm.test("Status is 200", () => pm.response.to.have.status(200));`;
    Lua pre `-- Runs before the request is sent.\n-- pm.environment.set("requestId", pm.variables.replace_in("{{$guid}}"))`;
    Lua post `-- Runs after the response arrives.\n-- pm.test("Status is 200", function() pm.response.to.have.status(200) end)`.
- `RequestEditorViewModel`: new `ScriptsEditorViewModel Scripts { get; }`; `FromDefinition` loads `definition.Scripts`;
  `ToDefinition()` includes `Scripts.ToSet()`; `Scripts.Changed` raises the same dirty/title notifications as a header edit;
  `MarkSaved` needs nothing extra (baseline comparison covers it). The `BadgeText` also raises a property change on the editor
  (`ScriptsBadge`) for the tab header.
- Changing a block's **language** keeps the source (the user may paste code), and marks the request dirty.

## Out of scope

Running the scripts (M3-43), folder scripts (M3-40), views.

## Acceptance criteria

- [x] Tests (`Yaapic.ViewModels.Tests`): load/`ToSet` round trip for both languages; changing source or language marks the
      request dirty and `Title` gains the marker, `MarkSaved` clears it; `FilledCount`/`BadgeText` for 0, 1, 2 filled and
      whitespace-only blocks; placeholders per language and phase; `ToDefinition()` carries the scripts and a saved/reloaded
      request through `InMemoryRequestStore` keeps them; `SelectedPhase` switches `Selected`; existing request editor tests
      unchanged and green.
- [x] 0 warnings, XML docs, architecture tests green (ViewModels still free of UI frameworks), all tests green.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (published exes, stub servers, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m3-scripting` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`feat(viewmodels): edit request scripts`
