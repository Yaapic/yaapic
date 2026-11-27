using Avalonia.Controls;
using Avalonia.Interactivity;
using Yaapic.ViewModels.Monitors;
using Yaapic.ViewModels.Runner;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The selectable, reorderable request list shared by the Runner and Load test windows; its data context is a <see cref="RunnerSetupViewModel"/>.</summary>
public sealed partial class RunnerRequestListView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public RunnerRequestListView() => InitializeComponent();

    private void OnFolderChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is MonitorFolderChoice choice && DataContext is RunnerSetupViewModel setup)
        {
            setup.SelectedFolder = choice;
        }
    }

    private void OnMoveUp(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RunnerSetupViewModel setup && (sender as Control)?.DataContext is RunnerItemViewModel item)
        {
            setup.MoveUpCommand.Execute(item);
        }
    }

    private void OnMoveDown(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RunnerSetupViewModel setup && (sender as Control)?.DataContext is RunnerItemViewModel item)
        {
            setup.MoveDownCommand.Execute(item);
        }
    }
}
