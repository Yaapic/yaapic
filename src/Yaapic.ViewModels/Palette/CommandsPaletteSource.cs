using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Secrets;
using Yaapic.ViewModels.Shell;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Palette;

/// <summary>The fixed application commands of the palette; availability is evaluated each time the palette opens.</summary>
public sealed class CommandsPaletteSource : IPaletteSource
{
    private static string Category => Strings.PaletteCategoryCommand;

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
        List<PaletteItem> items = [Item("new-request", Strings.PaletteNewRequest, "Ctrl+T", () => tabs.NewTab())];
        if (active is not null)
        {
            items.Add(Item("save-request", Strings.PaletteSaveRequest, "Ctrl+S", tabs.SaveActiveCommand));
            items.Add(Item("close-tab", Strings.PaletteCloseTab, "Ctrl+W", tabs.CloseActiveCommand));
            if (active.SendCommand.CanExecute(null))
            {
                items.Add(Item("send-request", Strings.PaletteSendRequest, "Ctrl+Enter", active.SendCommand));
            }

            if (active.IsSending)
            {
                items.Add(Item("cancel-request", Strings.PaletteCancelRequest, "Esc", active.CancelCommand));
            }
        }

        if (tabs.Tabs.Count >= 2)
        {
            items.Add(Item("next-tab", Strings.PaletteNextTab, "Alt+Down", tabs.NextTabCommand));
            items.Add(Item("previous-tab", Strings.PalettePreviousTab, "Alt+Up", tabs.PreviousTabCommand));
        }

        items.Add(Item("new-collection", Strings.PaletteNewCollection, null, _shell.Collections.NewCollectionCommand));
        items.Add(Item("manage-environments", Strings.PaletteManageEnvironments, "Ctrl+Shift+E", () => _shell.RequestEnvironments()));
        items.Add(Item("show-collections", Strings.PaletteShowCollections, null, _shell.ShowCollectionsCommand));
        items.Add(Item("show-history", Strings.PaletteShowHistory, null, _shell.ShowHistoryCommand));
        if (!_shell.History.IsEmpty)
        {
            items.Add(Item("clear-history", Strings.PaletteClearHistory, null, _shell.History.ClearCommand));
        }

        items.Add(Item("toggle-layout", Strings.PaletteToggleLayout, null, _shell.Layout.ToggleCommand));
        items.Add(Item("toggle-console", Strings.PaletteToggleConsole, "Ctrl+`", _shell.ToggleConsoleCommand));
        items.Add(Item("clear-console", Strings.PaletteClearConsole, null, _shell.ClearConsoleCommand));
        var vault = _shell.Vault;
        switch (vault.State)
        {
            case VaultState.NotCreated:
                items.Add(Item("vault-setup", Strings.PaletteVaultSetup, null, vault.RequestSetupCommand));
                break;
            case VaultState.Locked:
                items.Add(Item("vault-unlock", Strings.PaletteVaultUnlock, null, vault.RequestUnlockCommand));
                break;
            default:
                items.Add(Item("vault-lock", Strings.PaletteVaultLock, null, vault.LockCommand));
                break;
        }

        var updates = _shell.Updates;
        if (updates.IsSupported)
        {
            items.Add(Item("check-updates", Strings.PaletteCheckUpdates, null, () =>
            {
                updates.OpenSettingsCommand.Execute(null);
                _ = updates.CheckNowCommand.ExecuteAsync(null);
            }));
        }

        if (updates.State == Updates.UpdateState.ReadyToRestart)
        {
            items.Add(Item("restart-to-update", Strings.PaletteRestartToUpdate, null, updates.RestartAndApplyCommand));
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
