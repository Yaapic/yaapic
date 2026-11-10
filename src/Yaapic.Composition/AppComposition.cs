using Yaapic.Core.Http;
using Yaapic.Core.Scripting;
using Yaapic.Core.Secrets;
using Yaapic.Infrastructure.Http;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;
using Yaapic.Scripting.Lua;
using Yaapic.Scripting.QuickJs;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.History;
using Yaapic.ViewModels.Palette;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Shell;
using Yaapic.ViewModels.Tabs;
using Yaapic.ViewModels.Vault;

namespace Yaapic.Composition;

/// <summary>Wires the application object graph shared by every App project.</summary>
public static class AppComposition
{
    /// <summary>Creates the shell view model on top of a workspace folder, creating the folder when needed.</summary>
    /// <param name="workspaceRoot">The workspace root directory.</param>
    /// <param name="dialogs">The platform dialog implementation.</param>
    /// <param name="platformProtectors">The platform key protectors; <see langword="null"/> offers the passphrase protector only.</param>
    /// <returns>The shell; call <see cref="ShellViewModel.InitializeAsync"/> before showing it.</returns>
    /// <exception cref="Yaapic.Core.Storage.StorageException">The workspace cannot be created.</exception>
    public static ShellViewModel CreateShell(string workspaceRoot, IDialogService dialogs, IKeyProtectorFactory? platformProtectors = null) =>
        CreateShell(workspaceRoot, dialogs, platformProtectors, new DefaultVaultLocation(), [new QuickJsEngine(), new LuaEngine()]);

    // The seam of the tests: another vault location and other engines.
    internal static ShellViewModel CreateShell(
        string workspaceRoot,
        IDialogService dialogs,
        IKeyProtectorFactory? platformProtectors,
        IVaultLocation vaultLocation,
        IEnumerable<IScriptEngine> scriptEngines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(dialogs);

        Workspace.EnsureCreated(workspaceRoot);
        var requestStore = new FileRequestStore(workspaceRoot);
        var sessionStore = new FileSessionStore(workspaceRoot);
        var sender = new RequestSender(new HttpClientTransport(DefaultHttpClient.Create(), DefaultHttpClient.CreateNonRedirecting(), TimeSpan.FromSeconds(30)));

        var protectors = platformProtectors ?? new PassphraseKeyProtectorFactory();
        var vault = new FileSecretVault(workspaceRoot, vaultLocation, protectors);
        var reloader = new DeferredReloader();
        var vaultViewModel = new VaultViewModel(vault, protectors, reloader);

        var engines = new ScriptEngineRegistry();
        foreach (var engine in scriptEngines)
        {
            engines.Add(engine);
        }

        var scriptedSender = new ScriptedRequestSender(sender, engines, TimeProvider.System, new SecretRedactor(vault));
        var console = new ConsoleViewModel(dialogs);
        var environments = new EnvironmentsViewModel(new SecretAwareEnvironmentStore(new FileEnvironmentStore(workspaceRoot), vault), dialogs, vaultViewModel);
        var globals = new GlobalsViewModel(new SecretAwareGlobalsStore(new FileGlobalsStore(workspaceRoot), vault), vaultViewModel);
        VariableScopeProvider? scopes = null;
        HistoryViewModel? history = null;
        ScriptSendServices? scripting = null;
        var tabs = new TabsViewModel(
            requestStore,
            dialogs,
            (definition, isNew) =>
            {
                var editor = RequestEditorViewModel.FromDefinition(definition, sender, scopes!, isNew, dialogs, scripting);
                editor.Sent += (_, e) => _ = history!.RecordAsync(e.Request, e.Report);
                return editor;
            });
        history = new HistoryViewModel(new FileHistoryStore(workspaceRoot), TimeProvider.System, tabs, dialogs);
        var requests = new RequestListViewModel(requestStore, tabs, dialogs);
        var collections = new CollectionTreeViewModel(
            new SecretAwareCollectionStore(new FileCollectionStore(workspaceRoot), vault), requestStore, tabs, dialogs, vaultViewModel);
        scopes = new VariableScopeProvider(environments, globals, collections);
        scripting = new ScriptSendServices(scriptedSender, new CollectionTreeScriptChainProvider(collections), console, scopes);
        var shell = new ShellViewModel(tabs, requests, collections, history, environments, globals, sessionStore, dialogs, console, vaultViewModel, engines);
        reloader.Target = shell;
        shell.AttachPalette(new CommandPaletteViewModel(
        [
            new CommandsPaletteSource(shell),
            new RequestsPaletteSource(collections),
            new EnvironmentsPaletteSource(environments),
            new TabsPaletteSource(tabs),
        ]));
        return shell;
    }

    // The vault view model needs the reloader before the shell exists.
    private sealed class DeferredReloader : IVaultReloader
    {
        public IVaultReloader? Target { get; set; }

        public Task ReloadAsync() => Target?.ReloadAsync() ?? Task.CompletedTask;
    }
}
