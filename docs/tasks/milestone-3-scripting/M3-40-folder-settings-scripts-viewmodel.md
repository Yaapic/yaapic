# M3-40: Folder and collection settings ViewModel: scripts

Status: todo
Platform: any
Depends on: M3-39, M3-06

## Goal

Folders and collections can edit and save their scripts from the same settings view model that already edits their name,
description and variables.

## Design decisions

- Read first: `src/Yaapic.ViewModels/Collections/FolderSettingsViewModel.cs` (`IsDirty`, `Saved`, `Variables`, `ShowVariables`,
  save path), M3-39 (`ScriptsEditorViewModel`), M2-71's use of `FolderSettingsViewModel` in `VariableScopeProvider`.
- `FolderSettingsViewModel` gains `ScriptsEditorViewModel Scripts { get; }` (loaded from `FolderDefinition.Scripts`),
  `bool ShowScripts => true` (folders **and** collections; unlike variables, scripts exist on every level, ADR 0009), `IsDirty`
  includes the scripts, the save path builds the `FolderDefinition` with `Scripts.ToSet()` (keeping `Variables` rules unchanged),
  revert/cancel paths reset the scripts like the other fields.
- `CollectionTreeViewModel`: after a save, `RefreshAsync` already reloads the tree, so `CollectionTreeScriptChainProvider`
  (M3-10) sees the new scripts; add a test proving it end to end with the in-memory store.
- A folder-level pre-request or post-response script applies to **every request below the folder**: the dialog shows a
  hint text constant `FolderSettingsViewModel.ScriptsHint` = `"Runs for every request in this folder, before the request's own script."`
  (and the collection variant `"Runs for every request in this collection, before the folder and request scripts."`), selected by
  `IsCollection`.

## Out of scope

Views (M3-52, M3-59), running scripts.

## Acceptance criteria

- [ ] Tests: dirty tracking for source and language; save writes the scripts to the store double and reloads them; scripts of a
      nested folder do not affect the collection's; hint text per level; cancel/revert restores; chain provider returns the saved
      scripts after the save; existing folder settings tests unchanged.
- [ ] 0 warnings, XML docs, all tests green.

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

`feat(viewmodels): edit folder and collection scripts`
