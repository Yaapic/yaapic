# M6-77: WinUI: automation entries on the rail

Status: todo
Platform: windows
Depends on: M6-76

## Goal

The WinUI rail (the left menu) gets the Runner, Load test, Mock servers and Monitors entries of ADR 0023, with the same selection
look as Collections and History. Until M6-78 a click still opens the tool window (the shell still raises its events).

## Design decisions

- Read first: ADR 0023 ("Rail"), M6-76 (`IsRunnerSection`..., `IsCollectionsSelected`, `IsHistorySelected`, `Show*Command`),
  `src/Yaapic.App.WinUI/MainWindow.xaml` (the rail: the `StackPanel` with the Collections and History `Grid`s, about lines 130 to 166),
  `Presentation/BrushMap.cs` (`CollectionsRail`, `HistoryRail`, private `Rail(bool)`), `Presentation/VisibilityMap.cs` (`WhenTrue`).
- `BrushMap`: add `public static Brush RailSelected(bool selected) => Rail(selected);` (documented); delete `CollectionsRail` and
  `HistoryRail` once unused.
- Collections and History entries: their indicator `Rectangle` and `Border` use `VisibilityMap.WhenTrue(ViewModel.IsCollectionsSelected)`
  (resp. `IsHistorySelected`) and the icon stroke `BrushMap.RailSelected(...)` of the same bool, instead of `ViewModel.Mode`. The sidebar
  content keeps its `WhenCollections`/`WhenHistory(ViewModel.Mode)` bindings.
- After the History `Grid`, inside the same `StackPanel`: a `StackPanel` (`Spacing="4"`, `Visibility="{x:Bind p:VisibilityMap.WhenTrue(ViewModel.CanAutomate), Mode=OneWay}"`)
  holding a separator `<Rectangle Width="24" Height="1" Margin="0,4" Fill="{ThemeResource YaapicBorderSubtleBrush}"/>` and four `Grid`s copied
  from the History one (indicator `Rectangle`, selection `Border`, `YaapicRailButtonStyle` button with a 20 px `Viewbox`/24 px `Canvas`/`Path`
  StrokeThickness 1.8, round joins and caps):

  | Entry | Path data | Name and tooltip | AutomationId | Command | Selected when |
  |---|---|---|---|---|---|
  | Runner | `YiRunner` | `loc:Strings.RunnerTitle` | `RailRunner` | `ViewModel.ShowRunnerCommand` | `ViewModel.IsRunnerSection` |
  | Load test | `YiTimeline` | `loc:Strings.LoadTitle` | `RailLoadTest` | `ViewModel.ShowLoadTestCommand` | `ViewModel.IsLoadTestSection` |
  | Mock servers | `YiMock` | `loc:Strings.MockTitle` | `RailMockServers` | `ViewModel.ShowMockServersCommand` | `ViewModel.IsMockServersSection` |
  | Monitors | `YiMonitor` | `loc:Strings.MonitorTitle` | `RailMonitors` | `ViewModel.ShowMonitorsCommand` | `ViewModel.IsMonitorsSection` |

  All bindings `Mode=OneWay` (the `Bindings?.Update()` of `ApplyCaptionButtonColors` refreshes the brushes on a theme change, as today).
- No new string (the titles exist since M6-41/M6-44..48).

## Out of scope

Showing the views in the main area and removing the tool windows (M6-78); Avalonia (M6-79).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts.
- [ ] Manual smoke on the published exe (UI Automation or by hand), dark and light: the four entries appear under a separator after
  History with their tooltips; clicking one highlights it (and only it) and opens/activates its tool window (transitional); clicking
  Collections or History moves the highlight back; the folder context menu and the status bar items also move the highlight. Record
  what was not run in Notes.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the automation entries to the rail`
