# M2-63: Palette sources for requests, environments and tabs, and shell wiring

Status: todo
Platform: any
Depends on: M2-62, M2-44

## Goal

The palette finds saved requests, switches environments and tabs, and is reachable from the shell.

## Design decisions

- In `Yaapic.ViewModels.Palette`, each `IPaletteSource`:
  - `RequestsPaletteSource(CollectionTreeViewModel collections)`, category "Request": one item per request in the loaded
    tree (`CollectionNode.Flatten`), `Id` `request:<guid>`, `Title` the request name, `Subtitle` `"{METHOD}  {url}"` plus
    `"  ·  Collection / Folder"` when it is inside folders. `Execute` calls a new `void OpenRequest(Guid id)` on
    `CollectionTreeViewModel` (add it here: opens the request with that id in a tab through `tabs.Open`, no-op when the id
    is unknown; `Execute` wraps it in a completed task).
  - `EnvironmentsPaletteSource(EnvironmentsViewModel environments)`, category "Environment": "No environment" and one
    "Use environment: {name}" per environment, `Execute` sets `ActiveEnvironment` (null for none); the active one has the
    subtitle "Active".
  - `TabsPaletteSource(TabsViewModel tabs)`, category "Tab": "Go to tab: {DisplayName}" per open tab with the method text and
    URL as subtitle, `Execute` activates it.
- The palette sources need the shell (`CommandsPaletteSource` takes it), so the shell cannot receive the palette in its
  constructor. `ShellViewModel` gains `public void AttachPalette(CommandPaletteViewModel palette)` (XML doc: called once
  by the composition root; a second call throws `InvalidOperationException`), the property `Palette` (returns the
  attached instance, throws `InvalidOperationException` when read before `AttachPalette`) and `[RelayCommand]
  OpenPalette()` (calls `Palette.OpenCommand`).
- `AppComposition.CreateShell` builds the shell, then `new CommandPaletteViewModel([new CommandsPaletteSource(shell), new
  RequestsPaletteSource(collections), new EnvironmentsPaletteSource(environments), new TabsPaletteSource(tabs)])`, then
  `shell.AttachPalette(palette)`.
- Existing shell tests need no palette; new palette tests attach one.

## Out of scope

Views (M2-64, M2-65), search in request bodies, history as a source.

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests`: requests source lists nested requests with path subtitles and opens one into a
      tab (`OpenRequest`); unknown id is a no-op; environments source switches and clears; tabs source activates; the shell test opens the
      palette with a query and executes the first result; `AppCompositionTests` assert `Shell.Palette` is set and its
      first open lists the commands.
- [ ] 0 warnings; architecture tests green.

## Suggested commit message

`feat: add palette request, environment and tab sources and wire the palette into the shell`
