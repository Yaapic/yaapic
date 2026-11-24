using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Import;

namespace Yaapic.App.WinUI.Views;

/// <summary>Dialog that imports a file, a folder or pasted text through an <see cref="ImportViewModel"/>.</summary>
public sealed partial class ImportDialog : ContentDialog
{
    /// <summary>Initializes the dialog and resets the view model.</summary>
    /// <param name="viewModel">The import view model.</param>
    public ImportDialog(ImportViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        viewModel.Reset();
        InitializeComponent();
    }

    /// <summary>Gets the import view model.</summary>
    public ImportViewModel ViewModel { get; }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();
}
