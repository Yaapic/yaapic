using Avalonia.Controls;
using Avalonia.Interactivity;
using Yaapic.ViewModels.Updates;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The About and update settings window: version, channel, automatic check and the state of the update.</summary>
public sealed partial class UpdatesWindow : Window
{
    /// <summary>Creates the window for the previewer; it has no view model.</summary>
    public UpdatesWindow() => InitializeComponent();

    /// <summary>Creates the window.</summary>
    /// <param name="viewModel">The update view model.</param>
    public UpdatesWindow(UpdateViewModel viewModel) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
        VersionText.Text = $"Version {viewModel.CurrentVersion}";
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
