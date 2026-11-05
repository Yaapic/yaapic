using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.History;

namespace Yaapic.App.WinUI.Views;

/// <summary>The sidebar request history, bound to a <see cref="HistoryViewModel"/>.</summary>
public sealed partial class HistoryView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(HistoryViewModel), typeof(HistoryView),
        new PropertyMetadata(null, (d, _) => ((HistoryView)d).Bindings.Update()));

    /// <summary>Initializes the view.</summary>
    public HistoryView() => InitializeComponent();

    /// <summary>Gets or sets the history shown.</summary>
    public HistoryViewModel? ViewModel
    {
        get => (HistoryViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is HistoryItemViewModel item)
        {
            ViewModel?.OpenCommand.Execute(item);
        }
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is HistoryItemViewModel item)
        {
            ViewModel?.DeleteCommand.Execute(item);
        }
    }
}
