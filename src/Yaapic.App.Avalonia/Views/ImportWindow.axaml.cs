using Avalonia.Controls;
using Avalonia.Interactivity;
using Yaapic.ViewModels.Import;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Modal window that imports a file, a folder or pasted text through an <see cref="ImportViewModel"/>.</summary>
public sealed partial class ImportWindow : Window
{
    /// <summary>Creates the window for the previewer; it has no view model.</summary>
    public ImportWindow() => InitializeComponent();

    /// <summary>Creates the window and resets the view model.</summary>
    /// <param name="viewModel">The import view model.</param>
    public ImportWindow(ImportViewModel viewModel) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        viewModel.Reset();
        DataContext = viewModel;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
