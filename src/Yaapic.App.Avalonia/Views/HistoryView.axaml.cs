using Avalonia.Controls;
using Avalonia.Interactivity;
using Yaapic.ViewModels.History;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The sidebar request history, bound to a <see cref="HistoryViewModel"/>.</summary>
public sealed partial class HistoryView : UserControl
{
    /// <summary>Creates the view.</summary>
    public HistoryView() => InitializeComponent();

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: HistoryItemViewModel item } && DataContext is HistoryViewModel history)
        {
            history.OpenCommand.Execute(item);
        }
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: HistoryItemViewModel item } && DataContext is HistoryViewModel history)
        {
            history.DeleteCommand.Execute(item);
        }
    }
}
