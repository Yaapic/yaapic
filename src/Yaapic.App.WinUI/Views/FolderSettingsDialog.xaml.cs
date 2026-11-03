using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Collections;

namespace Yaapic.App.WinUI.Views;

/// <summary>Dialog that edits the name, description and variables of a folder through a <see cref="FolderSettingsViewModel"/>.</summary>
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
    }

    /// <summary>Gets the edited folder settings.</summary>
    public FolderSettingsViewModel ViewModel { get; }
}
