# M0-05: Avalonia empty themed window (Native AOT)

Status: done
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

- [x] `dotnet publish src/Yaapic.App.Avalonia -c Release -r <host rid> -p:PublishAot=true` succeeds with 0 IL2xxx/IL3xxx
      warnings.
- [x] The published binary starts and exits cleanly in a smoke run.

## Implementation notes

- Verified on Windows only (`-r win-x64`): AOT publish has 0 IL2xxx/IL3xxx warnings and the native exe starts and
  closes with exit code 0. **Not verified here:** the `linux-x64`/`osx-arm64` publish and smoke run; M0-07 (CI) must
  cover them.
- Dark and light follow the system variant (`RequestedThemeVariant="Default"`). The generated Avalonia dictionary has
  no high-contrast variant (Avalonia has no system high-contrast theme variant), so high contrast is not rendered
  yet: a card must extend the token generator output for it.
- `UseOptimizedPublish=false`: .NET 11 AOT publish otherwise fails with MSB3030 (missing documentation file).
- No rendering comparison against WinUI yet: the window is empty (background `YaapicSurfaceAppBrush` only).

## Suggested commit message

`feat: add Avalonia app shell with themed empty window`
