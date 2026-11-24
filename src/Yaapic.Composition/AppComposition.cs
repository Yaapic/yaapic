using System.Reflection;
using Yaapic.Core.Auth;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Core.CodeGeneration;
using Yaapic.Core.Export;
using Yaapic.Core.Export.Bruno;
using Yaapic.Core.Export.Insomnia;
using Yaapic.Core.Export.OpenApi;
using Yaapic.Core.Export.Postman;
using Yaapic.Core.Http;
using Yaapic.Core.Import;
using Yaapic.Core.Import.Bruno;
using Yaapic.Core.Import.Curl;
using Yaapic.Core.Import.Har;
using Yaapic.Core.Import.Insomnia;
using Yaapic.Core.Import.OpenApi;
using Yaapic.Core.Import.Postman;
using Yaapic.Core.Scripting;
using Yaapic.Core.Secrets;
using Yaapic.Core.Storage;
using Yaapic.Core.Updates;
using Yaapic.Infrastructure.Auth;
using Yaapic.Infrastructure.Certificates;
using Yaapic.Infrastructure.Http;
using Yaapic.Infrastructure.Import;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;
using Yaapic.Infrastructure.Updates;
using Yaapic.Scripting.Lua;
using Yaapic.Scripting.QuickJs;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.History;
using Yaapic.ViewModels.Import;
using Yaapic.ViewModels.Palette;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Settings;
using Yaapic.ViewModels.Shell;
using Yaapic.ViewModels.Tabs;
using Yaapic.ViewModels.Tools;
using Yaapic.ViewModels.Updates;
using Yaapic.ViewModels.Vault;

namespace Yaapic.Composition;

/// <summary>Wires the application object graph shared by every App project.</summary>
public static class AppComposition
{
    /// <summary>Reads the version of the running application from the entry assembly (<c>0.0.0-dev</c> when it carries none).</summary>
    /// <returns>The application information.</returns>
    public static IAppInfo CreateAppInfo() => CreateAppInfo(Assembly.GetEntryAssembly() ?? typeof(AppComposition).Assembly);

    internal static IAppInfo CreateAppInfo(Assembly assembly) =>
        new AppInfo(AppVersion.TryParse(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, out var version)
            ? version
            : AppVersion.Parse("0.0.0-dev"));

    /// <summary>Creates the shell view model on top of a workspace folder, creating the folder when needed.</summary>
    /// <param name="workspaceRoot">The workspace root directory.</param>
    /// <param name="dialogs">The platform dialog implementation.</param>
    /// <param name="platformProtectors">The platform key protectors; <see langword="null"/> offers the passphrase protector only.</param>
    /// <param name="userDataDirectory">Another per-user data directory for the vault file (screenshot captures); <see langword="null"/> for the real one.</param>
    /// <param name="updateDemo">Screenshot tooling only: a scripted update mode (see <see cref="UpdateDemoService"/>); <see langword="null"/> for the real update service.</param>
    /// <returns>The shell; call <see cref="ShellViewModel.InitializeAsync"/> before showing it.</returns>
    /// <exception cref="Yaapic.Core.Storage.StorageException">The workspace cannot be created.</exception>
    public static ShellViewModel CreateShell(string workspaceRoot, IDialogService dialogs, IKeyProtectorFactory? platformProtectors = null, string? userDataDirectory = null, string? updateDemo = null) =>
        updateDemo is null
            ? CreateShell(workspaceRoot, dialogs, platformProtectors, new DefaultVaultLocation(userDataDirectory), [new QuickJsEngine(), new LuaEngine()], updateSettings: new FileUpdateSettingsStore(userDataDirectory), userDataDirectory: userDataDirectory)
            : CreateShell(workspaceRoot, dialogs, platformProtectors, new DefaultVaultLocation(userDataDirectory), [new QuickJsEngine(), new LuaEngine()], updateService: new UpdateDemoService(updateDemo), updateSettings: new UpdateDemoService(updateDemo), userDataDirectory: userDataDirectory);

    // The seam of the tests: another vault location, other engines and other script limits.
    internal static ShellViewModel CreateShell(
        string workspaceRoot,
        IDialogService dialogs,
        IKeyProtectorFactory? platformProtectors,
        IVaultLocation vaultLocation,
        IEnumerable<IScriptEngine> scriptEngines,
        ScriptLimits? limits = null,
        IUpdateService? updateService = null,
        IUpdateSettingsStore? updateSettings = null,
        string? userDataDirectory = null,
        IHttpTransport? transport = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(dialogs);

        Workspace.EnsureCreated(workspaceRoot);
        var requestStore = new FileRequestStore(workspaceRoot);
        var sessionStore = new FileSessionStore(workspaceRoot);
        var protectors = platformProtectors ?? new PassphraseKeyProtectorFactory();
        var vault = new FileSecretVault(workspaceRoot, vaultLocation, protectors);

        var time = TimeProvider.System;
        var certificateStore = new FileCertificateStore(userDataDirectory);
        var certificates = new ClientCertificateProvider(certificateStore, vault);
        var versionSupport = new QuicHttpVersionSupport();
        var realTransport = new HttpClientTransport(
            DefaultHttpClient.Create(), DefaultHttpClient.CreateNonRedirecting(), TimeSpan.FromSeconds(30), certificates, support: versionSupport);
        var httpTransport = transport ?? realTransport;
        var tokens = new OAuth2TokenService(httpTransport, new InMemoryOAuth2TokenCache(), time, new TcpLoopbackRedirectReceiver(), new SystemBrowserLauncher());
        var sender = new RequestSender(httpTransport, RequestAuthenticator.CreateDefault(httpTransport, tokens, time));
        var codeGeneration = new CodeGenerationServices(
            new CodeGeneratorRegistry(
            [
                new CurlCodeGenerator(), new JavaScriptFetchCodeGenerator(), new PythonRequestsCodeGenerator(),
                new CSharpHttpClientCodeGenerator(), new PowerShellCodeGenerator(), new GoCodeGenerator(),
            ]),
            AppPreferences.CreateStore(userDataDirectory),
            dialogs);
        var reloader = new DeferredReloader();
        var vaultViewModel = new VaultViewModel(vault, protectors, reloader);

        var engines = new ScriptEngineRegistry();
        foreach (var engine in scriptEngines)
        {
            engines.Add(engine);
        }

        var scriptedSender = new ScriptedRequestSender(sender, engines, TimeProvider.System, new SecretRedactor(vault));
        var console = new ConsoleViewModel(dialogs);
        var environmentStore = new SecretAwareEnvironmentStore(new FileEnvironmentStore(workspaceRoot), vault);
        var globalsStore = new SecretAwareGlobalsStore(new FileGlobalsStore(workspaceRoot), vault);
        var collectionStore = new SecretAwareCollectionStore(new FileCollectionStore(workspaceRoot), vault);
        var environments = new EnvironmentsViewModel(environmentStore, dialogs, vaultViewModel);
        var globals = new GlobalsViewModel(globalsStore, vaultViewModel);
        VariableScopeProvider? scopes = null;
        HistoryViewModel? history = null;
        ScriptSendServices? scripting = null;
        var layout = new PaneLayoutViewModel();
        var tabs = new TabsViewModel(
            requestStore,
            dialogs,
            (definition, isNew) =>
            {
                var editor = RequestEditorViewModel.FromDefinition(definition, sender, scopes!, isNew, dialogs, scripting, tokens, codeGeneration, versionSupport);
                editor.Layout = layout;
                editor.Sent += (_, e) => _ = history!.RecordAsync(e.Request, e.Report);
                return editor;
            });
        history = new HistoryViewModel(new FileHistoryStore(workspaceRoot), TimeProvider.System, tabs, dialogs);
        var requests = new RequestListViewModel(requestStore, tabs, dialogs);
        var collections = new CollectionTreeViewModel(collectionStore, requestStore, tabs, dialogs, vaultViewModel);
        scopes = new VariableScopeProvider(environments, globals, collections);
        scripting = new ScriptSendServices(scriptedSender, new CollectionTreeScriptChainProvider(collections), console, scopes, limits);
        var updates = new UpdateViewModel(updateService ?? new VelopackUpdateService(), updateSettings ?? new FileUpdateSettingsStore(), CreateAppInfo(), new UpdateScheduler(TimeProvider.System), console);
        var shell = new ShellViewModel(tabs, requests, collections, history, environments, globals, sessionStore, dialogs, console, vaultViewModel, engines, updates, layout);
        reloader.Target = shell;
        shell.AttachTools(() => new CertificatesViewModel(certificateStore, vault, dialogs), () => new JwtDecoderViewModel(time));
        AttachImportExport(shell, dialogs, vault, requestStore, collectionStore, environmentStore, globalsStore);
        shell.Own(realTransport);
        shell.Own(certificates);
        shell.AttachPalette(new CommandPaletteViewModel(
        [
            new CommandsPaletteSource(shell, tokens),
            new RequestsPaletteSource(collections),
            new EnvironmentsPaletteSource(environments),
            new TabsPaletteSource(tabs),
        ]));
        return shell;
    }

    // Importers register in tie-break order of ImporterRegistry.Detect; each dialog opening gets a new view model.
    private static void AttachImportExport(
        ShellViewModel shell, IDialogService dialogs, ISecretVault vault, IRequestStore requestStore,
        ICollectionStore collectionStore, IEnvironmentStore environmentStore, IGlobalsStore globalsStore)
    {
        var structured = new StructuredTextReader(new VYamlTreeReader());
        var importers = new ImporterRegistry(
        [
            new PostmanCollectionImporter(), new PostmanEnvironmentImporter(), new OpenApiImporter(structured),
            new InsomniaImporter(), new HarImporter(), new BrunoImporter(), new CurlImporter(),
        ]);
        var service = new ImportService(importers, new ImportApplier(requestStore, collectionStore, environmentStore, globalsStore, () => vault.State == VaultState.Unlocked));
        var inputReader = new FileImportInputReader();
        var exporters = new ExporterRegistry(
        [
            new PostmanCollectionExporter(), new PostmanEnvironmentExporter(), new OpenApiJsonExporter(),
            new OpenApiYamlExporter(), new InsomniaExporter(), new BrunoExporter(),
        ]);
        var sourceReader = new ExportSourceReader(collectionStore, environmentStore, globalsStore);
        shell.AttachImportExport(
            () => new ImportViewModel(service, importers, inputReader, dialogs, shell.ReloadAsync),
            () => new ExportViewModel(exporters, sourceReader, collectionStore, environmentStore, dialogs));
    }

    // The vault view model needs the reloader before the shell exists.
    private sealed class DeferredReloader : IVaultReloader
    {
        public IVaultReloader? Target { get; set; }

        public Task ReloadAsync() => Target?.ReloadAsync() ?? Task.CompletedTask;
    }
}
