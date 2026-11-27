# M6-79: Avalonia: automation entries on the rail

Status: todo
Platform: unix
Depends on: M6-76, M6-77

## Goal

The Avalonia counterpart of M6-77: the rail gets the Runner, Load test, Mock servers and Monitors entries (ADR 0023). Until M6-80 a click
still opens the tool window.

## Design decisions

- Read first: M6-77 (entries, order, names, AutomationIds, selection rule to match), M6-76, `src/Yaapic.App.Avalonia/MainWindow.axaml`
  (rail `Border` in column 0: `DockPanel` with Settings at the bottom and a `StackPanel` with Collections, History and the palette
  button; the `Button.icon.rail` and `Button.icon.selected` styles in `Window.Styles`), `Presentation/SidebarMap.cs`,
  `design/icons/Yaapic.Icons.README.txt` (`PathIcon` only fills: stroked icons need a `Path`).
- Collections and History entries: `Classes.selected="{Binding IsCollectionsSelected}"` (resp. `IsHistorySelected`) and the indicator
  `Border` `IsVisible` bound to the same bool, instead of the `Mode` converters. The sidebar content keeps its `SidebarMap` converters.
- Icons: the Yi icons are strokes, so every rail icon (the two existing ones included, for one look) is
  `<Viewbox Width="20" Height="20"><Canvas Width="24" Height="24"><Path Data="{StaticResource Yi...}" StrokeThickness="1.8" StrokeJoin="Round" StrokeLineCap="Round" Stroke="{Binding $parent[Button].Foreground}"/></Canvas></Viewbox>`
  instead of `PathIcon` (the `.selected` style already sets the button foreground to the accent). Remove the
  `Button.icon.rail PathIcon` style if nothing uses it any more; leave the Settings and palette buttons as they are when their icon is a
  filled glyph.
- After the History entry and before the palette button: a `StackPanel Spacing="4" IsVisible="{Binding CanAutomate}"` with a separator
  `<Border Width="24" Height="1" Margin="0,4" HorizontalAlignment="Center" Background="{DynamicResource YaapicBorderSubtleBrush}"/>` and four
  entries built like History (`Grid` with the `Button Classes="icon rail"` and the 3 px indicator `Border`):

  | Entry | Icon | `ToolTip.Tip` and `AutomationProperties.Name` | `AutomationProperties.AutomationId` | Command | Selected when |
  |---|---|---|---|---|---|
  | Runner | `YiRunner` | `{x:Static loc:Strings.RunnerTitle}` | `RailRunner` | `ShowRunnerCommand` | `IsRunnerSection` |
  | Load test | `YiTimeline` | `{x:Static loc:Strings.LoadTitle}` | `RailLoadTest` | `ShowLoadTestCommand` | `IsLoadTestSection` |
  | Mock servers | `YiMock` | `{x:Static loc:Strings.MockTitle}` | `RailMockServers` | `ShowMockServersCommand` | `IsMockServersSection` |
  | Monitors | `YiMonitor` | `{x:Static loc:Strings.MonitorTitle}` | `RailMonitors` | `ShowMonitorsCommand` | `IsMonitorsSection` |

  Compiled bindings (`x:DataType="vm:ShellViewModel"` on the window): no reflection, no new converter.

## Out of scope

The views in the main area and removing `ToolWindowHost` (M6-80).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly.
- [ ] Runtime smoke on macOS or Linux as in M6-77 (entries, tooltips, single highlight, Collections/History move it back, stroked icons
  render as lines, dark and light); if not possible, run it on Windows through UI Automation as M6-56 did and say so in Notes.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the automation entries to the rail`
