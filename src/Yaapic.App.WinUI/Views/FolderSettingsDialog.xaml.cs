using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Collections;

namespace Yaapic.App.WinUI.Views;

/// <summary>Dialog that edits the name, description, variables and scripts of a folder through a <see cref="FolderSettingsViewModel"/>.</summary>
public sealed partial class FolderSettingsDialog : ContentDialog
{
    /// <summary>Initializes the dialog.</summary>
    /// <param name="viewModel">The folder settings to edit.</param>
    public FolderSettingsDialog(FolderSettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        Services.DialogButtons.Normalize(this, new Thickness(0, 10, 16, 10));
        Variables.ViewModel = viewModel.Variables;
        ScriptsEditor.ViewModel = viewModel.Scripts;
        Loaded += (_, _) =>
        {
            Fit();
            XamlRoot.Changed += (_, _) => Fit();
        };
    }

    /// <summary>Gets the edited folder settings.</summary>
    public FolderSettingsViewModel ViewModel { get; }

    // The footer Save runs the command; the dialog stays open unless the view model closes it.
    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        ViewModel.SaveCommand.Execute(null);
    }

    // Keeps the dialog clear of the title bar (the system caption buttons draw above any popup) when the window is small.
    private void Fit()
    {
        var size = XamlRoot.Size;
        Body.Width = Math.Clamp(size.Width - 64, 320, 640);
        Scroller.MaxHeight = Math.Max(64, size.Height - 2 * 56 - 52 - 56);
    }
}
