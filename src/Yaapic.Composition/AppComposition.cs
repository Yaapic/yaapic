using Yaapic.Core.Http;
using Yaapic.Infrastructure.Http;
using Yaapic.Infrastructure.Storage;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Shell;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.Composition;

/// <summary>Wires the application object graph shared by every App project.</summary>
public static class AppComposition
{
    /// <summary>Creates the shell view model on top of a workspace folder, creating the folder when needed.</summary>
    /// <param name="workspaceRoot">The workspace root directory.</param>
    /// <param name="dialogs">The platform dialog implementation.</param>
    /// <returns>The shell; call <see cref="ShellViewModel.InitializeAsync"/> before showing it.</returns>
    /// <exception cref="Yaapic.Core.Storage.StorageException">The workspace cannot be created.</exception>
    public static ShellViewModel CreateShell(string workspaceRoot, IDialogService dialogs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(dialogs);

        Workspace.EnsureCreated(workspaceRoot);
        var requestStore = new FileRequestStore(workspaceRoot);
        var environmentStore = new FileEnvironmentStore(workspaceRoot);
        var sessionStore = new FileSessionStore(workspaceRoot);
        var sender = new RequestSender(new HttpClientTransport(DefaultHttpClient.Create(), TimeSpan.FromSeconds(30)));
        var environments = new EnvironmentsViewModel(environmentStore, dialogs);
        var tabs = new TabsViewModel(
            requestStore,
            dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, environments, isNew));
        var requests = new RequestListViewModel(requestStore, tabs, dialogs);
        return new ShellViewModel(tabs, requests, environments, sessionStore, dialogs);
    }
}
