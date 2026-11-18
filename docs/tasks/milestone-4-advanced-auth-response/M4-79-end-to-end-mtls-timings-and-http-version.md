# M4-79: End-to-end tests: mTLS, timings, HTTP version, diff and code generation

Status: done
Platform: any
Depends on: M4-39, M4-48, M4-58, M4-61

## Goal

The remaining M4 features are proven together through the composed services.

## Design decisions

- Read first: M4-78 (same folder and style), M4-34, M4-36, M4-39, M4-48, M4-58.
- New `Milestone4TransportEndToEndTests` in `tests/Yaapic.Infrastructure.Tests/EndToEnd/`:
  - mTLS: certificates generated at test time, a local TLS server requiring a client certificate and echoing its thumbprint, `FileCertificateStore` in a temp folder, `FileSecretVault`
    with a passphrase protector (unlocked in the test), a PFX entry with passphrase stored in the vault, the real `ClientCertificateProvider` and `HttpClientTransport`
    (the self-signed server accepted through the test-only `configureHandler`): matching host sends the certificate; a non-matching host does not; locking the vault gives
    `ClientCertificateError` with the vault message; removing the entry stops sending the certificate (store `Changed`).
  - Timings: through the same composition, a local server with delayed headers and body: all phases present on a new connection (`Wait`, `Download` within generous bounds), `ConnectionReused` on
    the second request, `Sum` <= `Duration`; the values reach `ResponsePanelViewModel.Timings` rows.
  - HTTP version: `Http1` reports `HTTP/1.1`; `Http3` with a fake `IHttpVersionSupport(false)` is sent as HTTP/2 or lower without failing.
  - Diff: two sends of a stub whose body changes: `ResponsePanelViewModel.Diff` has a comparison with the expected added and removed lines; the pinned baseline; a binary response leaves the
    previous snapshot.
  - Code generation: for a request with variables, an environment, Basic auth and a JSON body, `CodeGenerationViewModel` output contains the resolved URL, the `Authorization` header and the body in each
    of the six languages (assert substrings, not full text: the full text is covered by the generator tests); an OAuth 2.0 request contains the placeholder header and no real token.
- Also add an **AOT smoke** helper only if one exists in the Milestone 3 end-to-end tests; otherwise nothing.

## Out of scope

UI tests, authentication scenarios (M4-78).

## Acceptance criteria

- [x] The scenarios above are green and deterministic; every listener, certificate file and server is cleaned up.
- [x] Skip nothing silently: a scenario that needs a capability the host lacks (for example the vault protector) uses the passphrase protector, which exists everywhere.
- [x] Architecture tests green, 0 warnings, all tests green.

## Suggested commit message

`test: add end-to-end tests for mTLS, timings, HTTP version, diff and code generation`
