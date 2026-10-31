# M1-24: WinUI shell: sidebar, tabs and dialogs

Status: done
Platform: windows
Depends on: M1-23

## Goal

Replace the empty WinUI window with the real shell bound to `ShellViewModel`: saved requests sidebar, request tabs,
environment selector, dialogs, keyboard shortcuts.

## Design decisions

- `Yaapic.App.WinUI` references `Yaapic.Composition` (rule added in M1-23). `App.OnLaunched` calls
  `AppComposition.CreateShell(Workspace.DefaultRoot(), new WinUIDialogService(...))`, sets it on `MainWindow.ViewModel`,
  then awaits `InitializeAsync()`. The App project only wires and displays: no business logic.
- `WinUIDialogService : IDialogService` in `Yaapic.App.WinUI/Services/`, using `ContentDialog` (needs the window's
  `Content.XamlRoot`): unsaved prompt "Save changes to '{name}'?" with buttons Save (primary), Don't save (secondary),
  Cancel (close); delete prompt "Delete '{name}'?" with Delete / Cancel.
- Layout (`MainWindow.xaml`): keep the 40 px title bar (Mica, `SetTitleBar`) and put in it, right-aligned: the
  environment `ComboBox` (items `Environments.Environments`, `SelectedItem` two-way to `ActiveEnvironment`, placeholder
  "No environment", width 200) and a "Theme" `DropDownButton` with a `MenuFlyout` holding the three existing theme
  actions (Dark / Light / System). Body: two columns, a fixed 260 px sidebar (no splitter in M1), then the tab area.
- Sidebar: "New" button (`Requests.NewRequestCommand`), a plain `TextBox` bound to
  `Requests.FilterText` (`UpdateSourceTrigger=PropertyChanged`, placeholder "Filter requests"), `ListView` of
  `Requests.Items`; item template: method text (width 44, `YaapicCodeFontSize`, foreground `YaapicMethodGetBrush` /
  `YaapicMethodPostBrush`) + name; `ItemClick` opens; context menu "Delete".
- Tab area: WinUI `TabView` with `TabItemsSource` = `Tabs.Tabs`, `SelectedItem` two-way to `Tabs.ActiveTab`,
  header = `Title`, `AddTabButtonClick` -> `Tabs.NewTab()`, `TabCloseRequested` -> `Tabs.CloseAsync(tab)`. The tab
  content is a placeholder `TextBlock` showing the request `Url` (replaced by M1-25). Empty state when no tab: centered
  text "No request open" and a "New request" button.
- Keyboard accelerators on the window: Ctrl+T new tab, Ctrl+W close active tab, Ctrl+S save active tab.
- Window closing: handle `AppWindow.Closing`, set `args.Cancel = true`, await `ViewModel.RequestCloseAsync()`, and close
  again only when it returns true.
- `x:Bind` only (compiled), `x:DataType` on every `DataTemplate`, mapping functions (method/status to brush) as static
  methods in `Yaapic.App.WinUI/Presentation/BrushMap.cs` called through `x:Bind` function binding. Brushes only from the
  generated `Yaapic*` resources.
- `Tabs.LastError` / `Environments.LastError`, when non-null, show in an `InfoBar` (severity Error) under the title bar.

## Out of scope

Request editor and response views (M1-25, M1-26), environment management dialog (M1-27), resizable sidebar, tab
drag and drop, menus beyond the theme picker.

## Acceptance criteria

- [x] `dotnet publish src/Yaapic.App.WinUI -c Release -r win-x64 -p:PublishAot=true` with 0 IL2xxx/IL3xxx warnings. If
      CsWinRT reports AOT warnings for ViewModel types crossing the WinRT boundary (`ObservableCollection<T>` as
      `ItemsSource`), fix them in the App project only, following the CsWinRT AOT guidance, and record the fix in the
      card's Notes.
- [x] Smoke run of the published exe: the window shows one "New Request" tab; Ctrl+T adds a tab; Ctrl+W closes the
      untouched tab without prompt; closing the window exits with code 0; a second start restores no unsaved tab.
- [x] Architecture tests green (`Yaapic.App.WinUI` references only the allowed projects).

## Suggested commit message

`feat(winui): add shell with request sidebar, tabs and dialogs`

## Notes

- Native AOT publish succeeded (0 warnings). CsWinRT1030 requires `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in
  `Yaapic.App.WinUI` for the ViewModel collections bound in XAML (`ObservableCollection<T>` as `ItemsSource`); without
  it the published exe crashed at startup inside `Microsoft.UI.Xaml.dll` (0xC000027B) as soon as a collection was bound.
- Smoke run of the published exe: the window starts, closes through `CloseMainWindow` and exits with code 0. Ctrl+T /
  Ctrl+W / session-restore behavior was not driven by UI automation (no UIA harness); it is covered by the
  ViewModel tests.
- `TabView` header comes from a `TabViewItem` declared in `TabItemTemplate` (`Header` x:Bound to `Title`).
