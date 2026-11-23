using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Import;

namespace Yaapic.App.WinUI.Views;

/// <summary>Dialog that exports a collection or environments to a file through an <see cref="ExportViewModel"/>.</summary>
public sealed partial class ExportDialog : ContentDialog
{
    /// <summary>Initializes the dialog.</summary>
    /// <param name="viewModel">The export view model, already loaded.</param>
    public ExportDialog(ExportViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>Gets the export view model.</summary>
    public ExportViewModel ViewModel { get; }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();
}
