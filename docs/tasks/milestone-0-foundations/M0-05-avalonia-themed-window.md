# M0-05: Avalonia empty themed window (Native AOT)

Status: todo
Platform: unix
Depends on: M0-04

## Goal

`Yaapic.App.Avalonia`: an empty main window using the generated resources, with dark, light and high-contrast themes.

## Design decisions

`x:CompileBindings` on, no business logic, no reflection. Minimum window size 900x600. Record any rendering
difference against WinUI in this card.

## Out of scope

Real screens, navigation.

## Acceptance criteria

- [ ] `dotnet publish src/Yaapic.App.Avalonia -c Release -r <host rid> -p:PublishAot=true` succeeds with 0 IL2xxx/IL3xxx
      warnings.
- [ ] The published binary starts and exits cleanly in a smoke run.

## Suggested commit message

`feat: add Avalonia app shell with themed empty window`
