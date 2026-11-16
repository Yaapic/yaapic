# M4-60: ViewModel: palette commands and shell entries for the M4 tools

Status: todo
Platform: any
Depends on: M4-41, M4-44, M4-48, M4-49, M4-58

## Goal

The command palette and the shell expose the new tools: generate code, decode a JWT, pin a response for diff, clear OAuth 2.0 tokens, manage client
certificates.

## Design decisions

- Read first: `src/Yaapic.ViewModels/Palette/CommandsPaletteSource.cs` (how entries are defined, shortcuts, localized names), `Shell/` view models
  (how the console panel, the vault window or the updates window are opened from the shell: an event or a service the view handles), `ShortcutText`.
- New palette entries (names localized EN and FR, `Strings.Palette*`): "Generate code for this request" (needs an open request; raises the editor's
  `CodeGenerationRequested`), "Decode JWT" (opens the decoder dialog), "Compare response with previous" (switches the response panel to the Diff tab: a `ShowDiffRequested`
  event on the panel), "Pin response as baseline", "Clear OAuth 2.0 tokens" (calls `IOAuth2TokenService`'s cache `Clear` through a new `ClearAllTokens()` member:
  add it to the service and the fake), "Client certificates..." (opens the certificates dialog of M4-64 and M4-73).
- Shell view model: events `JwtDecoderRequested`, `CertificatesRequested` and the handler wiring identical to the existing window-opening events; no UI code.
- Shortcuts: none assigned (avoid collisions); entries are available only through the palette and menus.
- Entries that need an open request or a response are disabled (hidden by the existing palette rule) when there is none.

## Out of scope

The windows themselves, composition.

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests`: each entry appears with its localized name; availability rules (request open or not, response present or not); invoking each
  raises its event or calls its service once; `ClearAllTokens` clears the cache (Core test).
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add palette commands for the M4 tools`
