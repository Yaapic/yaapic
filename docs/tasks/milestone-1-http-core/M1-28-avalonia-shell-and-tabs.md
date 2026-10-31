# M1-28: Avalonia shell: sidebar, tabs and dialogs

Status: todo
Platform: unix
Depends on: M0-05, M1-23

## Goal

Same shell as M1-24, in `Yaapic.App.Avalonia`, bound to the same `ShellViewModel`.

## Design decisions

- Behaviour, texts, shortcuts and layout are those of M1-24; only the controls differ. Read M1-24 first.
- `Yaapic.App.Avalonia` references `Yaapic.Composition`. In `DependencyRulesTests` add
  `["Yaapic.App.Avalonia"] = ["Yaapic.Composition", "Yaapic.UI.Abstractions", "Yaapic.ViewModels"]`.
- Startup in `App.OnFrameworkInitializationCompleted`: `AppComposition.CreateShell(Workspace.DefaultRoot(), dialogs)`,
  assign to `MainWindow.DataContext`, await `InitializeAsync()`.
- `AvaloniaDialogService : IDialogService` in `Services/`: a small modal `Window` (`ShowDialog<UnsavedChoice>` /
  `ShowDialog<bool>`) with the same texts and buttons as M1-24. No third-party dialog package.
- Controls: environment `ComboBox` and theme `DropDownButton`-equivalent (`Button` with `MenuFlyout`) in the top bar;
  sidebar `TextBox` + `ListBox`; tabs = `TabControl` with an `ItemTemplate` header showing `Title` and a close button
  (`Tabs.CloseAsync`), plus a "+" button next to it calling `Tabs.NewTab()`; tab content placeholder `TextBlock` with
  the URL (replaced by M1-29). Shortcuts through `Window.KeyBindings` (Ctrl+T, Ctrl+W, Ctrl+S; on macOS use the platform
  modifier via `KeyModifiers.Meta` when `OperatingSystem.IsMacOS()`).
- Window closing: handle `Closing`, cancel, await `RequestCloseAsync()`, close again on true.
- `x:CompileBindings="True"` and `x:DataType` everywhere; value mapping (method/status to brush) as static methods
  exposed through a `FuncValueConverter` defined in `Presentation/BrushMap.cs`; brushes only from the generated
  `Yaapic*` resources.

## Out of scope

Same as M1-24.

## Acceptance criteria

- [ ] `dotnet publish src/Yaapic.App.Avalonia -c Release -r <host rid> -p:PublishAot=true` with 0 IL2xxx/IL3xxx warnings.
- [ ] Smoke run on the published binary: same checks as M1-24.
- [ ] Architecture tests green with the new rule.

## Suggested commit message

`feat(avalonia): add shell with request sidebar, tabs and dialogs`
