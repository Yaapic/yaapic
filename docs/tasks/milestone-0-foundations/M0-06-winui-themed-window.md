# M0-06: WinUI 3 empty themed window (Native AOT)

Status: done
Platform: windows
Depends on: M0-04

## Goal

`Yaapic.App.WinUI`: an empty main window using the generated resources, with the three themes.

## Design decisions

`x:Bind` compiled, Mica on the title bar only, no business logic, no reflection. Minimum window size 900x600.

## Out of scope

Real screens, MSIX packaging.

## Acceptance criteria

- [x] `dotnet publish src/Yaapic.App.WinUI -c Release -r win-x64 -p:PublishAot=true` succeeds with 0 IL2xxx/IL3xxx
      warnings (verify Windows App SDK Native AOT support first and record it).
- [x] The published exe starts and exits cleanly in a smoke run.

## Suggested commit message

`feat: add WinUI 3 app shell with themed empty window`

## Notes

Windows App SDK 1.8 (Native AOT supported since 1.6), unpackaged and self-contained, net11.0-windows10.0.19041.0.
Publishing needs `vswhere.exe` on PATH (`C:\Program Files (x86)\Microsoft Visual Studio\Installer`). The publish
produced 0 IL2xxx/IL3xxx warnings; the exe started, showed the window and closed with exit code 0.
