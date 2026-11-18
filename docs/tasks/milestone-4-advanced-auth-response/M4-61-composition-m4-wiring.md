# M4-61: Composition: wire the M4 services

Status: done
Platform: any
Depends on: M4-27, M4-24, M4-25, M4-33, M4-36, M4-39, M4-41, M4-43, M4-58, M4-59, M4-60

## Goal

The application builds and shares the real M4 services, so the front-ends only need views.

## Design decisions

- Read first: `src/Yaapic.Composition/AppComposition.cs` (`CreateShell`, how `RequestSender`, `ScriptedRequestSender`, the vault and the update service are
  built and handed to the shell and to `RequestEditorViewModel` factories), `AppPreferences.cs`, the App projects' `App.xaml.cs` / `Program.cs` call sites.
- Build once and share (singletons for the shell's lifetime): `InMemoryOAuth2TokenCache`, `TcpLoopbackRedirectReceiver`, `SystemBrowserLauncher`,
  `OAuth2TokenService`, `FileCertificateStore`, `ClientCertificateProvider`, `QuicHttpVersionSupport`, `RequestAuthenticator.CreateDefault(...)`,
  `CodeGeneratorRegistry` with the six generators (cURL, JavaScript fetch, Python requests, C# HttpClient, PowerShell, Go; in that order), `TimeProvider.System`.
- `HttpClientTransport` is constructed with the certificate provider and the version support (M4-34, M4-39); `RequestSender` with the authenticator; the scripted
  sender still wraps that `RequestSender` (`pm.sendRequest` unaffected).
- The shell and every `RequestEditorViewModel` factory call get `IOAuth2TokenService`, `IHttpVersionSupport`, `CodeGenerationServices` (M4-43, M4-58, M4-59); the
  `CertificatesViewModel` (M4-44) and `JwtDecoderViewModel` (M4-49) are created by the shell on demand and exposed through the events of M4-60.
- Disposal: the transport and the certificate provider are disposed when the composition's owner disposes (match what the vault does).
- Nothing in this card references a view or an App project (architecture tests).
- The token cache is cleared when the user locks the vault? **No**: tokens are independent of the vault (ADR 0013).
- `docs/architecture.md`: add the new services to the composition description in two or three lines.

## Out of scope

Views and dialogs on either front-end, end-to-end tests.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests` or a composition test project if one exists: `CreateShell` with fakes for the file-system parts still returns a shell; an
  OAuth 2.0 request through the composed `RequestSender` with a fake transport reaches the token service; the registry has the six generators in order;
  the palette entries of M4-60 are present. Existing composition tests unchanged and green.
- [x] AOT publish of the host's app project: 0 IL2xxx/IL3xxx warnings, the published exe starts and exits (kill it afterwards).
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(composition): wire authentication, certificates, timings and code generation`
