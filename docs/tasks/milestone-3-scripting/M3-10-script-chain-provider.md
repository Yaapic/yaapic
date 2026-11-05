# M3-10: Script chain: collection, folders, request

Status: todo
Platform: any
Depends on: M3-03, M3-06

## Goal

Given a request, produce the ordered list of script sets that apply to it (collection first, then each folder, then the
request), from the in-memory collection tree.

## Design decisions

- Read first: `src/Yaapic.ViewModels/Collections/CollectionTreeViewModel.cs` (`FindCollection(requestId)` added by M2-71 and
  the way `VariableScopeProvider` finds the collection variables), `CollectionNodeViewModel.cs`.
- Core (`Yaapic.Core.Scripting`): `public sealed record ScriptStep(string Origin, ScriptSet Scripts)` (`Origin` is the display
  text, for example `"Users API"` for a collection or folder name and the request name for the request);
  `public sealed record ScriptChain(IReadOnlyList<ScriptStep> Steps)` with `static ScriptChain Empty` and
  `ScriptChain WithRequest(string name, ScriptSet scripts)` appending the request step; `public interface IScriptChainProvider`
  with `ScriptChain GetAncestors(Guid requestId)` (folders only, outermost first; empty for an unfiled request or an unsaved
  one). The request's **own** scripts are not read from the tree: they come from the editor's current definition (unsaved
  text must run), which is why the chain has `WithRequest`.
- ViewModels: `CollectionTreeScriptChainProvider(CollectionTreeViewModel tree) : IScriptChainProvider` walking the loaded
  tree from the request node up to the top-level folder, returning the folders outermost first with
  `FolderDefinition.Scripts`; a step per folder even when its set is empty (the pipeline skips empty blocks).
  `Origin` for a folder step is `Folder.Name`.
- Test double: `tests/Yaapic.TestDoubles/FakeScriptChainProvider.cs` (settable chain).
- No caching: the tree view model already holds the loaded tree; the provider must reflect renames and edits after
  `RefreshAsync`.

## Out of scope

Running the scripts (M3-11), editing folder scripts (M3-40).

## Acceptance criteria

- [ ] Tests (`Yaapic.ViewModels.Tests`, in-memory store double): unfiled request -> empty ancestors; request in a collection
      -> `[collection]`; request in a nested folder -> `[collection, folder, subfolder]` in that order; scripts and names
      reflect a folder edit after refresh; unknown id -> empty; `WithRequest` appends last.
- [ ] 0 warnings, XML docs, architecture tests green, all tests green.

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

`feat: resolve the collection, folder and request script chain`
