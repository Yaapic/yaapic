# M4-25: Infrastructure: system browser launcher

Status: todo
Platform: any
Depends on: M4-23

## Goal

The real `IExternalBrowser`: opens an `http` or `https` URL in the user's default browser on Windows, macOS and Linux.

## Design decisions

- Read first: ADR 0013 (Authorization-code redirect), M4-23, how the update code or the About UI already opens links (grep `Process.Start`,
  `UseShellExecute`, `Launcher`); reuse that helper when one exists instead of duplicating it, and say so in the Notes.
- `src/Yaapic.Infrastructure/Auth/SystemBrowserLauncher.cs`, `public sealed class`, implements the port. The process start is behind a small
  internal delegate `Action<ProcessStartInfo>` (constructor parameter, default `p => Process.Start(p)?.Dispose()`) so tests never launch a
  browser.
- Only `Uri.Scheme` `http` and `https` are accepted; anything else throws `ArgumentException` (no `file:`, no custom schemes, no `javascript:`).
- Windows: `UseShellExecute = true`, `FileName` = the absolute URL string. macOS: `FileName = "open"`, `ArgumentList = [url]`, no shell.
  Linux: `FileName = "xdg-open"`, `ArgumentList = [url]`, no shell. Arguments go through `ArgumentList` (never string concatenation), so a URL
  containing spaces or quotes cannot inject arguments.
- A `Win32Exception` or `InvalidOperationException` from the start becomes `InvalidOperationException(Strings.OAuth2BrowserFailed)` ("Could not open
  the browser. Open this URL yourself: ..." is **not** done here: the URL contains the state, so the message stays generic).
- String `OAuth2BrowserFailed`, EN and FR.

## Out of scope

An embedded web view, copying the URL to the clipboard.

## Acceptance criteria

- [ ] Tests in `Yaapic.Infrastructure.Tests` with the fake start delegate: the `ProcessStartInfo` per OS (the OS is a constructor parameter
  `OSPlatform` or an enum so all three are tested on any host); `file:///c:/x` and `ftp://` rejected; a URL with spaces and quotes stays one
  argument; start failure mapped.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green; no browser opened by the tests.

## Suggested commit message

`feat(infra): add the system browser launcher`
