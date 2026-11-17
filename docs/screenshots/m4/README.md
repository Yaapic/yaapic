# M4 WinUI screenshots and design comparison

Captures of the Milestone 4 WinUI views (OAuth 2.0, JWT and AWS Signature v4 authentication, response Timing and Diff tabs, code
generation, client certificates, JWT decoder, HTTP version setting), dark and light, taken from the Native AOT build at
1440 x 900 and compared with the design sources in `design/mockups/`. There is **no mockup for any milestone 4 screen**, so each
screen is compared with the closest component sheet (named per screen below). Every value on screen is fake: the JWT private key,
the two self-signed certificates and the HS256 token are generated at capture time and deleted afterwards, the secrets are
`fake-client-secret` and `fake-aws-secret-0000`.

## Regenerate

```powershell
dotnet publish src/Yaapic.App.WinUI -c Release -r win-x64 -p:PublishAot=true
pwsh tools/screenshots/Capture-M4.ps1                      # all 20 files; -Only <screen> for one screen
```

`Capture-M4.ps1` runs `Capture-WinUI.ps1` once per file, in English. Additions to the shared tooling in this card: the fixture
requests `oauth2`, `oauth2-code`, `jwt`, `aws` and `changing` (and three fake variables in the `local` environment), the stub
endpoints `/token` and `/changing` (a body and headers that change on every call), the `-Certificates` switch (seeds
`certificates.json` with two throw-away certificates kept in `C:\yaapic-shot-certs`, deleted at the end) and a throw-away RSA key
for the `jwt` request. The request pane of a 900 px window is only ~300 px high, so the auth and settings shots use the
side-by-side layout (`invoke:Layout`) and the timing shot shrinks the request pane with the splitter keys.

## Defects found by the capture

Fixed in this card (small):

- **OAuth 2.0 send crashed in AOT** with `0x8001010E` (wrong thread): `TokensChanged` is raised from a worker thread and the editor
  updated its bound properties there. It now posts to the creating `SynchronizationContext` (unit test added).
- **Client certificates dialog killed the AOT process** (stowed exception) as soon as a certificate was listed: `Issues` was an
  `IReadOnlyList<string>` bound to `ItemsSource`; it goes through `ListMap.Lines` like the other lists.
- **The Auth tab and the Settings tab did not scroll**, so in a short request pane the OAuth 2.0 token status, the JWT fields and
  the HTTP version choice were unreachable. Both panes are now in a `ScrollViewer`; so is the Timing waterfall.
- **Password boxes had no border** (client secret, JWT secret, AWS secret and session token, passphrase), unlike the text boxes
  next to them. They now use the same `YaapicBorderDefaultBrush` border. `VaultDialog` still has the borderless boxes (its M3
  screenshots are untouched).

## Deviations left open

| # | Screen | Deviation | Class |
|---|---|---|---|
| D1 | response-diff | In the side-by-side layout (the narrowest case, ~470 px) the response tab strip is overlapped by the status chip (`Timing`/`Diff` labels), the diff toolbar is clipped (`Pin as baseli`, `Ignore vo`) and the header diff rows are not aligned with the line numbers. Not seen in the stacked layout. | follow-up: responsive response header and diff toolbar |
| D2 | auth-oauth2 | Same tab-strip overlap as D1 while the response is shown. | see D1 |
| D3 | jwt-decoder | The dialog content is taller than a 900 px window: the time claims and the signature note need scrolling; scrolled, the third claim column (relative time) is clipped at the right edge. | follow-up: compact decoder layout |
| D4 | settings-http-version | The *Follow redirects* switch shows `Activé` on a French Windows although the app runs in English (the `OnContent` of `ToggleSwitch` follows the OS language). | follow-up: set `OnContent`/`OffContent` from `Strings` |
| D5 | response-timing | A 900 px window leaves ~240 px for the waterfall: the first line is scrolled out of view in the capture so that the five rows are visible. | none (window size) |
| D6 | code-generation, jwt-decoder, certificates | The `Close` button of the first two is a full-width accent button, in the third (`Save`/`Close`) it is outlined: the dialogs do not share one footer. | follow-up: one dialog footer style |
| D7 | all | Window frame, title bar and rail colours: same question as M1 F1. | see M1 F1 |

LoomEditor 0.6.0 has no shell, PowerShell or Go grammar, so cURL, PowerShell and Go code is plain text (by design).

## Screens

Each entry: dark and light file, the closest design source, and what differs.

### auth-oauth2

[dark](auth-oauth2-dark.png) · [light](auth-oauth2-light.png) — `MainWindow.dc.html` (request tabs), `DSComposants2.dc.html` (fields).
Client credentials after a send: the token is cached, the footer reads `Valid until ...` with *Get new token* (accent) and *Clear
token*. Fields use the 1 px default border and the field height of the component sheet; the client secret is masked. Shot with the
side-by-side layout and the pane scrolled to its end (D2).

### auth-oauth2-code

[dark](auth-oauth2-code-dark.png) · [light](auth-oauth2-code-light.png) — `DSComposants2.dc.html`.
Authorization code (PKCE): the *Authorization URL* and *Redirect URI* fields appear after *Access token URL*; the hints and the
buttons are below the fold of the pane (scroll). No mockup for the grant selector (a plain combo box).

### auth-jwt

[dark](auth-jwt-dark.png) · [light](auth-jwt-light.png) — `DSSyntaxeIcones.dc.html` (monospace fields).
RS256: the private key and payload boxes use the code font, as in the sheet; the key is a throw-away PEM.

### auth-aws

[dark](auth-aws-dark.png) · [light](auth-aws-light.png) — `DSComposants2.dc.html`.
Access key, masked secret and session token, region and service, all with the same chrome after the border fix.

### response-timing

[dark](response-timing-dark.png) · [light](response-timing-light.png) — `MainWindow.dc.html` (response tabs).
`HTTP/1.1 · New connection` summary, one row per phase with the bar in the accent colour for the wait and grey for the others,
`n/a` for the TLS handshake on plain HTTP. D5.

### response-diff

[dark](response-diff-dark.png) · [light](response-diff-light.png) — `DSComposants2.dc.html` (list rows).
Summary line, `Previous response` combo box, *Pin as baseline*, the toggles, the headers section (`ETag`, `X-Request-Count`) and
removed/added lines in the danger/success tints with both line numbers. D1.

### code-generation

[dark](code-generation-dark.png) · [light](code-generation-light.png) — `DSComposants2.dc.html` (dialog).
Python (requests) selected: the highlighted code in the code font with line numbers, the *Copy* button and the secret warning.
D6.

### certificates

[dark](certificates-dark.png) · [light](certificates-light.png) — `EnvVars.dc.html` (master-detail).
List on the sidebar surface with the host pattern as second line, the editor on the base surface, the *Vault is locked* line
with *Unlock*. D6.

### jwt-decoder

[dark](jwt-decoder-dark.png) · [light](jwt-decoder-light.png) — `DSComposants2.dc.html` (dialog).
An expired HS256 token: the red `Expired ... days ago` status, header and payload as read-only JSON. D3.

### settings-http-version

[dark](settings-http-version-dark.png) · [light](settings-http-version-light.png) — `MainWindow.dc.html` (settings pane).
*HTTP version* combo box under the timeout, set to HTTP/2. D4.
