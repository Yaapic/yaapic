using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Runner;

namespace Yaapic.App.WinUI.Views;

/// <summary>The selectable, reorderable request list shared by the Runner and Load test windows.</summary>
public sealed partial class RunnerRequestListView : UserControl
{
    /// <summary>Identifies the <see cref="Setup"/> dependency property.</summary>
    public static readonly DependencyProperty SetupProperty = DependencyProperty.Register(
        nameof(Setup), typeof(RunnerSetupViewModel), typeof(RunnerRequestListView),
        new PropertyMetadata(null, (d, _) => ((RunnerRequestListView)d).Bindings.Update()));

    /// <summary>Initializes the view.</summary>
    public RunnerRequestListView() => InitializeComponent();

    /// <summary>Gets or sets the request list and data file view model.</summary>
    public RunnerSetupViewModel? Setup
    {
        get => (RunnerSetupViewModel?)GetValue(SetupProperty);
        set => SetValue(SetupProperty, value);
    }

    private void OnMoveUp(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is RunnerItemViewModel item)
        {
            Setup?.MoveUpCommand.Execute(item);
        }
    }

    private void OnMoveDown(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is RunnerItemViewModel item)
        {
            Setup?.MoveDownCommand.Execute(item);
        }
    }
}
