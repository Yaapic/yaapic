using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Yaapic.ViewModels.Shell;

namespace Yaapic.App.WinUI.Views;

/// <summary>The 24 px status bar: script engine warning, vault slot and the Console toggle with its counts.</summary>
public sealed partial class StatusBarView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(ShellViewModel), typeof(StatusBarView),
        new PropertyMetadata(null, (d, _) => ((StatusBarView)d).Bindings.Update()));

    /// <summary>Initializes the view.</summary>
    public StatusBarView() => InitializeComponent();

    /// <summary>Gets or sets the shell shown.</summary>
    public ShellViewModel? ViewModel
    {
        get => (ShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Unlocked: a flyout offers Lock; otherwise the click opens the matching dialog.
    private void OnVaultClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.Vault is not { } vault)
        {
            return;
        }

        if (vault.IsUnlocked)
        {
            FlyoutBase.ShowAttachedFlyout(VaultButton);
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
