using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Secrets;
using Yaapic.ViewModels.Shell;

namespace Yaapic.ViewModels.Palette;

/// <summary>The fixed application commands of the palette; availability is evaluated each time the palette opens.</summary>
public sealed class CommandsPaletteSource : IPaletteSource
{
    private const string Category = "Command";

    private readonly ShellViewModel _shell;

    /// <summary>Creates the source.</summary>
    /// <param name="shell">The shell whose commands are offered.</param>
    public CommandsPaletteSource(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        _shell = shell;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PaletteItem> GetItems()
    {
        var tabs = _shell.Tabs;
        var active = tabs.ActiveTab;
        List<PaletteItem> items = [Item("new-request", "New request", "Ctrl+T", () => tabs.NewTab())];
        if (active is not null)
        {
            items.Add(Item("save-request", "Save request", "Ctrl+S", tabs.SaveActiveCommand));
            items.Add(Item("close-tab", "Close tab", "Ctrl+W", tabs.CloseActiveCommand));
            if (active.SendCommand.CanExecute(null))
            {
                items.Add(Item("send-request", "Send request", "Ctrl+Enter", active.SendCommand));
            }

            if (active.IsSending)
            {
                items.Add(Item("cancel-request", "Cancel request", "Esc", active.CancelCommand));
            }
        }

        if (tabs.Tabs.Count >= 2)
        {
            items.Add(Item("next-tab", "Next tab", "Alt+Down", tabs.NextTabCommand));
            items.Add(Item("previous-tab", "Previous tab", "Alt+Up", tabs.PreviousTabCommand));
        }

        items.Add(Item("new-collection", "New collection", null, _shell.Collections.NewCollectionCommand));
        items.Add(Item("manage-environments", "Manage environments", "Ctrl+Shift+E", () => _shell.RequestEnvironments()));
        items.Add(Item("show-collections", "Show collections", null, _shell.ShowCollectionsCommand));
        items.Add(Item("show-history", "Show history", null, _shell.ShowHistoryCommand));
        if (!_shell.History.IsEmpty)
        {
            items.Add(Item("clear-history", "Clear history", null, _shell.History.ClearCommand));
        }

        items.Add(Item("toggle-layout", "Toggle request layout (side by side or stacked)", null, _shell.Layout.ToggleCommand));
        items.Add(Item("toggle-console", "Toggle console", "Ctrl+`", _shell.ToggleConsoleCommand));
        items.Add(Item("clear-console", "Clear console", null, _shell.ClearConsoleCommand));
        var vault = _shell.Vault;
        switch (vault.State)
        {
            case VaultState.NotCreated:
                items.Add(Item("vault-setup", "Set up the secret vault...", null, vault.RequestSetupCommand));
                break;
            case VaultState.Locked:
                items.Add(Item("vault-unlock", "Unlock the vault...", null, vault.RequestUnlockCommand));
                break;
            default:
                items.Add(Item("vault-lock", "Lock the vault", null, vault.LockCommand));
                break;
        }

        var updates = _shell.Updates;
        if (updates.IsSupported)
        {
            items.Add(Item("check-updates", "Check for updates", null, () =>
            {
                updates.OpenSettingsCommand.Execute(null);
                _ = updates.CheckNowCommand.ExecuteAsync(null);
            }));
        }

        if (updates.State == Updates.UpdateState.ReadyToRestart)
        {
            items.Add(Item("restart-to-update", "Restart to update", null, updates.RestartAndApplyCommand));
        }

        return items;
    }

    private static PaletteItem Item(string slug, string title, string? shortcut, Action action) =>
        new($"command:{slug}", title, null, Category, shortcut, () =>
        {
            action();
            return Task.CompletedTask;
        });

    private static PaletteItem Item(string slug, string title, string? shortcut, ICommand command) =>
        new($"command:{slug}", title, null, Category, shortcut, () =>
        {
            if (command is IAsyncRelayCommand asyncCommand)
            {
                return asyncCommand.ExecuteAsync(null);
            }

            command.Execute(null);
            return Task.CompletedTask;
        });
}
