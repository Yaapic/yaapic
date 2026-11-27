# M6-68: Composition: headless runner services

Status: todo
Platform: any
Depends on: M6-66, M6-67

## Goal

`HeadlessComposition` builds everything a headless run needs (stores, transport, authentication, script engines, runner) with no vault, no
view model and no write to the workspace, as ADR 0022 says.

## Design decisions

- Read first: ADR 0022, `src/Yaapic.Composition/AppComposition.cs` (the internal `CreateShell`: transport, certificates, OAuth 2.0 token
  service, `RequestSender`, engines, `ScriptedRequestSender`, `AttachAutomation`), M6-66, M6-67, `src/Yaapic.Core/Auth/OAuth2/IExternalBrowser.cs`,
  `src/Yaapic.Core/Secrets/ISecretVault.cs`.
- `src/Yaapic.Composition/HeadlessComposition.cs`:
  ```csharp
  public static class HeadlessComposition
  {
      public static HeadlessRunServices Create(string workspaceRoot, IReadOnlyList<KeyValueEntry> overrides);
      // test seam, like AppComposition.CreateShell
      internal static HeadlessRunServices Create(string workspaceRoot, IReadOnlyList<KeyValueEntry> overrides,
          IEnumerable<IScriptEngine> engines, IHttpTransport? transport = null, TimeProvider? time = null);
  }
  ```
  The public overload passes `[new QuickJsEngine(), new LuaEngine()]`. Throws `ArgumentException` for a blank root; does **not** call
  `Workspace.EnsureCreated` and checks nothing on disk (the CLI checks the directory, M6-70).
- `src/Yaapic.Composition/HeadlessRunServices.cs`, `public sealed class HeadlessRunServices : IDisposable`:
  - `public ICollectionStore Collections { get; }` = `new FileCollectionStore(root)` (plain store, not secret-aware);
  - `public IEnvironmentStore Environments { get; }` = `new FileEnvironmentStore(root)`;
  - `public IGlobalsStore Globals { get; }` = `new FileGlobalsStore(root)`;
  - `public IImportInputReader Files { get; }` = `new FileImportInputReader()` (data files, ADR 0018);
  - `public CollectionRunner CreateRunner(IReadOnlyList<CollectionNode> roots, EnvironmentDefinition? environment, IReadOnlyList<KeyValueEntry> globals)`:
    `var env = VariableOverrides.Apply(environment, overrides);`
    `new CollectionRunner(new RunStepExecutor(scriptedSender, new TreeScriptChainProvider(roots), new StaticRunScopeSource(roots, env, globals)), time)`;
  - `Dispose()` disposes the real transport and the certificate provider (what `shell.Own` disposes in the app).
- Wiring inside `Create` (same objects as `AppComposition`, minus UI, history and vault):
  - `var vault = new NoSecretVault();` a private sealed class in Composition implementing `ISecretVault`: `State` `NotCreated`,
    `ProtectorId` null, `StateChanged` with empty `add`/`remove`, `InitializeAsync` completed, `TryGet` false with `""`, `GetSecretValues`
    empty, `Lock` no-op; `CreateAsync`, `UnlockAsync`, `SetAsync`, `RemoveAsync`, `RemoveOwnerAsync` throw
    `VaultException("The vault is not available in yaapic-cli.")` (`UnlockAsync` may return `false` instead if `VaultException` does not
    fit its contract).
  - Transport: `new HttpClientTransport(DefaultHttpClient.Create(), DefaultHttpClient.CreateNonRedirecting(), TimeSpan.FromSeconds(30),
    new ClientCertificateProvider(new FileCertificateStore(), vault), support: new QuicHttpVersionSupport())`, or the injected transport.
  - OAuth 2.0: `new OAuth2TokenService(transport, new InMemoryOAuth2TokenCache(), time, new TcpLoopbackRedirectReceiver(), new HeadlessBrowser())`
    where `HeadlessBrowser : IExternalBrowser` (private) throws `InvalidOperationException` with the exact text of ADR 0022 ("yaapic-cli
    cannot open a browser: ..."). Check how `OAuth2TokenService` surfaces the exception: the request must end as `Error` with that text,
    not crash the run; wrap it where the service already maps failures if needed.
  - `var sender = new RequestSender(transport, RequestAuthenticator.CreateDefault(transport, tokens, time));`
    `var scriptedSender = new ScriptedRequestSender(sender, registry, time, new FixedSecretRedactor(overrides.Select(o => o.Value)));`
  - `time ??= TimeProvider.System`.
- `docs/architecture.md`: no change here (M6-74).
- No new package; `docs/dependencies.md` unchanged.

## Out of scope

The CLI project (M6-69), argument handling and output (M6-70, M6-71).

## Acceptance criteria

- [ ] `tests/Yaapic.Infrastructure.Tests/Composition/HeadlessCompositionTests.cs` (or next to the existing composition tests), with
  `TempWorkspace` and `LoopbackServer`, real engines (skip rules of the milestone 3 end-to-end tests):
  1. a collection with two requests (`GET {{baseUrl}}/echo`, `POST .../echo`) and an environment `local` holding `baseUrl`: `CreateRunner` +
     `RunAsync` gives two `Passed` results;
  2. an override `token` (`KeyValueEntry("token", "s3cret-value", true, true)`) used in a header is echoed by the server, and a
     `console.log(yaapic.environment.get('token'))` line in the result reads `••••`;
  3. a post-response script sets an environment variable read by the next request (it is seen), and after the run every file of the
     workspace is byte-identical and no file or directory was added (compare a snapshot of the tree before and after; the workspace has no
     `.yaapic/` directory before the run);
  4. without an environment (`null`), an override `baseUrl` alone makes the two requests pass (the unnamed in-memory environment);
  5. a request with OAuth 2.0 authorization code ends as `Error` with the ADR 0022 browser message and the run goes on to the next request;
  6. `Dispose` twice does not throw.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.
- [ ] Docs: none in this card (M6-74).

## Suggested commit message

`feat(composition): add the headless runner services`
