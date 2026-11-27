using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Runner;

namespace Yaapic.App.WinUI.Views;

/// <summary>The results pane of the Runner window: progress, summary, filtered rows and the details of the selected row.</summary>
public sealed partial class RunnerResultsView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(RunnerViewModel), typeof(RunnerResultsView),
        new PropertyMetadata(null, (d, _) => ((RunnerResultsView)d).OnViewModelChanged()));

    private bool _syncing;

    /// <summary>Initializes the view.</summary>
    public RunnerResultsView() => InitializeComponent();

    /// <summary>Gets or sets the Runner view model.</summary>
    public RunnerViewModel? ViewModel
    {
        get => (RunnerViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged()
    {
        _syncing = true;
        FilterChips.SelectedIndex = ViewModel is { } vm ? (int)vm.Filter : 0;
        _syncing = false;
        Bindings?.Update();
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && ViewModel is { } vm && FilterChips.SelectedIndex >= 0)
        {
            vm.Filter = (RunnerFilter)FilterChips.SelectedIndex;
        }
    }

    private void OnRowSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.SelectedRow = RowsList.SelectedItem as RunnerResultRowViewModel;
        }
    }
}
