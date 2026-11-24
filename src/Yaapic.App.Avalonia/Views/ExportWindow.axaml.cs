using Avalonia.Controls;
using Avalonia.Interactivity;
using Yaapic.ViewModels.Import;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Modal window that exports a collection or environments through an <see cref="ExportViewModel"/>.</summary>
public sealed partial class ExportWindow : Window
{
    /// <summary>Creates the window for the previewer; it has no view model.</summary>
    public ExportWindow() => InitializeComponent();

    /// <summary>Creates the window bound to a view model that the caller has loaded.</summary>
    /// <param name="viewModel">The export view model.</param>
    public ExportWindow(ExportViewModel viewModel) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
