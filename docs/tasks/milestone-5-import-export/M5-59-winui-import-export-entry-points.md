# M5-59: WinUI: import and export entry points

Status: todo
Platform: windows
Depends on: M5-53, M5-57, M5-58

## Goal

The user can open Import and Export from the sidebar, a collection's context menu and the palette.

## Design decisions

- Read first: M5-53 (shell events, `ExportCollectionCommand`, palette entries), `MainWindow.xaml` (the `+` flyout with `PaletteNewRequest` and `CollectionsNewCollection`, around the `MenuFlyoutItem`s of the sidebar header), `MainWindow.xaml.cs` (how `EnvironmentsRequested`, `JwtDecoderRequested`, `CertificatesRequested` open their dialogs),
  the collection tree view that holds the folder context menu (grep `NewFolder` in `Views` and `MainWindow.xaml`).
- **Sidebar**: add `MenuFlyoutItem` `Strings.CollectionsImport` ("Import...") under "New collection" in the `+` flyout, command `ViewModel.RequestImport` (use a `Click` handler if the flyout items use handlers, or a `RelayCommand` exposed by the shell: follow the neighbouring items).
- **Context menu**: on **top-level folder** rows add `Strings.CollectionsExport` ("Export...") bound to `ViewModel.ExportCollectionCommand` with the node as parameter, in the same flyout as the existing folder actions (rename, delete, settings); the item is hidden or disabled for nested folders (the command's `CanExecute`).
- **Handlers** in `MainWindow.xaml.cs`: `ViewModel.ImportRequested += async ...` creates `ViewModel.CreateImport()` and shows `ImportDialog`; `ViewModel.ExportRequested += async (s, id) => ...` creates `ViewModel.CreateExport()`, `await vm.LoadAsync(id)`, shows `ExportDialog`; both set `XamlRoot` as the other dialogs do and are guarded against opening two dialogs at once (the existing pattern).
  Unsubscribe in the same place the other handlers are.
- The palette entries need no view work (M5-53).

## Out of scope

Keyboard shortcuts (none are defined for import and export).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts.
- [ ] Manual smoke on the published exe: the `+` flyout shows Import; the folder context menu shows Export on a top-level folder only; the palette finds "Import" and "Export"; each opens the right dialog; after an import the sidebar shows the new collection; an exported file re-imports. Record anything not run.
- [ ] Strings in EN and FR exist (M5-53); 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the import and export entry points`
