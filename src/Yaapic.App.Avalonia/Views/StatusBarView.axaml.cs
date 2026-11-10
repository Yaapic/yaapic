using Avalonia.Controls;
using Avalonia.Interactivity;
using Yaapic.ViewModels.Shell;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The 24 px status bar: script engine warning, vault item and the Console toggle.</summary>
public sealed partial class StatusBarView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public StatusBarView() => InitializeComponent();

    // Unlocked: a flyout offers Lock; otherwise the click opens the matching window.
    private void OnVaultClick(object? sender, RoutedEventArgs e)
    {
        if ((DataContext as ShellViewModel)?.Vault is not { } vault)
        {
            return;
        }

        if (vault.IsUnlocked)
        {
            var lockItem = new MenuItem { Header = "Lock vault" };
            lockItem.Click += (_, _) => vault.LockCommand.Execute(null);
            new MenuFlyout { Items = { lockItem } }.ShowAt(VaultButton);
        }
        else if (vault.CanSetUp)
        {
            vault.RequestSetupCommand.Execute(null);
        }
        else
        {
            vault.RequestUnlockCommand.Execute(null);
        }
    }
}
